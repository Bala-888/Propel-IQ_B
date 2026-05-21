namespace Api.Audit;

/// <summary>
/// Immutable value object carrying all fields required for a single audit log entry.
/// No PHI is included — only structural IDs (OWASP A09; HIPAA minimum-necessary principle).
/// <see cref="OccurredAt"/> must be set to <c>DateTime.UtcNow</c> at the call site to
/// capture the moment of the action, not the moment the DB processes the insert (AC-001).
/// </summary>
public sealed record AuditEntry(
    string ActorId,
    string ActorRole,
    string ActionType,
    string ResourceType,
    string ResourceId,
    string IpAddress,
    string UserAgent,
    DateTime OccurredAt);
