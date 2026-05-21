namespace Api.Services;

/// <summary>
/// Raised after a booking is committed to signal the confirmation email pipeline (us_022).
/// All fields are populated at event-creation time so the background worker never performs
/// PHI queries inside the fire-and-forget path (OWASP A01; AC-003).
/// </summary>
public sealed record BookingConfirmedEvent(
    int             BookingId,
    int             PatientId,
    string          PatientEmail,
    string          PatientFullName,
    DateTimeOffset  AppointmentDateTime,
    string          ClinicName,
    string          ClinicAddress,
    string?         ProviderName);
