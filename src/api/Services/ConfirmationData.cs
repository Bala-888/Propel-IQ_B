namespace Api.Services;

/// <summary>
/// Scheduling and identity data passed to <see cref="IConfirmationPdfService.GenerateAsync"/>
/// to render the AC-002 mandatory fields in the appointment confirmation PDF (us_022; AC-002).
///
/// <para>
/// Contains only scheduling and identity fields — no medical history, diagnosis, medication,
/// or intake data is included so the PDF document adheres to minimum-necessary data principles
/// (OWASP A02; HIPAA minimum-necessary).
/// </para>
/// </summary>
/// <param name="PatientFullName">Full name of the patient — used on the confirmation PDF.</param>
/// <param name="AppointmentDateTime">Date and time of the appointment (offset-aware).</param>
/// <param name="ClinicName">Name of the clinic displayed in the document header.</param>
/// <param name="ClinicAddress">Physical address of the clinic (AC-002 mandatory field).</param>
/// <param name="ProviderName">Assigned provider name, or <c>null</c> if not yet assigned (AC-002 — rendered as "To be assigned").</param>
/// <param name="BookingReferenceId">Integer booking PK — displayed as text and encoded in the QR code (AC-002).</param>
public sealed record ConfirmationData(
    string          PatientFullName,
    DateTimeOffset  AppointmentDateTime,
    string          ClinicName,
    string          ClinicAddress,
    string?         ProviderName,
    int             BookingReferenceId);
