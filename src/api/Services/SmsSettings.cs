namespace Api.Services;

/// <summary>
/// Configuration for SMTP-to-SMS gateway dispatch (us_026; AC-002).
///
/// Bind from <c>appsettings.json</c> section <c>"Sms"</c>:
/// <code>
/// "Sms": { "SmsGatewayDomain": "txt.att.net" }
/// </code>
///
/// The gateway domain is the only non-secret SMS setting; all SMTP credentials (host, port,
/// username, password) are shared with the email channel and sourced from the same
/// <c>SMTP_*</c> environment variables (OWASP A02 — no credentials in source code).
/// </summary>
public sealed class SmsSettings
{
    /// <summary>
    /// SMTP-to-SMS carrier gateway domain (e.g. <c>txt.att.net</c>, <c>tmomail.net</c>).
    /// SMS is constructed as <c>{PhoneNumber}@{SmsGatewayDomain}</c> and sent via SMTP.
    /// When empty or not configured, SMS dispatch is skipped (AC-002).
    /// </summary>
    public string SmsGatewayDomain { get; set; } = string.Empty;
}
