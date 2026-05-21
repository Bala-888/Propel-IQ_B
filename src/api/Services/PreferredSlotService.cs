using Api.Data;
using Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

/// <summary>
/// Registers or replaces a patient's preferred alternative appointment slot (us_024).
///
/// <para>
/// Guards are applied in order: ownership → status → same-slot → availability.
/// Each guard returns a typed result — no exceptions are thrown for known business-rule
/// violations so the caller can map results to precise HTTP status codes (AC-003, AC-004;
/// Edge: slot unavailable; OWASP A01).
/// </para>
///
/// <para>
/// Upsert strategy: <c>ExecuteDeleteAsync</c> on any existing row for the booking, then
/// <c>Add</c> + <c>SaveChangesAsync</c>.  The UNIQUE constraint on <c>booking_id</c> handles
/// concurrent duplicate inserts at the database level (AC-002; performance — no SELECT FOR UPDATE).
/// </para>
/// </summary>
public sealed class PreferredSlotService : IPreferredSlotService
{
    private readonly AppDbContext              _db;
    private readonly ILogger<PreferredSlotService> _logger;

    public PreferredSlotService(AppDbContext db, ILogger<PreferredSlotService> logger)
    {
        _db     = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<SetPreferredSlotResult> SetPreferredSlotAsync(
        int               bookingId,
        int               patientId,
        int               slotId,
        CancellationToken ct = default)
    {
        // ── Guard 1: ownership ──────────────────────────────────────────────────────────────
        // Load booking without tracking — guards are read-only up to the upsert step (AC-001)
        var booking = await _db.Bookings
            .FirstOrDefaultAsync(b => b.Id == bookingId, ct);

        if (booking is null || booking.PatientId != patientId)
        {
            // Return Forbidden for both null and wrong-patient cases to prevent booking-ID
            // enumeration by unauthenticated parties (OWASP A01; A07)
            return new PreferredSlotForbidden();
        }

        // ── Guard 2: booking status ─────────────────────────────────────────────────────────
        if (booking.Status != "Confirmed")
        {
            return new PreferredSlotBookingNotConfirmed();
        }

        // ── Guard 3: same-slot ──────────────────────────────────────────────────────────────
        if (slotId == booking.AppointmentSlotId)
        {
            return new PreferredSlotSameAsActive();
        }

        // ── Guard 4: slot availability ──────────────────────────────────────────────────────
        var slot = await _db.AppointmentSlots
            .FirstOrDefaultAsync(s => s.Id == slotId && s.IsAvailable, ct);

        if (slot is null)
        {
            return new PreferredSlotSlotNotAvailable();
        }

        // ── Upsert: delete existing row + insert new row ─────────────────────────────────────
        // ExecuteDeleteAsync is a bulk operation — it does not load the entity into memory (performance; AC-002)
        await _db.PreferredSlots
            .Where(ps => ps.BookingId == bookingId)
            .ExecuteDeleteAsync(ct);

        var preferred = new PreferredSlot
        {
            BookingId  = bookingId,
            SlotId     = slotId,
            CreatedAt  = DateTimeOffset.UtcNow,
        };

        _db.PreferredSlots.Add(preferred);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "PreferredSlotRegistered: bookingId={BookingId} slotId={SlotId} patientId={PatientId}",
            bookingId, slotId, patientId);

        return new PreferredSlotRegistered(slotId);
    }
}
