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
    /// <param name="actorId">Identity of the actor (user ID or "unknown").</param>
    /// <param name="actionType">Semantic event name, e.g. <c>UserCreated</c>, <c>RoleChanged</c>.</param>
    /// <param name="resourceId">ID of the affected resource.</param>
    /// <param name="details">
    /// Optional JSON payload for structured events, e.g. <c>{"from":"Patient","to":"Staff"}</c>
    /// for <c>RoleChanged</c>. Stored in the <c>Details</c> Serilog context property (AC-002; OWASP A09).
    /// </param>
    void Log(string actorId, string actionType, string resourceId, string? details = null);
}
