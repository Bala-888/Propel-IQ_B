using System.Net;
using System.Net.Mail;
using Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Services;

/// <summary>
/// Sends slot-swap update notifications (email + optional SMS) for a committed swap (us_026; AC-001–AC-004).
///
/// <para>
/// <b>Payload freshness</b> (Edge): all notification content is loaded from the DB using
/// <see cref="SlotSwapCompletedEvent.NewBookingId"/> — event fields are never used to compose
/// subject, body, or SMS text (AC-001; checklist).
/// </para>
///
/// <para>
/// <b>Concurrency</b>: email and SMS tasks are created independently and awaited together
/// via <c>Task.WhenAll</c> — neither channel blocks the other (AC-002; checklist).
/// </para>
///
/// <para>
/// <b>PHI guardrail</b>: patient phone number, email address, and name are loaded from the
/// DB at runtime and never appear in Serilog structured log fields — only opaque IDs are logged
/// (OWASP A02; HIPAA minimum-necessary).
/// </para>
/// </summary>
public sealed class SlotSwapNotificationService : ISlotSwapNotificationService
{
    private readonly AppDbContext                          _db;
    private readonly IConfirmationPdfService               _pdfService;
    private readonly IOptions<SmsSettings>                 _smsSettings;
    private readonly ILogger<SlotSwapNotificationService>  _logger;

    // Retry back-off delays shared by both email and SMS channels (AC-001; AC-002)
    private static readonly int[] RetryDelaysSeconds = [30, 60, 120];

    // Hardcoded clinic constants — can be promoted to IOptions<ClinicSettings> when needed
    private const string ClinicName    = "PropelIQ Clinic";
    private const string ClinicAddress = "123 Health Ave, Suite 100";

    public SlotSwapNotificationService(
        AppDbContext                         db,
        IConfirmationPdfService              pdfService,
        IOptions<SmsSettings>                smsSettings,
        ILogger<SlotSwapNotificationService> logger)
    {
        _db          = db;
        _pdfService  = pdfService;
        _smsSettings = smsSettings;
        _logger      = logger;
    }

