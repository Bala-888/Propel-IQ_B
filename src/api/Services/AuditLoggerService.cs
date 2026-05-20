using Serilog;

namespace Api.Services;

/// <summary>
/// Implements <see cref="IAuditLogger"/> by writing fixed-schema structured events to Serilog.
/// ForContext chains attach all mandatory audit properties before the log entry is emitted,
/// ensuring Seq receives them as first-class searchable fields (AC-004).
/// </summary>
public sealed class AuditLoggerService : IAuditLogger
{
    private static readonly Serilog.ILogger _log = Serilog.Log.ForContext<AuditLoggerService>();

    /// <inheritdoc />
    public void Log(string actorId, string actionType, string resourceId, string? details = null)
    {
        _log
            .ForContext("EventType", "AuditLog")
            .ForContext("ActorId", actorId)
            .ForContext("ActionType", actionType)
            .ForContext("ResourceId", resourceId)
            // Details: empty string when absent so Seq always has the field present (AC-002; OWASP A09)
            .ForContext("Details", details ?? string.Empty)
            .Information(
                "Audit event: {ActionType} on {ResourceId} by {ActorId} at {OccurredAt}",
                actionType,
                resourceId,
                actorId,
                // Always UTC — never DateTime.Now or local offset (AC-004 edge: server timezone drift)
                DateTime.UtcNow);
    }
}
