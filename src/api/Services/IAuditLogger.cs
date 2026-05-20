namespace Api.Services;

/// <summary>
/// Writes structured audit log events to the centralised log pipeline (Serilog → Seq).
/// Each call emits a fixed-schema event carrying EventType, ActorId, ActionType, ResourceId,
/// and OccurredAt (UTC) — satisfying OWASP A09 audit completeness requirements (AC-004).
/// </summary>
public interface IAuditLogger
{
    /// <summary>
    /// Records an auditable action performed by <paramref name="actorId"/> on <paramref name="resourceId"/>.
    /// </summary>
    void Log(string actorId, string actionType, string resourceId);
}
