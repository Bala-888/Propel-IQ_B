using System.Data;
using System.Threading.Channels;
using Api.Audit;
using Api.Data;
using Api.Data.Entities;
using Api.DTOs;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Api.Services;

// ── Booking result discriminated union ────────────────────────────────────────────────────────

/// <summary>Discriminated-union result returned by <see cref="BookingService.CreateBookingAsync"/>.</summary>
public abstract class BookingResult { }

public sealed class BookingSuccess : BookingResult
{
    public int             BookingId { get; init; }
    public string          Status    { get; init; } = string.Empty;
    public SlotDto         Slot      { get; init; } = default!;
}

public sealed class BookingNotFound : BookingResult { }

public sealed class BookingConflict : BookingResult
{
    public IReadOnlyList<SlotDto>? Alternatives { get; init; }
}

public sealed class BookingDuplicateWindow : BookingResult { }

public sealed class BookingLockTimeout : BookingResult { }

public sealed class BookingError : BookingResult
{
    public string Message { get; init; } = string.Empty;
}

// ── Service ───────────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Handles the ACID appointment booking transaction for <c>POST /bookings</c> (us_020).
///
/// <para>
/// Uses <c>SELECT … FOR UPDATE</c> with a 10-second lock timeout to prevent double-booking.
/// Transaction isolation is <c>Serializable</c> to eliminate phantom-read anomalies.
/// </para>
/// </summary>
public sealed class BookingService
{
    private readonly AppDbContext               _db;
    private readonly SlotsService               _slotsService;
    private readonly Api.Services.IAuditLogger  _audit;
    private readonly ILogger<BookingService>    _logger;
    private readonly Channel<BookingCreatedEvent>        _channel;
    private readonly Channel<BookingConfirmedEvent>      _confirmationChannel;
    private readonly Channel<PreferredSlotReleasedEvent> _preferredSlotReleasedChannel;
    private readonly ICalendarSyncService               _calendarSync;

    public BookingService(
        AppDbContext                      db,
        SlotsService                     slotsService,
        Api.Services.IAuditLogger        audit,
        ILogger<BookingService>          logger,
        Channel<BookingCreatedEvent>     channel,
        Channel<BookingConfirmedEvent>   confirmationChannel,
        Channel<PreferredSlotReleasedEvent> preferredSlotReleasedChannel,
        ICalendarSyncService             calendarSync)
    {
        _db                           = db;
        _slotsService                 = slotsService;
        _audit                        = audit;
        _logger                       = logger;
        _channel                      = channel;
        _confirmationChannel          = confirmationChannel;
        _preferredSlotReleasedChannel = preferredSlotReleasedChannel;
        _calendarSync                 = calendarSync;
    }

