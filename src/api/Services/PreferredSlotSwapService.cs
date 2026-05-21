using System.Data;
using Api.Data;
using Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

/// <summary>
/// Executes the ACID four-write preferred-slot swap for a single candidate (us_025; AC-002).
///
/// <para>
/// Each call to <see cref="SwapAsync"/> opens its own <c>Serializable</c>
/// <see cref="Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction"/>, re-verifies slot
/// availability under a row-level lock, performs all four writes, and commits — or rolls back
/// leaving every row in its pre-swap state (AC-004).
/// </para>
///
/// <para>
/// PHI guardrail: only non-PHI integer identifiers are written to Serilog log fields
/// (OWASP A02; HIPAA minimum-necessary — no patient name, DOB, or medical data in logs).
/// </para>
/// </summary>
public sealed class PreferredSlotSwapService : IPreferredSlotSwapService
{
    private readonly AppDbContext                    _db;
    private readonly ILogger<PreferredSlotSwapService> _logger;

    public PreferredSlotSwapService(AppDbContext db, ILogger<PreferredSlotSwapService> logger)
    {
        _db     = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<SwapResult> SwapAsync(int preferredSlotId, CancellationToken ct = default)
    {
        // Clear any stale tracked entities from earlier calls in the same cycle scope so that
        // FOR UPDATE re-fetches always execute real SQL against the database (DI lifetime; OWASP A04).
        _db.ChangeTracker.Clear();

        // Serializable isolation prevents phantom reads across the four-write atomic unit (AC-002)
        await using var tx = await _db.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, ct);

        try
        {
            // Short lock timeout prevents the job from holding the transaction open for minutes
            // if the database is under heavy write contention (Edge: lock escalation).
            await _db.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '5s'", ct);

            // ── Re-fetch preferred slot row ───────────────────────────────────────────────
            // The row may have been processed already by a concurrent tick (race window between
            // the candidate query and this transaction opening).
            var preferredSlotRow = await _db.PreferredSlots
                .FirstOrDefaultAsync(ps => ps.Id == preferredSlotId, ct);

            if (preferredSlotRow is null)
            {
                // Already processed — skip silently
                await tx.RollbackAsync(ct);
                return new SwapSkipped();
            }

            // ── Lock destination slot with SELECT FOR UPDATE ───────────────────────────────
            // EF Core has no built-in FOR UPDATE; raw SQL is required (same pattern as BookingService).
            // Locks the row so a concurrent booking request cannot claim the same slot between
            // our availability check and the IsAvailable=false write (AC-002; AC-004).
            var destinationSlot = await _db.AppointmentSlots
                .FromSqlRaw(
                    "SELECT * FROM appointment_slots WHERE id = {0} FOR UPDATE",
                    preferredSlotRow.SlotId)
                .FirstOrDefaultAsync(ct);

            if (destinationSlot is null || !destinationSlot.IsAvailable)
            {
                // Slot became unavailable (blocked, booked by another patient, or deleted) since
                // the candidate query ran — skip but preserve the preferred_slots row so it is
                // re-evaluated on the next cycle (Edge: Blocked slot; AC-004).
                await tx.RollbackAsync(ct);
                _logger.LogWarning(
                    "SlotSwapSkippedBlocked: PreferredSlotId={PreferredSlotId} SlotId={SlotId}",
                    preferredSlotId, preferredSlotRow.SlotId);
                return new SwapSkipped();
            }

            // ── Load original booking (tracked; needs Status update) ───────────────────────
            var originalBooking = await _db.Bookings
                .FirstOrDefaultAsync(b => b.Id == preferredSlotRow.BookingId, ct);

            if (originalBooking is null)
            {
                // Booking was hard-deleted in a concurrent request — preserve preferred_slots row
                await tx.RollbackAsync(ct);
                _logger.LogWarning(
                    "SlotSwapSkippedBookingGone: PreferredSlotId={PreferredSlotId} BookingId={BookingId}",
                    preferredSlotId, preferredSlotRow.BookingId);
                return new SwapSkipped();
            }

            // ── Lock original slot with SELECT FOR UPDATE ─────────────────────────────────
            // Prevents a concurrent booking from claiming the slot we are about to restore
            // to Available — essential for the atomic pair (destinationSlot.Booked ↔ originalSlot.Available).
            var originalSlot = await _db.AppointmentSlots
                .FromSqlRaw(
                    "SELECT * FROM appointment_slots WHERE id = {0} FOR UPDATE",
                    originalBooking.AppointmentSlotId)
                .FirstOrDefaultAsync(ct);

            // Capture IDs before writes for the event and audit log (AC-005)
            var patientId = originalBooking.PatientId;
            var oldSlotId = originalBooking.AppointmentSlotId;
            var newSlotId = preferredSlotRow.SlotId;

            // ── ACID four-write block (AC-002) ────────────────────────────────────────────
            // (a) Cancel the original booking
            originalBooking.Status = "Cancelled";

            // (b) Create a new confirmed booking on the preferred slot
            var newBooking = new Booking
            {
                PatientId         = patientId,
                AppointmentSlotId = newSlotId,
                Status            = "Confirmed",
                BookedAt          = DateTime.UtcNow,
                NoShowRiskTier    = "Unknown",   // risk pipeline will score asynchronously
            };
            _db.Bookings.Add(newBooking);

            // (c) Mark destination slot as no longer available
            destinationSlot.IsAvailable = false;

            // (d) Restore original slot to available (freeing it for other patients)
            if (originalSlot is not null)
            {
                originalSlot.IsAvailable = true;
            }
            else
            {
                // Slot row missing — log but do NOT abort; the primary goal (swap) can still proceed.
                // The original slot may have been administratively removed; treat as tolerated inconsistency.
                _logger.LogWarning(
                    "SlotSwapOriginalSlotMissing: PreferredSlotId={PreferredSlotId} OriginalSlotId={OriginalSlotId} — swap will proceed without restoring availability",
                    preferredSlotId, oldSlotId);
            }

            // (e) Remove the preferred_slots designation — no longer needed after the swap
            _db.PreferredSlots.Remove(preferredSlotRow);

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            // ── Commit succeeded ─────────────────────────────────────────────────────────

            _logger.LogInformation(
                "SlotSwapCompleted: PatientId={PatientId} OldSlotId={OldSlotId} NewSlotId={NewSlotId} NewBookingId={NewBookingId}",
                patientId, oldSlotId, newSlotId, newBooking.Id);

            return new SwapSuccess
            {
                PatientId    = patientId,
                OldSlotId    = oldSlotId,
                NewSlotId    = newSlotId,
                NewBookingId = newBooking.Id,
            };
        }
        catch (Exception ex)
        {
            // RollbackAsync best-effort — the connection may already be broken (Edge: DB outage)
            try { await tx.RollbackAsync(ct); } catch { /* intentionally swallowed */ }

            // AC-004: log failure with identifiers only (no PHI); preferred_slots row was NOT removed
            // because SaveChangesAsync and CommitAsync did not execute — it will be re-evaluated next cycle.
            _logger.LogError(ex,
                "SlotSwapFailed: PreferredSlotId={PreferredSlotId}",
                preferredSlotId);

            return new SwapFailed();
        }
    }
}
