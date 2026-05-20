namespace Api.Services;

/// <summary>
/// Abstraction over the outbound email transport.
/// The concrete implementation reads SMTP settings from environment variables only —
/// no credentials are committed to source code (AC-001; OWASP A02).
/// </summary>
public interface IEmailSender
{
    /// <summary>
    /// Sends a plain-text email. Fire-and-forget failures are logged as warnings;
    /// the caller is not expected to retry.
    /// </summary>
    Task SendEmailAsync(string to, string subject, string body, CancellationToken ct = default);
}
