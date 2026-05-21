using Api.Data;
using Api.Data.Entities;
using Api.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Serilog;

namespace Api.Audit;

/// <summary>
/// Persists audit entries to PostgreSQL via a dedicated <see cref="AuditDbContext"/> and emits
/// a Serilog structured event to Seq on every successful write (AC-001, AC-003).
/// </summary>
/// <remarks>
/// The <see cref="AuditDbContext"/> is intentionally separate from the request's
/// <c>AppDbContext</c> so the audit INSERT has its own transaction scope — it cannot be
/// rolled back by a failure in the request's unit-of-work (checklist: transaction isolation).
/// </remarks>
public sealed class PostgresAuditLogger : IAuditLogger
{
    private readonly AuditDbContext _auditContext;

    public PostgresAuditLogger(AuditDbContext auditContext)
    {
        _auditContext = auditContext;
    }

    /// <inheritdoc />
    public async Task RecordAsync(AuditEntry entry, CancellationToken ct = default)
    {
        var entity = new AuditLog
        {
            ActorId      = entry.ActorId,
            ActorRole    = entry.ActorRole,
            ActionType   = entry.ActionType,
            ResourceType = entry.ResourceType,
            ResourceId   = entry.ResourceId,
            IpAddress    = entry.IpAddress,
            UserAgent    = entry.UserAgent,
            // Set application-side (not DB default) to capture the moment of the action,
            // satisfying the 100 ms requirement and AC-003 OccurredAt accuracy (AC-001).
            OccurredAt   = entry.OccurredAt,
        };

        try
        {
            _auditContext.AuditLogs.Add(entity);
            await _auditContext.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is NpgsqlException || ex is DbUpdateException)
        {
            // AC-002: wrap DB failures in a typed exception caught by AuditMiddleware.
            // AuditLogUnavailableException must NOT propagate to the global 500 handler (OWASP A05).
            throw new AuditLogUnavailableException("Audit logging unavailable.", ex);
        }

        // AC-003: structured event emitted AFTER successful SaveChangesAsync.
        // No PHI fields — only structural IDs (OWASP A09; HIPAA minimum-necessary principle).
        Log
            .ForContext("EventType",   "AuditLog")
            .ForContext("ActionType",  entry.ActionType)
            .ForContext("ActorId",     entry.ActorId)
            .ForContext("ResourceId",  entry.ResourceId)
            .ForContext("OccurredAt",  entry.OccurredAt)
            .Information("Audit event recorded");
    }
}
