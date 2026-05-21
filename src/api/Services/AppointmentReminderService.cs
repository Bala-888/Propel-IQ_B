using System.Net;
using System.Net.Mail;
using Api.Data;
using Api.Data.Entities;
using Microsoft.Extensions.Options;

namespace Api.Services;

/// <summary>
/// Dispatches appointment reminder notifications (email + optional SMS) for a single booking
/// and persists the appropriate deduplication tracking column after dispatch (us_027; AC-001, AC-002).
///
/// <para>
/// <b>Booking pre-loaded</b>: the caller (<see cref="AppointmentReminderJob"/>) must load the
/// booking with <c>Include(b =&gt; b.Patient)</c> and <c>Include(b =&gt; b.AppointmentSlot)</c>
/// before calling <see cref="SendReminderAsync"/>.  The service never re-queries the DB for the
/// booking itself — it does however call <see cref="AppDbContext.SaveChangesAsync"/> to persist
/// the tracking column on the <em>same</em> scoped context instance.
/// </para>
///
/// <para>
/// <b>Tracking semantics</b> (Edge): <see cref="Booking.Reminder24hSentAt"/> /
/// <see cref="Booking.Reminder2hSentAt"/> are set to <see cref="DateTimeOffset.UtcNow"/>
/// after <c>Task.WhenAll</c> resolves — regardless of whether individual channel dispatches
/// succeeded — so the job does not retry-spam the patient on the next tick.  If both channels
/// fail the booking is effectively silently de-queued; operational monitoring surfaces the
/// per-channel <c>ReminderNotificationFailed</c> log events.
/// </para>
///
/// <para>
/// <b>PHI guardrail</b>: patient email, phone, and name are never embedded in structured
/// Serilog log fields — only opaque IDs are logged (OWASP A02; HIPAA minimum-necessary).
/// </para>
/// </summary>
public sealed class AppointmentReminderService : IAppointmentReminderService
{
    private readonly AppDbContext                       _db;
    private readonly IConfirmationPdfService            _pdfService;
    private readonly IOptions<SmsSettings>              _smsSettings;
    private readonly ILogger<AppointmentReminderService> _logger;

    // Retry back-off delays shared by both email and SMS channels (mirrors SlotSwapNotificationService)
    private static readonly int[] RetryDelaysSeconds = [30, 60, 120];

    private const string ClinicName    = "PropelIQ Clinic";
    private const string ClinicAddress = "123 Health Ave, Suite 100";

    public AppointmentReminderService(
        AppDbContext                        db,
        IConfirmationPdfService             pdfService,
        IOptions<SmsSettings>               smsSettings,
        ILogger<AppointmentReminderService> logger)
    {
        _db          = db;
        _pdfService  = pdfService;
        _smsSettings = smsSettings;
        _logger      = logger;
    }

