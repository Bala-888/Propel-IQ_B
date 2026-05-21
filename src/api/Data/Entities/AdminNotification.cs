namespace Api.Data.Entities;

/// <summary>
/// Represents an admin alert raised by automated monitoring (e.g. repeated 403 events from
/// a single source IP). Rows are inserted by <c>RepeatedUnauthorizedAccessTracker</c> when
/// the per-IP 403 count reaches the configured threshold (AC-005).
/// The table is append-only — no UPDATE or DELETE operations are exposed (OWASP A09).
/// </summary>
public class AdminNotification
{
    /// <summary>Surrogate PK — UUID generated application-side before insert.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Type of alert, e.g. <c>"RepeatedUnauthorizedAccess"</c>. Max 64 chars (AC-005).
    /// </summary>
    public string AlertType { get; set; } = string.Empty;

    /// <summary>
    /// Source IP address that triggered the threshold.
    /// Max 45 chars — accommodates full IPv6 and IPv4-mapped IPv6 addresses (OWASP A05).
    /// </summary>
    public string SourceIp { get; set; } = string.Empty;

    /// <summary>Optional: ID of the actor whose requests triggered the alert.</summary>
    public Guid? ActorId { get; set; }

    /// <summary>Optional: role claim of the actor at the time of the alert.</summary>
    public string? ActorRole { get; set; }

    /// <summary>
    /// Set by the DB server via <c>DEFAULT now()</c> — not writable by the application
    /// to prevent clock-skew manipulation (AC-005; OWASP A09).
    /// </summary>
    public DateTime CreatedAt { get; set; }
}
