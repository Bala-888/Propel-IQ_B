namespace Api.Data.Entities;

/// <summary>
/// Stores a patient's preferred alternative appointment slot for a confirmed booking (us_024).
/// A UNIQUE index on <see cref="BookingId"/> enforces the at-most-one invariant at the database
/// level so concurrent upsert requests cannot create duplicate rows (AC-002; OWASP A04).
/// </summary>
public class PreferredSlot
{
    public int Id { get; set; }

    /// <summary>FK → bookings.id — UNIQUE; cascade-deleted when the booking row is hard-deleted.</summary>
    public int BookingId { get; set; }

    /// <summary>FK → appointment_slots.id — the patient's desired alternative slot.</summary>
    public int SlotId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Booking          Booking { get; set; } = null!;
    public AppointmentSlot  Slot    { get; set; } = null!;
}
