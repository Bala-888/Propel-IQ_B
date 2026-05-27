using System.Data;
using Api.Audit;
using Api.Data;
using Api.Data.Entities;
using Api.DTOs;
using Api.Features.Queue;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Api.Features.Bookings;

/// <summary>
/// Implements <see cref="IWalkinBookingService"/> for <c>POST /api/bookings/walkin</c> (us_030/AC-003).
///
/// <para>
/// Transaction flow:
/// <list type="number">
///   <item>Duplicate-today check runs BEFORE acquiring a lock — avoids holding the row lock
///         while the duplicate check query executes (edge: duplicate walk-in; checklist).</item>
///   <item>Serializable transaction + <c>SET LOCAL lock_timeout = '5s'</c> prevents indefinite blocking.</item>
///   <item><c>SELECT … FOR UPDATE</c> on appointment_slots eliminates TOCTOU double-booking (OWASP A04).</item>
///   <item>Queue position computed after <c>CommitAsync</c> — reflects the committed row (AC-003).</item>
/// </list>
/// </para>
/// </summary>
public sealed class WalkinBookingService : IWalkinBookingService
{
    private readonly AppDbContext             _db;
    private readonly Api.Services.IAuditLogger _audit;
    private readonly ILogger<WalkinBookingService> _logger;
    private readonly IQueueHubService         _hub;

    public WalkinBookingService(
        AppDbContext db,
        Api.Services.IAuditLogger audit,
        ILogger<WalkinBookingService> logger,
        IQueueHubService hub)
    {
        _db     = db;
        _audit  = audit;
        _logger = logger;
        _hub    = hub;
    }

    /// <inheritdoc />
    public async Task<WalkinBookingResult> CreateAsync(
        WalkinBookingRequest request,
        string staffId,
        CancellationToken ct = default)
    {
        // ── Boundary validation: patient must exist ───────────────────────────────────────────
        var patientExists = await _db.Patients.AnyAsync(p => p.Id == request.PatientId, ct);
        if (!patientExists)
            return new WalkinBookingPatientNotFound();

        // ── Duplicate-today check (before opening transaction; checklist) ─────────────────────
        // Compares slot start date in UTC so timezone drift does not create phantom duplicates.
        var today      = DateTime.UtcNow.Date;
        var endOfToday = today.AddDays(1);

        if (!request.OverrideDuplicate)
        {
            var existingBooking = await _db.Bookings
                .Include(b => b.AppointmentSlot)
                .Where(b =>
                    b.PatientId == request.PatientId &&
                    b.Status    == "Confirmed"       &&
                    b.AppointmentSlot.SlotStart >= today &&
                    b.AppointmentSlot.SlotStart <  endOfToday)
                .Select(b => (int?)b.Id)
                .FirstOrDefaultAsync(ct);

            if (existingBooking.HasValue)
                return new WalkinBookingDuplicateToday { ExistingBookingId = existingBooking.Value };
        }

        // ── Serializable transaction + slot lock ──────────────────────────────────────────────
        await using var tx = await _db.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, ct);

        try
        {
            // 5-second lock timeout — returns 503 to caller on SqlState 55P03 (OWASP A04; AC-003)
            await _db.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '5s'", ct);

            // SELECT … FOR UPDATE acquires a row-level exclusive lock for the slot row (OWASP A04)
            var slot = await _db.AppointmentSlots
                .FromSqlRaw(
                    "SELECT * FROM appointment_slots WHERE id = {0} FOR UPDATE",
                    request.SlotId)
                .FirstOrDefaultAsync(ct);

            if (slot is null || !slot.IsAvailable)
            {
                await tx.RollbackAsync(ct);
                return new WalkinBookingSlotUnavailable();
            }

            // Mark slot as booked and insert the Booking row
            slot.IsAvailable = false;

            var booking = new Booking
            {
                PatientId         = request.PatientId,
                AppointmentSlotId = slot.Id,
                Status            = "Confirmed",
                BookedAt          = DateTime.UtcNow,
                ReasonForVisit    = request.ReasonForVisit,
                Priority          = request.Priority,
                CreatedByStaffId  = staffId,
            };

            _db.Bookings.Add(booking);
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            // ── Patch patient contact info captured at walk-in (outside slot-lock tx; best-effort) ──────
            // Runs after the booking commits so a failure here does not roll back the confirmed booking.
            if (!string.IsNullOrWhiteSpace(request.Phone) || !string.IsNullOrWhiteSpace(request.InsuranceProvider))
            {
                var patient = await _db.Patients.FindAsync(new object[] { request.PatientId }, ct);
                if (patient is not null)
                {
                    if (!string.IsNullOrWhiteSpace(request.Phone))
                        patient.Phone = request.Phone;
                    if (!string.IsNullOrWhiteSpace(request.InsuranceProvider))
                        patient.InsuranceProvider = request.InsuranceProvider;
                    await _db.SaveChangesAsync(ct);
                }
            }

            // ── Queue position: count of Confirmed bookings for today, committed at or before this one ──
            var queuePosition = await _db.Bookings
                .Include(b => b.AppointmentSlot)
                .CountAsync(b =>
                    b.AppointmentSlot.SlotStart >= today      &&
                    b.AppointmentSlot.SlotStart <  endOfToday &&
                    b.Status  == "Confirmed"                  &&
                    b.BookedAt <= booking.BookedAt,
                    ct);

            // Audit: staffId + bookingId only — no PHI (OWASP A02; AC-003)
            _audit.Log(staffId, AuditActionTypes.WalkinBookingCreated, booking.Id.ToString());

            // us_033/AC-001: broadcast QueueEntryAdded after the transaction commits successfully.
            // Patient name lookup uses CancellationToken.None so a client disconnect after commit
            // does not cancel the broadcast (Edge: broadcast must not affect committed data; OWASP A04).
            var patientName = await _db.Patients
                .Where(p => p.Id == booking.PatientId)
                .Select(p => $"{p.FirstName} {p.LastName}")
                .FirstOrDefaultAsync(CancellationToken.None) ?? string.Empty;

            var broadcastDto = new QueueEntryDto(
                Id              : booking.Id,
                Position        : queuePosition,
                PatientName     : patientName,
                ArrivalTime     : null,
                AppointmentTime : new DateTimeOffset(DateTime.SpecifyKind(slot.SlotStart, DateTimeKind.Utc)),
                NoShowRiskTier  : "Unknown", // risk score is asynchronous; not yet available post-commit
                Status          : "Confirmed");

            await _hub.BroadcastEntryAddedAsync(broadcastDto);

            return new WalkinBookingSuccess
            {
                BookingId     = booking.Id,
                Status        = "Confirmed",
                QueuePosition = queuePosition,
            };
        }
        catch (PostgresException pgEx) when (pgEx.SqlState == "55P03")
        {
            // Lock timeout — another transaction holds the slot lock; advise retry (OWASP A04)
            _logger.LogWarning(
                "WalkinBookingLockTimeout: slot {SlotId} lock timed out for staff {StaffId}",
                request.SlotId, staffId);
            await tx.RollbackAsync(ct);
            return new WalkinBookingLockTimeout();
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }
}
