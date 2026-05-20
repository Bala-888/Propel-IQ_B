using System.Net;
using System.Net.Mail;

namespace Api.Services;

/// <summary>
/// <see cref="IEmailSender"/> implementation backed by <see cref="SmtpClient"/>.
/// All SMTP settings are read from environment variables at send time (OWASP A02 — no
/// credentials in source code or appsettings.json):
/// <list type="bullet">
///   <item><c>SMTP_HOST</c> — required; if absent the send is a no-op with a warning log.</item>
///   <item><c>SMTP_PORT</c> — default 587.</item>
///   <item><c>SMTP_FROM</c> — sender address; required when SMTP_HOST is set.</item>
///   <item><c>SMTP_USERNAME</c> — optional; if set, <c>SMTP_PASSWORD</c> is also expected.</item>
///   <item><c>SMTP_PASSWORD</c> — optional; paired with SMTP_USERNAME.</item>
/// </list>
/// </summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(ILogger<SmtpEmailSender> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task SendEmailAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        var host = Environment.GetEnvironmentVariable("SMTP_HOST");
        if (string.IsNullOrWhiteSpace(host))
        {
            // Dev environments without SMTP configured: log and skip — do not fail the request.
            _logger.LogWarning(
                "SMTP_HOST is not configured. Welcome email to {To} was not sent. " +
                "Set SMTP_HOST, SMTP_PORT, SMTP_FROM, SMTP_USERNAME, SMTP_PASSWORD to enable outbound email.",
                to);
            return;
        }

        var port      = int.TryParse(Environment.GetEnvironmentVariable("SMTP_PORT"), out var p) ? p : 587;
        var fromAddr  = Environment.GetEnvironmentVariable("SMTP_FROM") ?? host;
        var username  = Environment.GetEnvironmentVariable("SMTP_USERNAME");
        var password  = Environment.GetEnvironmentVariable("SMTP_PASSWORD");

        using var client = new SmtpClient(host, port)
        {
            EnableSsl        = true,
            DeliveryMethod   = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
        };

        if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
        {
            client.Credentials = new NetworkCredential(username, password);
        }

        using var message = new MailMessage(fromAddr, to, subject, body);

        try
        {
            await client.SendMailAsync(message, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Swallow send failures: account creation must not roll back due to email failures.
            // The user can be manually contacted and the temp password reset by an admin.
            _logger.LogError(ex, "Failed to send welcome email to {To}", to);
        }
    }
}