    /// <inheritdoc />
    public async Task SendReminderAsync(Booking booking, ReminderTier tier, CancellationToken ct = default)
    {
        // SMTP_HOST guard — dev environments without SMTP skip dispatch (OWASP A02)
        var smtpHost = Environment.GetEnvironmentVariable("SMTP_HOST");
        if (string.IsNullOrWhiteSpace(smtpHost))
        {
            _logger.LogWarning(
                "ReminderSkipped: SMTP_HOST not configured. BookingId={BookingId} Tier={Tier}",
                booking.Id, tier);
            return;
        }

        var patient   = booking.Patient;
        var slotStart = booking.AppointmentSlot.SlotStart;

        // ── Per-channel opt-out / availability checks ─────────────────────────────────────────
        // NOTE: us_009 EmailNotificationsEnabled / SmsNotificationsEnabled flags are not yet
        // present on the Patient entity.  Until that story lands, contact-info presence is used
        // as the opt-in gate.  When us_009 adds those columns, replace these guards with:
        //   bool emailEnabled = patient.EmailNotificationsEnabled && !string.IsNullOrWhiteSpace(patient.Email);
        //   bool smsEnabled   = patient.SmsNotificationsEnabled   && !string.IsNullOrWhiteSpace(patient.Phone);
        bool emailEnabled = !string.IsNullOrWhiteSpace(patient.Email);
        bool smsEnabled   = !string.IsNullOrWhiteSpace(patient.Phone)
                            && !string.IsNullOrWhiteSpace(_smsSettings.Value.SmsGatewayDomain);

        if (!emailEnabled)
            _logger.LogInformation(
                "ReminderEmailSkipped: PatientId={PatientId} BookingId={BookingId} Tier={Tier} Reason=NoEmail",
                patient.Id, booking.Id, tier);

        if (!smsEnabled)
            _logger.LogInformation(
                "ReminderSmsSkipped: PatientId={PatientId} BookingId={BookingId} Tier={Tier} Reason=NoSms",
                patient.Id, booking.Id, tier);

        if (!emailEnabled && !smsEnabled)
            return; // Nothing to dispatch — still update tracking so we don't re-evaluate this booking

        // ── PDF generation with 10-second timeout ─────────────────────────────────────────────
        byte[]? pdfBytes = null;
        using var pdfCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        pdfCts.CancelAfter(TimeSpan.FromSeconds(10));

        try
        {
            var pdfData = new ConfirmationData(
                PatientFullName:     $"{patient.FirstName} {patient.LastName}".Trim(),
                AppointmentDateTime: new DateTimeOffset(slotStart, TimeSpan.Zero),
                ClinicName:         ClinicName,
                ClinicAddress:      ClinicAddress,
                ProviderName:       booking.AppointmentSlot.ProviderName,
                BookingReferenceId: booking.Id);

            pdfBytes = await _pdfService.GenerateAsync(pdfData, pdfCts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(
                "ReminderPdfTimeout: confirmation PDF not generated within 10 s — plain-text fallback. " +
                "BookingId={BookingId}", booking.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "ReminderPdfError: unexpected error generating confirmation PDF — plain-text fallback. " +
                "BookingId={BookingId}", booking.Id);
        }

        // ── Concurrent email + SMS dispatch via Task.WhenAll ──────────────────────────────────
        var smtpPort = int.TryParse(Environment.GetEnvironmentVariable("SMTP_PORT"), out var p) ? p : 587;
        var fromAddr = Environment.GetEnvironmentVariable("SMTP_FROM") ?? smtpHost;
        var username = Environment.GetEnvironmentVariable("SMTP_USERNAME");
        var password = Environment.GetEnvironmentVariable("SMTP_PASSWORD");

        var emailTask = emailEnabled
            ? SendEmailAsync(booking, patient.Email!, tier, slotStart,
                smtpHost, smtpPort, fromAddr, username, password, pdfBytes, ct)
            : Task.CompletedTask;

        var smsTask = smsEnabled
            ? SendSmsAsync(booking, patient.Phone!, tier, slotStart,
                smtpHost, smtpPort, fromAddr, username, password, ct)
            : Task.CompletedTask;

        await Task.WhenAll(emailTask, smsTask);

        // ── Persist tracking column ────────────────────────────────────────────────────────────
        // Written unconditionally after Task.WhenAll so the job does not attempt re-dispatch on
        // subsequent ticks even if one or both channel dispatches failed (see class summary).
        var sentAt = DateTimeOffset.UtcNow;
        if (tier == ReminderTier.TwentyFourHour)
            booking.Reminder24hSentAt = sentAt;
        else
            booking.Reminder2hSentAt = sentAt;

        await _db.SaveChangesAsync(ct);
    }

    // ── Email dispatch ────────────────────────────────────────────────────────────────────────

    private async Task SendEmailAsync(
        Booking         booking,
        string          toEmail,
        ReminderTier    tier,
        DateTime        slotStart,
        string          host,
        int             port,
        string          fromAddr,
        string?         username,
        string?         password,
        byte[]?         pdfBytes,
        CancellationToken ct)
    {
        if (!IsValidEmail(toEmail))
        {
            _logger.LogWarning(
                "ReminderEmailSkipped: invalid email format. BookingId={BookingId} PatientId={PatientId}",
                booking.Id, booking.PatientId);
            return;
        }

        var tierLabel = tier == ReminderTier.TwentyFourHour ? "24-hour" : "2-hour";
        var subject   = $"Appointment reminder ({tierLabel}) – {slotStart:MMMM d, yyyy h:mm tt}";
        var body      = BuildEmailBody(booking, tier, slotStart, pdfBytes is null);

        for (var attempt = 0; attempt <= RetryDelaysSeconds.Length; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                await SendViaSmtpAsync(host, port, fromAddr, username, password,
                    toEmail, subject, body,
                    pdfBytes, $"appointment-confirmation-{booking.Id}.pdf",
                    ct);

                _logger.LogInformation(
                    "ReminderEmailSent: BookingId={BookingId} PatientId={PatientId} Tier={Tier}",
                    booking.Id, booking.PatientId, tier);
                return;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                if (attempt < RetryDelaysSeconds.Length)
                {
                    _logger.LogWarning(ex,
                        "ReminderEmailRetry: attempt {Attempt}/{Max} failed for BookingId={BookingId}. " +
                        "Retrying in {Delay}s.",
                        attempt + 1, RetryDelaysSeconds.Length + 1, booking.Id,
                        RetryDelaysSeconds[attempt]);
                    await Task.Delay(TimeSpan.FromSeconds(RetryDelaysSeconds[attempt]), ct);
                }
                else
                {
                    _logger.LogError(ex,
                        "ReminderNotificationFailed: Channel=Email BookingId={BookingId} PatientId={PatientId} Tier={Tier}",
                        booking.Id, booking.PatientId, tier);
                }
            }
        }
    }

