namespace Api.Data.Entities;

public class AuditLog
{
    public int Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? UserId { get; set; }
    public string? IpAddress { get; set; }
    public DateTime OccurredAt { get; set; }
}