    /// <inheritdoc />
    public async Task SendAsync(SlotSwapCompletedEvent evt, CancellationToken ct = default)
    {
        // SMTP_HOST guard — dev environments without SMTP skip the entire notification (OWASP A02)
        var smtpHost = Environment.GetEnvironmentVariable("SMTP_HOST");
        if (string.IsNullOrWhiteSpace(smtpHost))
        {
            _logger.LogWarning(
                "SlotSwapNotificationSkipped: SMTP_HOST not configured. NewBookingId={NewBookingId}",
                evt.NewBookingId);
            return;
        }

        // ── Load fresh booking data from DB (Edge: payload freshness; AC-001) ──────────────────
        // All notification content is derived from this record — the event fields are routing-only.
        var booking = await _db.Bookings
            .Include(b => b.AppointmentSlot)
            .Include(b => b.Patient)
            .FirstOrDefaultAsync(b => b.Id == evt.NewBookingId && b.Status == "Confirmed", ct);

        if (booking is null)
        {
            // Guard: swap may have been rolled back before the event was processed (race condition)
            _logger.LogWarning(
                "SlotSwapNewBookingNotFound: NewBookingId={NewBookingId} — notification skipped",
                evt.NewBookingId);
            return;
        }

        var patient = booking.Patient;

        // ── Per-channel opt-out checks (AC-004) ───────────────────────────────────────────────
        // NOTE: us_009 EmailNotificationsEnabled / SmsNotificationsEnabled flags are not yet on
        // the Patient entity. Until that story is completed, contact-info presence is used as
        // the availability gate. When us_009 adds the boolean columns, replace these guards with:
        //   bool emailEnabled = patient.EmailNotificationsEnabled && !string.IsNullOrWhiteSpace(patient.Email);
        //   bool smsEnabled   = patient.SmsNotificationsEnabled   && !string.IsNullOrWhiteSpace(patient.Phone);
        bool emailEnabled = !string.IsNullOrWhiteSpace(patient.Email);
        bool smsEnabled   = !string.IsNullOrWhiteSpace(patient.Phone)
                            && !string.IsNullOrWhiteSpace(_smsSettings.Value.SmsGatewayDomain);

        if (!emailEnabled)
        {
            _logger.LogInformation(
                "EmailSkipped: PatientId={PatientId} Reason=OptedOut NewBookingId={NewBookingId}",
                patient.Id, evt.NewBookingId);
        }

        if (!smsEnabled)
        {
            _logger.LogInformation(
                "SmsSkipped: PatientId={PatientId} Reason=OptedOut NewBookingId={NewBookingId}",
                patient.Id, evt.NewBookingId);
        }

        if (!emailEnabled && !smsEnabled)
        {
            return; // Both channels skipped — nothing to dispatch
        }

        // ── PDF generation with 10-second timeout (AC-003; us_022 Edge pattern reuse) ─────────
        byte[]? pdfBytes = null;
        using var pdfCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        pdfCts.CancelAfter(TimeSpan.FromSeconds(10));

        try
        {
            var slotStart  = booking.AppointmentSlot.SlotStart;
            var pdfData = new ConfirmationData(
                PatientFullName:    $"{patient.FirstName} {patient.LastName}".Trim(),
                AppointmentDateTime: new DateTimeOffset(slotStart, TimeSpan.Zero),
                ClinicName:         ClinicName,
                ClinicAddress:      ClinicAddress,
                ProviderName:       booking.AppointmentSlot.ProviderName,
                BookingReferenceId: booking.Id);

            pdfBytes = await _pdfService.GenerateAsync(pdfData, pdfCts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // PDF timed out — email falls back to plain-text body without attachment (AC-003)
            _logger.LogWarning(
                "PdfGenerationTimeout: updated confirmation PDF could not be generated within 10 s. " +
                "NewBookingId={NewBookingId} — sending plain-text fallback.",
                evt.NewBookingId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "PdfGenerationError: unexpected error generating updated confirmation PDF. " +
                "NewBookingId={NewBookingId} — sending plain-text fallback.",
                evt.NewBookingId);
        }

        // ── Concurrent email + SMS dispatch via Task.WhenAll (AC-002) ────────────────────────
        // Both tasks are constructed before either is awaited — they start simultaneously.
        // A skipped channel is represented as Task.CompletedTask so WhenAll resolves normally.
        var smtpPort = int.TryParse(Environment.GetEnvironmentVariable("SMTP_PORT"), out var p) ? p : 587;
        var fromAddr = Environment.GetEnvironmentVariable("SMTP_FROM") ?? smtpHost;
        var username = Environment.GetEnvironmentVariable("SMTP_USERNAME");
        var password = Environment.GetEnvironmentVariable("SMTP_PASSWORD");

        var emailTask = emailEnabled
            ? SendEmailAsync(booking, patient.Email!, smtpHost, smtpPort, fromAddr, username, password, pdfBytes, evt, ct)
            : Task.CompletedTask;

        var smsTask = smsEnabled
            ? SendSmsAsync(booking, patient.Phone!, smtpHost, smtpPort, fromAddr, username, password, evt, ct)
            : Task.CompletedTask;

        await Task.WhenAll(emailTask, smsTask);
    }

    // ── Email dispatch ────────────────────────────────────────────────────────────────────────

    private async Task SendEmailAsync(
        Api.Data.Entities.Booking booking,
        string   toEmail,
        string   host,
        int      port,
        string   fromAddr,
        string?  username,
        string?  password,
        byte[]?  pdfBytes,
        SlotSwapCompletedEvent evt,
        CancellationToken ct)
    {
        // RFC 5322 email validation — reject malformed addresses before SMTP round-trip (OWASP A03)
        if (!IsValidEmail(toEmail))
        {
            _logger.LogWarning(
                "EmailSkipped: invalid email format. NewBookingId={NewBookingId} PatientId={PatientId}",
                evt.NewBookingId, booking.PatientId);
            return;
        }

        var slotStart = booking.AppointmentSlot.SlotStart;
        var subject   = $"Your appointment has been updated – {slotStart:MMMM d, yyyy}";
        var body      = BuildEmailBody(booking, slotStart, pdfBytes is null);

        for (var attempt = 0; attempt <= RetryDelaysSeconds.Length; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                await SendViaSmtpAsync(host, port, fromAddr, username, password,
                    toEmail, subject, body,
                    pdfBytes, $"updated-confirmation-{booking.Id}.pdf",
                    ct);

                _logger.LogInformation(
                    "SlotSwapEmailSent: NewBookingId={NewBookingId} PatientId={PatientId}",
                    evt.NewBookingId, booking.PatientId);
                return; // success
            }
            catch (OperationCanceledException)
            {
                throw; // propagate graceful shutdown
            }
            catch (Exception ex)
            {
                if (attempt < RetryDelaysSeconds.Length)
                {
                    _logger.LogWarning(ex,
                        "SlotSwapEmailRetry: attempt {Attempt}/{Max} failed for NewBookingId={NewBookingId}. " +
                        "Retrying in {Delay}s.",
                        attempt + 1, RetryDelaysSeconds.Length + 1, evt.NewBookingId,
                        RetryDelaysSeconds[attempt]);
                    await Task.Delay(TimeSpan.FromSeconds(RetryDelaysSeconds[attempt]), ct);
                }
                else
                {
                    // All retries exhausted — log and return without modifying booking state (Edge)
                    _logger.LogError(ex,
                        "SlotSwapNotificationFailed: Channel=Email NewBookingId={NewBookingId} PatientId={PatientId}",
                        evt.NewBookingId, booking.PatientId);
                }
            }
        }
    }

