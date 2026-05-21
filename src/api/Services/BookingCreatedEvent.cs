namespace Api.Services;

/// <summary>
/// Minimal domain event published after a booking is committed to the database (us_021; AC-003).
///
/// Contains only scheduling metadata — no PHI fields (patient name, demographics, intake data)
/// are included in the event record (OWASP A02; HIPAA minimum-necessary).
/// </summary>
/// <param name="BookingId">Integer PK of the newly created <c>bookings</c> row.</param>
/// <param name="PatientId">Integer PK of the patient who made the booking.</param>
/// <param name="SlotDate">Calendar date of the appointment.</param>
/// <param name="SlotStartTime">Start time of the appointment slot (local to the slot's timezone).</param>
/// <param name="BookingCreatedAt">UTC timestamp when the booking was committed.</param>
public sealed record BookingCreatedEvent(
    int      BookingId,
    int      PatientId,
    DateOnly SlotDate,
    TimeOnly SlotStartTime,
    DateTime BookingCreatedAt);
