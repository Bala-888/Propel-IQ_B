namespace Upacip.Api.Domain.Entities;

/// <summary>
/// Immutable audit log entry. The application DB role has INSERT-only privilege on this table —
/// no UPDATE or DELETE is permitted (enforced in migration SQL).
/// </summary>
public sealed class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Nullable — unauthenticated requests (e.g., LOGIN_FAILED) may have no actor
    public Guid? ActorUserId { get; set; }
    public string? ActorRole { get; set; }

    public string ActionType { get; set; } = string.Empty;
    public string? ResourceType { get; set; }
    public string? ResourceId { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    // Additional context as JSON
    public string? DetailsJson { get; set; }

    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    // Soft FK — no cascade; actor accounts must never trigger log deletion
    public AppUser? ActorUser { get; set; }
}
