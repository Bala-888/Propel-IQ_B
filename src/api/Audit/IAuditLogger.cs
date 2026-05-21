namespace Api.Audit;

/// <summary>
/// Persistence-capable audit logger.
/// Implementations must write a durable record to PostgreSQL (AC-001) AND emit
/// a Serilog structured event to Seq (AC-003) on every successful call.
/// On DB failure, implementations must throw <see cref="Exceptions.AuditLogUnavailableException"/>
/// so <c>AuditMiddleware</c> can return HTTP 503 before the protected action executes (AC-002).
/// </summary>
public interface IAuditLogger
{
    /// <summary>
    /// Persists <paramref name="entry"/> to the audit store and emits a structured log event.
    /// Throws <see cref="Exceptions.AuditLogUnavailableException"/> if the store is unavailable.
    /// </summary>
    Task RecordAsync(AuditEntry entry, CancellationToken ct = default);
}
