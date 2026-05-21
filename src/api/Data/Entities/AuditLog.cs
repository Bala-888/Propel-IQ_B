namespace Api.Data.Entities;

/// <summary>
/// Append-only audit log record persisted by <c>PostgresAuditLogger</c>.
/// The BIGSERIAL primary key (migrated by task_002 of us_014) makes concurrent INSERTs safe
/// with no sequence contention (Edge: concurrent writes; AC-001).
/// No PHI fields — only structural IDs (OWASP A09; HIPAA minimum-necessary principle).
/// </summary>
public class AuditLog
{
    /// <summary>
    /// Auto-generated surrogate key. task_002 (us_014) migrates this to BIGSERIAL / bigint.
    /// </summary>
    public long Id { get; set; }

    /// <summary>JWT sub claim of the acting user, or <c>"system"</c> for background jobs.</summary>
    public string? ActorId { get; set; }

    /// <summary>JWT role claim of the acting user, or <c>"System"</c> for background jobs.</summary>
    public string? ActorRole { get; set; }

    /// <summary>One of the <see cref="Audit.AuditActionTypes"/> constants (AC-005).</summary>
    public string ActionType { get; set; } = string.Empty;

    /// <summary>Controller / domain name of the accessed resource.</summary>
    public string? ResourceType { get; set; }

    /// <summary>Route-level identifier of the specific resource instance.</summary>
    public string? ResourceId { get; set; }

    /// <summary>Client IP address (post-forwarded-headers resolution).</summary>
    public string? IpAddress { get; set; }

    /// <summary>HTTP User-Agent header value. task_002 (us_014) migrates this column.</summary>
    public string? UserAgent { get; set; }

    /// <summary>UTC timestamp set by the application at the moment of the action (AC-001).</summary>
    public DateTime OccurredAt { get; set; }
}
