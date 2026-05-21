namespace Api.Audit;

/// <summary>
/// Factory that produces <see cref="AuditEntry"/> instances for background jobs and hosted services.
/// Guarantees <c>ActorId = "system"</c> and <c>ActorRole = "System"</c> are never null,
/// satisfying the HIPAA requirement for complete audit trail coverage of system-initiated actions
/// (Edge: system-initiated; AC-001).
/// </summary>
public static class SystemAuditContext
{
    /// <summary>
    /// Returns an <see cref="AuditEntry"/> with system-level actor context.
    /// </summary>
    /// <param name="actionType">One of the <see cref="AuditActionTypes"/> constants.</param>
    /// <param name="resourceType">The type of resource being acted upon (e.g. controller name).</param>
    /// <param name="resourceId">The identifier of the specific resource instance.</param>
    public static AuditEntry For(string actionType, string resourceType, string resourceId) =>
        new AuditEntry(
            ActorId:      "system",
            ActorRole:    "System",
            ActionType:   actionType,
            ResourceType: resourceType,
            ResourceId:   resourceId,
            IpAddress:    "127.0.0.1",
            UserAgent:    "system",
            OccurredAt:   DateTime.UtcNow);
}