    // ── SMS dispatch (SMTP-to-SMS gateway; AC-002) ────────────────────────────────────────────

    private async Task SendSmsAsync(
        Api.Data.Entities.Booking booking,
        string   phoneNumber,
        string   host,
        int      port,
        string   fromAddr,
        string?  username,
        string?  password,
        SlotSwapCompletedEvent evt,
        CancellationToken ct)
    {
        // Construct SMTP-to-SMS gateway address: {phone}@{gateway-domain}
        var smsAddress = $"{phoneNumber}@{_smsSettings.Value.SmsGatewayDomain}";

        var slotStart = booking.AppointmentSlot.SlotStart;
        var smsBody   = $"Your UPACIP appointment has been moved to " +
                        $"{slotStart:M/d/yyyy} {slotStart:h:mm tt}. Ref: {booking.Id}";

        for (var attempt = 0; attempt <= RetryDelaysSeconds.Length; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                await SendViaSmtpAsync(host, port, fromAddr, username, password,
                    smsAddress, subject: string.Empty, body: smsBody,
                    pdfBytes: null, attachmentName: null,
                    ct);

                _logger.LogInformation(
                    "SlotSwapSmsSent: NewBookingId={NewBookingId} PatientId={PatientId}",
                    evt.NewBookingId, booking.PatientId);
                return; // success
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (attempt < RetryDelaysSeconds.Length)
                {
                    _logger.LogWarning(ex,
                        "SlotSwapSmsRetry: attempt {Attempt}/{Max} failed for NewBookingId={NewBookingId}. " +
                        "Retrying in {Delay}s.",
                        attempt + 1, RetryDelaysSeconds.Length + 1, evt.NewBookingId,
                        RetryDelaysSeconds[attempt]);
                    await Task.Delay(TimeSpan.FromSeconds(RetryDelaysSeconds[attempt]), ct);
                }
                else
                {
                    // All retries exhausted — booking state is never touched on notification failure (Edge)
                    _logger.LogError(ex,
                        "SlotSwapNotificationFailed: Channel=SMS NewBookingId={NewBookingId} PatientId={PatientId}",
                        evt.NewBookingId, booking.PatientId);
                }
            }
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────────────────

    private static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        try   { _ = new MailAddress(email); return true; }
        catch (FormatException) { return false; }
    }

    private static string BuildEmailBody(
        Api.Data.Entities.Booking booking,
        DateTime slotStart,
        bool pdfMissing)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Dear {booking.Patient.FirstName},");
        sb.AppendLine();
        sb.AppendLine("Good news — your appointment has been moved to an earlier slot. Updated details:");
        sb.AppendLine();
        sb.AppendLine($"  Date & Time : {slotStart:dddd, MMMM d, yyyy} at {slotStart:h:mm tt}");
        sb.AppendLine($"  Clinic      : {ClinicName}");
        sb.AppendLine($"  Address     : {ClinicAddress}");
        if (!string.IsNullOrWhiteSpace(booking.AppointmentSlot.ProviderName))
            sb.AppendLine($"  Provider    : {booking.AppointmentSlot.ProviderName}");
        sb.AppendLine($"  Reference # : {booking.Id}");
        sb.AppendLine();
        if (pdfMissing)
            sb.AppendLine("(Your updated confirmation document is being generated and will follow shortly.)");
        else
            sb.AppendLine("Please find your updated confirmation PDF attached.");
        sb.AppendLine();
        sb.AppendLine("Thank you for choosing our clinic.");
        return sb.ToString();
    }

    private static async Task SendViaSmtpAsync(
        string   host,
        int      port,
        string   fromAddr,
        string?  username,
        string?  password,
        string   toAddr,
        string   subject,
        string   body,
        byte[]?  pdfBytes,
        string?  attachmentName,
        CancellationToken ct)
    {
        using var client = new SmtpClient(host, port)
        {
            EnableSsl             = true,
            DeliveryMethod        = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Credentials           = !string.IsNullOrWhiteSpace(username)
                                    ? new NetworkCredential(username, password)
                                    : null,
        };

        using var message = new MailMessage(fromAddr, toAddr, subject, body);

        if (pdfBytes is not null && !string.IsNullOrWhiteSpace(attachmentName))
        {
            var stream     = new MemoryStream(pdfBytes);
            var attachment = new Attachment(stream, attachmentName, "application/pdf");
            message.Attachments.Add(attachment);
        }

        await client.SendMailAsync(message, ct);
    }
}