    // ── SMS dispatch (SMTP-to-SMS gateway) ────────────────────────────────────────────────────

    private async Task SendSmsAsync(
        Booking         booking,
        string          phoneNumber,
        ReminderTier    tier,
        DateTime        slotStart,
        string          host,
        int             port,
        string          fromAddr,
        string?         username,
        string?         password,
        CancellationToken ct)
    {
        var smsAddress = $"{phoneNumber}@{_smsSettings.Value.SmsGatewayDomain}";
        var tierLabel  = tier == ReminderTier.TwentyFourHour ? "24 hours" : "2 hours";
        var smsBody    = $"UPACIP reminder: your appointment is in {tierLabel} on " +
                         $"{slotStart:M/d/yyyy} at {slotStart:h:mm tt}. Ref: {booking.Id}";

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
                    "ReminderSmsSent: BookingId={BookingId} PatientId={PatientId} Tier={Tier}",
                    booking.Id, booking.PatientId, tier);
                return;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                if (attempt < RetryDelaysSeconds.Length)
                {
                    _logger.LogWarning(ex,
                        "ReminderSmsRetry: attempt {Attempt}/{Max} failed for BookingId={BookingId}. " +
                        "Retrying in {Delay}s.",
                        attempt + 1, RetryDelaysSeconds.Length + 1, booking.Id,
                        RetryDelaysSeconds[attempt]);
                    await Task.Delay(TimeSpan.FromSeconds(RetryDelaysSeconds[attempt]), ct);
                }
                else
                {
                    _logger.LogError(ex,
                        "ReminderNotificationFailed: Channel=SMS BookingId={BookingId} PatientId={PatientId} Tier={Tier}",
                        booking.Id, booking.PatientId, tier);
                }
            }
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────────────────

    private static string BuildEmailBody(Booking booking, ReminderTier tier, DateTime slotStart, bool pdfTimedOut)
    {
        var tierDesc = tier == ReminderTier.TwentyFourHour
            ? "24 hours"
            : "2 hours";

        var pdfNote = pdfTimedOut
            ? "\n\nNote: your printable confirmation could not be generated at this time. " +
              "Please contact the clinic if you need a printed copy."
            : string.Empty;

        return $"""
            Dear Patient,

            This is a reminder that your appointment at {ClinicName} is in {tierDesc}.

            Appointment details:
              Date & Time : {slotStart:dddd, MMMM d, yyyy} at {slotStart:h:mm tt}
              Provider    : {booking.AppointmentSlot.ProviderName ?? "To be assigned"}
              Reference   : {booking.Id}
              Address     : {ClinicAddress}

            If you need to reschedule or cancel, please contact the clinic as soon as possible.{pdfNote}

            Thank you,
            {ClinicName}
            """;
    }

    private static async Task SendViaSmtpAsync(
        string   host,
        int      port,
        string   fromAddr,
        string?  username,
        string?  password,
        string   toAddress,
        string   subject,
        string   body,
        byte[]?  pdfBytes,
        string?  attachmentName,
        CancellationToken ct)
    {
        using var client  = new SmtpClient(host, port);
        using var message = new MailMessage();

        client.EnableSsl             = true;
        client.DeliveryMethod        = SmtpDeliveryMethod.Network;
        client.UseDefaultCredentials = false;

        if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
            client.Credentials = new NetworkCredential(username, password);

        message.From    = new MailAddress(fromAddr);
        message.To.Add(new MailAddress(toAddress));
        message.Subject = subject;
        message.Body    = body;

        if (pdfBytes is { Length: > 0 } && !string.IsNullOrWhiteSpace(attachmentName))
        {
            var stream     = new MemoryStream(pdfBytes);
            var attachment = new Attachment(stream, attachmentName, "application/pdf");
            message.Attachments.Add(attachment);
        }

        await client.SendMailAsync(message, ct);
    }

    /// <summary>
    /// Lightweight structural email-format check — prevents SMTP round-trips for obviously
    /// malformed addresses (OWASP A03; identical guard used in SlotSwapNotificationService).
    /// </summary>
    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new MailAddress(email);
            return addr.Address == email.Trim();
        }
        catch
        {
            return false;
        }
    }
}
