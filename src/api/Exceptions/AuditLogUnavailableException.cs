namespace Api.Exceptions;

/// <summary>
/// Thrown by <c>PostgresAuditLogger</c> when the PostgreSQL audit store is unavailable
/// (e.g. DB down, connection refused, constraint violation).
/// Caught exclusively in <c>AuditMiddleware</c>, which returns HTTP 503 and prevents the
/// protected action from executing — ensuring the "audit-before-action" invariant (AC-002;
/// HIPAA §164.312(b); OWASP A05).
/// Must NOT propagate to the global exception handler, which would return a generic 500.
/// </summary>
public sealed class AuditLogUnavailableException : Exception
{
    public AuditLogUnavailableException(string message, Exception? inner = null)
        : base(message, inner) { }
}
