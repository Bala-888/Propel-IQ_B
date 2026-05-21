namespace Api.Data.Entities;

/// <summary>
/// Stores encrypted OAuth access and refresh tokens for a patient's connected calendar provider
/// (us_028; AC-001, AC-002; OWASP A02).
///
/// <para>
/// <b>Encryption</b>: <see cref="EncryptedAccessToken"/> and <see cref="EncryptedRefreshToken"/>
/// are stored as AES-256 (pgcrypto-compatible) ciphertext.  <c>CalendarSyncService.ResolveTokenAsync</c>
/// calls <c>IPhiEncryptionService.Decrypt()</c> immediately before the API call and
/// <c>IPhiEncryptionService.Encrypt()</c> after a successful refresh — plaintext tokens are
/// never written to Serilog or response bodies (OWASP A02; HIPAA minimum-necessary).
/// </para>
///
/// <para>
/// <b>UNIQUE constraint</b>: <c>(PatientId, Provider)</c> is unique — a patient may have at most
/// one OAuth token row per provider (AppDbContext + migration enforce this).
/// </para>
/// </summary>
public class PatientCalendarToken
{
    public int    Id         { get; set; }
    public int    PatientId  { get; set; }

    /// <summary>"Google" or "Outlook".</summary>
    public string Provider   { get; set; } = string.Empty;

    /// <summary>AES-256 encrypted OAuth access token (bytea in Postgres).</summary>
    public byte[]? EncryptedAccessToken  { get; set; }

    /// <summary>AES-256 encrypted OAuth refresh token (bytea in Postgres).</summary>
    public byte[]? EncryptedRefreshToken { get; set; }

    /// <summary>UTC expiry of the current access token; used to detect when a refresh is needed.</summary>
    public DateTimeOffset TokenExpiry { get; set; }

    public Patient Patient { get; set; } = null!;
}
