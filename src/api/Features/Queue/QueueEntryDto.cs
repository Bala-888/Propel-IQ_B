namespace Api.Features.Queue;

/// <summary>
/// Immutable projection returned by <c>GET /api/queue</c> for each booking in today's queue (us_031/AC-001).
/// </summary>
/// <param name="Position">1-based ordinal position in the queue, ordered by <c>BookedAt</c> ascending.</param>
/// <param name="PatientName">Full name concatenated from <c>Patient.FirstName</c> and <c>Patient.LastName</c>.</param>
/// <param name="ArrivalTime">
///     UTC timestamp of check-in (<c>Booking.CheckedInAt</c>).
///     <c>null</c> until staff marks the patient arrived — the frontend renders "—" for wait time when null (AC-003 contract).
/// </param>
/// <param name="AppointmentTime">Scheduled slot start time (<c>AppointmentSlot.SlotStart</c>).</param>
/// <param name="NoShowRiskTier">
///     One of <c>"High"</c>, <c>"Medium"</c>, <c>"Low"</c>, or <c>"Unknown"</c> — never null.
///     Normalised from <c>Booking.NoShowRiskTier</c> with <c>?? "Unknown"</c> guard (Edge: missing risk score).
/// </param>
/// <param name="Status">Current booking status string (e.g. <c>"Confirmed"</c>, <c>"CheckedIn"</c>).</param>
public sealed record QueueEntryDto(
    int             Id,
    int             Position,
    string          PatientName,
    DateTimeOffset? ArrivalTime,
    DateTimeOffset  AppointmentTime,
    string          NoShowRiskTier,
    string          Status);
