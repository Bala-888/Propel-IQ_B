using System.Net;
using System.Net.Mail;

namespace Api.Services;

/// <summary>
/// Sends appointment confirmation emails with a PDF attachment (us_022).
///
/// <para>
/// SMTP credentials are read from environment variables at send time (OWASP A02):
/// <c>SMTP_HOST</c>, <c>SMTP_PORT</c> (default 587), <c>SMTP_FROM</c>,
/// <c>SMTP_USERNAME</c>, <c>SMTP_PASSWORD</c>.
/// If <c>SMTP_HOST</c> is absent the send is skipped with a warning log (dev environments).
/// </para>
///
/// <para>
/// PDF generation is attempted with a 10-second timeout.  On timeout the email is sent
/// as plain text only; the attachment is omitted and the event is logged (AC-003).
/// SMTP delivery is retried up to 3 times with back-off delays of 30 s / 60 s / 120 s
/// before a final <c>ConfirmationEmailFailed</c> log entry is emitted (AC-003).
/// </para>
/// </summary>
public sealed class ConfirmationEmailService : IConfirmationEmailService
{
    private readonly IConfirmationPdfService            _pdfService;
    private readonly ILogger<ConfirmationEmailService>  _logger;

    // Retry back-off delays in seconds
    private static readonly int[] RetryDelaysSeconds = [30, 60, 120];

    public ConfirmationEmailService(
        IConfirmationPdfService           pdfService,
        ILogger<ConfirmationEmailService> logger)
    {
        _pdfService = pdfService;
        _logger     = logger;
    }

    /// <inheritdoc />
    public async Task SendAsync(BookingConfirmedEvent evt, CancellationToken ct = default)
    {
        // SMTP_HOST guard — dev environments without SMTP skip silently (OWASP A02)
        var host = Environment.GetEnvironmentVariable("SMTP_HOST");
        if (string.IsNullOrWhiteSpace(host))
        {
            _logger.LogWarning(
                "ConfirmationEmailSkipped: SMTP_HOST not configured. " +
                "BookingId={BookingId} PatientId={PatientId}",
                evt.BookingId, evt.PatientId);
            return;
        }

        // RFC 5322 email validation — reject malformed addresses before SMTP round-trip (OWASP A03)
        if (!IsValidEmail(evt.PatientEmail))
        {
            _logger.LogWarning(
                "ConfirmationEmailSkipped: invalid email address format. " +
                "BookingId={BookingId} PatientId={PatientId}",
                evt.BookingId, evt.PatientId);
            return;
        }

        var port     = int.TryParse(Environment.GetEnvironmentVariable("SMTP_PORT"), out var p) ? p : 587;
        var fromAddr = Environment.GetEnvironmentVariable("SMTP_FROM") ?? host;
        var username = Environment.GetEnvironmentVariable("SMTP_USERNAME");
        var password = Environment.GetEnvironmentVariable("SMTP_PASSWORD");

        // ── PDF generation with 10-second timeout ─────────────────────────────────────────────
        byte[]? pdfBytes = null;
        using var pdfCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        pdfCts.CancelAfter(TimeSpan.FromSeconds(10));

        try
        {
            var data = new ConfirmationData(
                PatientFullName:     evt.PatientFullName,
                AppointmentDateTime: evt.AppointmentDateTime,
                ClinicName:          evt.ClinicName,
                ClinicAddress:       evt.ClinicAddress,
                ProviderName:        evt.ProviderName,
                BookingReferenceId:  evt.BookingId);

            pdfBytes = await _pdfService.GenerateAsync(data, pdfCts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // PDF generation timed out — send plain-text email without attachment (AC-003)
            _logger.LogWarning(
                "PdfGenerationTimeout: confirmation PDF could not be generated within 10 s. " +
                "BookingId={BookingId} — sending plain-text fallback.",
                evt.BookingId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "PdfGenerationError: unexpected error generating confirmation PDF. " +
                "BookingId={BookingId} — sending plain-text fallback.",
                evt.BookingId);
        }

        // ── SMTP send with retry ───────────────────────────────────────────────────────────────
        var subject = $"Appointment Confirmed – {evt.AppointmentDateTime:MMMM d, yyyy}";
        var body    = BuildEmailBody(evt, pdfBytes is null);

        for (var attempt = 0; attempt <= RetryDelaysSeconds.Length; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                await SendViaSmtpAsync(host, port, fromAddr, username, password,
                    evt.PatientEmail, subject, body, pdfBytes, evt.BookingId, ct);

                _logger.LogInformation(
                    "ConfirmationEmailSent: BookingId={BookingId} PatientId={PatientId}",
                    evt.BookingId, evt.PatientId);
                return; // success
            }
            catch (OperationCanceledException)
            {
                throw; // propagate shutdown/cancellation
            }
            catch (Exception ex)
            {
                if (attempt < RetryDelaysSeconds.Length)
                {
                    _logger.LogWarning(ex,
                        "ConfirmationEmailRetry: attempt {Attempt}/{Max} failed for BookingId={BookingId}. " +
                        "Retrying in {Delay}s.",
                        attempt + 1, RetryDelaysSeconds.Length + 1, evt.BookingId,
                        RetryDelaysSeconds[attempt]);
                    await Task.Delay(TimeSpan.FromSeconds(RetryDelaysSeconds[attempt]), ct);
                }
                else
                {
                    _logger.LogError(ex,
                        "ConfirmationEmailFailed: all {Max} attempts exhausted for BookingId={BookingId} PatientId={PatientId}.",
                        RetryDelaysSeconds.Length + 1, evt.BookingId, evt.PatientId);
                }
            }
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────────────────

    private static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        try
        {
            _ = new MailAddress(email);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string BuildEmailBody(BookingConfirmedEvent evt, bool pdfMissing)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Dear {evt.PatientFullName},");
        sb.AppendLine();
        sb.AppendLine("Your appointment has been confirmed. Details below:");
        sb.AppendLine();
        sb.AppendLine($"  Date & Time : {evt.AppointmentDateTime:dddd, MMMM d, yyyy} at {evt.AppointmentDateTime:h:mm tt}");
        sb.AppendLine($"  Clinic      : {evt.ClinicName}");
        sb.AppendLine($"  Address     : {evt.ClinicAddress}");
        if (!string.IsNullOrWhiteSpace(evt.ProviderName))
            sb.AppendLine($"  Provider    : {evt.ProviderName}");
        sb.AppendLine($"  Reference # : {evt.BookingId}");
        sb.AppendLine();
        if (pdfMissing)
            sb.AppendLine("(A PDF confirmation could not be attached at this time. Please save this email for your records.)");
        else
            sb.AppendLine("Please find your confirmation PDF attached.");
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
        int      bookingId,
        CancellationToken ct)
    {
        using var client = new SmtpClient(host, port)
        {
            EnableSsl             = true,
            DeliveryMethod        = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
        };

        if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
            client.Credentials = new NetworkCredential(username, password);

        using var message = new MailMessage(fromAddr, toAddr, subject, body);

        if (pdfBytes is { Length: > 0 })
        {
            // MemoryStream ownership transfers to Attachment; Attachment disposes it with the message
            var stream     = new MemoryStream(pdfBytes);
            var attachment = new Attachment(stream, $"confirmation-{bookingId}.pdf", "application/pdf");
            message.Attachments.Add(attachment);
        }

        await client.SendMailAsync(message, ct);
    }
}