    /// <summary>
    /// Books a slot for <paramref name="patientId"/> inside a serializable transaction.
    /// </summary>
    /// <param name="slotId">ID of the <c>appointment_slots</c> row to reserve.</param>
    /// <param name="patientId">Patient ID sourced exclusively from the JWT sub claim (OWASP A01).</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<BookingResult> CreateBookingAsync(
        int               slotId,
        int               patientId,
        CancellationToken ct = default)
    {
        // Begin serializable transaction — prevents phantom reads during concurrent booking (AC-005)
        await using var tx = await _db.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, ct);

        try
        {
            // Set a per-statement lock timeout so the thread is not blocked indefinitely (AC-004)
            await _db.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '10s'", ct);

            // SELECT … FOR UPDATE acquires a row-level lock, preventing concurrent modifications
            // to the same slot row for the duration of this transaction (AC-005; checklist).
            // EF Core has no built-in FOR UPDATE; raw SQL is required.
            var slot = await _db.AppointmentSlots
                .FromSqlRaw(
                    "SELECT * FROM appointment_slots WHERE id = {0} FOR UPDATE",
                    slotId)
                .FirstOrDefaultAsync(ct);

            if (slot is null)
            {
                await tx.RollbackAsync(ct);
                return new BookingNotFound();
            }

            // Load the patient record inside the transaction for PHI needed in the confirmation email
            var patient = await _db.Patients.FindAsync(new object[] { patientId }, ct);

            if (!slot.IsAvailable)
            {
                // Fetch alternatives before rolling back — they are read-only queries
                var alternatives = await _slotsService.GetNearestAvailableSlotsAsync(slot, count: 3, ct);
                await tx.RollbackAsync(ct);
                return new BookingConflict { Alternatives = alternatives };
            }

            // Time-window overlap check — prevents the same patient booking two overlapping slots (AC-002)
            // Uses the existing Bookings + AppointmentSlot navigation so no extra raw SQL is needed.
            var hasOverlap = await _db.Bookings
                .Include(b => b.AppointmentSlot)
                .AnyAsync(b =>
                    b.PatientId == patientId &&
                    b.Status    == "Confirmed" &&
                    b.AppointmentSlot.SlotStart < slot.SlotEnd &&
                    b.AppointmentSlot.SlotEnd   > slot.SlotStart,
                    ct);

            if (hasOverlap)
            {
                await tx.RollbackAsync(ct);
                return new BookingDuplicateWindow();
            }

            // Mark slot as booked and insert the Booking row
            slot.IsAvailable = false;

            var booking = new Booking
            {
                PatientId          = patientId,
                AppointmentSlotId  = slot.Id,
                Status             = "Confirmed",
                BookedAt           = DateTime.UtcNow,
            };

            _db.Bookings.Add(booking);

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            // Enqueue BookingCreatedEvent — synchronous TryWrite never blocks the 201 response (AC-003)
            var evt = new BookingCreatedEvent(
                BookingId:        booking.Id,
                PatientId:        patientId,
                SlotDate:         DateOnly.FromDateTime(slot.SlotStart),
                SlotStartTime:    TimeOnly.FromDateTime(slot.SlotStart),
                BookingCreatedAt: booking.BookedAt);

            if (!_channel.Writer.TryWrite(evt))
            {
                // Channel at capacity — log and continue; booking is already committed (AC-003; OWASP A05)
                _logger.LogWarning(
                    "BookingCreatedEventDropped: channel full for BookingId={BookingId}",
                    booking.Id);
            }

            // Enqueue BookingConfirmedEvent — triggers confirmation email pipeline (us_022; AC-003)
            if (patient is not null && !string.IsNullOrWhiteSpace(patient.Email))
            {
                var confirmedEvt = new BookingConfirmedEvent(
                    BookingId:           booking.Id,
                    PatientId:           patientId,
                    PatientEmail:        patient.Email,
                    PatientFullName:     $"{patient.FirstName} {patient.LastName}".Trim(),
                    AppointmentDateTime: new DateTimeOffset(slot.SlotStart, TimeSpan.Zero),
                    ClinicName:          "PropelIQ Clinic",
                    ClinicAddress:       "123 Health Ave, Suite 100",
                    ProviderName:        slot.ProviderName);

                if (!_confirmationChannel.Writer.TryWrite(confirmedEvt))
                {
                    _logger.LogWarning(
                        "BookingConfirmedEventDropped: channel full for BookingId={BookingId}",
                        booking.Id);
                }
            }
            else
            {
                _logger.LogWarning(
                    "ConfirmationEmailSkipped: patient email not available for BookingId={BookingId} PatientId={PatientId}",
                    booking.Id, patientId);
            }

            // Audit — structural IDs only; no PHI in the audit entry (AC-003; OWASP A09)
            _audit.Log(
                actorId:    patientId.ToString(),
                actionType: AuditActionTypes.BookingCreate,
                resourceId: booking.Id.ToString());

            _logger.LogInformation(
                "Booking created: bookingId={BookingId} patientId={PatientId} slotId={SlotId}",
                booking.Id, patientId, slotId);

            var slotDto = new SlotDto(
                slot.Id,
                DateOnly.FromDateTime(slot.SlotStart),
                TimeOnly.FromDateTime(slot.SlotStart),
                (int)(slot.SlotEnd - slot.SlotStart).TotalMinutes,
                "Booked",
                slot.ProviderName);

            return new BookingSuccess
            {
                BookingId = booking.Id,
                Status    = "Confirmed",
                Slot      = slotDto,
            };
        }
        catch (PostgresException pgEx) when (pgEx.SqlState == "55P03") // lock_not_available
        {
            // Another transaction holds the row lock and our 10-second timeout expired (AC-004)
            try { await tx.RollbackAsync(ct); } catch { /* best-effort */ }
            _logger.LogWarning("Lock timeout acquiring slot row slotId={SlotId}", slotId);
            return new BookingLockTimeout();
        }
        catch (DbUpdateException dbEx)
        {
            try { await tx.RollbackAsync(ct); } catch { /* best-effort */ }
            _logger.LogError(dbEx, "DbUpdateException during booking for slotId={SlotId}", slotId);
            return new BookingError { Message = "Booking could not be persisted." };
        }
        catch (Exception ex)
        {
            try { await tx.RollbackAsync(ct); } catch { /* best-effort */ }
            _logger.LogError(ex, "Unexpected error during booking for slotId={SlotId}", slotId);
            return new BookingError { Message = "An unexpected error occurred." };
        }
    }

    // ── Cancel booking ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Cancels a confirmed booking owned by <paramref name="patientId"/>.
    /// Deletes any associated <c>preferred_slots</c> row and enqueues a
    /// <see cref="PreferredSlotReleasedEvent"/> for the us_025 monitoring worker (us_024 Edge: booking cancelled).
    /// </summary>
    public async Task<CancelBookingResult> CancelBookingAsync(
        int               bookingId,
        int               patientId,
        CancellationToken ct = default)
    {
        var booking = await _db.Bookings
            .FirstOrDefaultAsync(b => b.Id == bookingId, ct);

        if (booking is null || booking.PatientId != patientId)
            return new CancelBookingForbidden();

        if (booking.Status == "Cancelled")
            return new CancelBookingAlreadyCancelled();

        // Delete preferred slot row before status update — ExecuteDeleteAsync is a bulk operation
        // and does not require the booking to still be in "Confirmed" state (us_024 Edge)
        var hadPreferredSlot = await _db.PreferredSlots
            .Where(ps => ps.BookingId == bookingId)
            .ExecuteDeleteAsync(ct) > 0;

        booking.Status = "Cancelled";

        // Mark the previously-booked slot as available again
        var slot = await _db.AppointmentSlots.FindAsync(new object[] { booking.AppointmentSlotId }, ct);
        if (slot is not null)
            slot.IsAvailable = true;

        await _db.SaveChangesAsync(ct);

        // ── Calendar sync delete hook (us_028; AC-004) ────────────────────────────────────────
        // Executed after SaveChangesAsync() so the booking cancellation is committed first.
        // A calendar failure here MUST NOT roll back or affect the booking cancellation (AC-005).
        // NOTE: When RescheduleAsync is implemented it should call _calendarSync.UpdateAsync() in
        //       a similar try/catch AFTER committing the reschedule state change (AC-003).
        try
        {
            var syncedRows = await _db.BookingCalendarSyncs
                .Where(s => s.BookingId == bookingId && s.Status == "Synced")
                .ToListAsync(ct);

            foreach (var row in syncedRows)
            {
                var deleteResult = await _calendarSync.DeleteAsync(bookingId, row.Provider, ct);
                if (deleteResult == SyncResult.TokenExpired)
                {
                    _logger.LogWarning(
                        "CalendarDeleteTokenExpired: BookingId={BookingId} Provider={Provider}",
                        bookingId, row.Provider);
                }
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            // Log only — cancellation is already committed; calendar failure must not resurface
            _logger.LogError(ex,
                "CalendarDeleteHookFailed: BookingId={BookingId}", bookingId);
        }

        // Enqueue PreferredSlotReleasedEvent if a preferred slot was registered — us_025 hook
        if (hadPreferredSlot)
        {
            if (!_preferredSlotReleasedChannel.Writer.TryWrite(new PreferredSlotReleasedEvent(bookingId)))
            {
                _logger.LogWarning(
                    "PreferredSlotReleasedEventDropped: channel full for BookingId={BookingId}",
                    bookingId);
            }
        }

        _audit.Log(
            actorId:    patientId.ToString(),
            actionType: AuditActionTypes.BookingModify,
            resourceId: bookingId.ToString());

        _logger.LogInformation(
            "Booking cancelled: bookingId={BookingId} patientId={PatientId}",
            bookingId, patientId);

        return new CancelBookingSuccess();
    }
}

// ── Cancel booking result union ───────────────────────────────────────────────────────────────────

public abstract record CancelBookingResult { }
public sealed record CancelBookingSuccess            : CancelBookingResult;
public sealed record CancelBookingForbidden          : CancelBookingResult;
public sealed record CancelBookingAlreadyCancelled   : CancelBookingResult;
