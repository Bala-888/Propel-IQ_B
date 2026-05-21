namespace Api.Features.Documents;

/// <summary>
/// Response DTO for <c>GET /api/documents</c> and <c>GET /api/documents/{id}/status</c>
/// (us_039/AC-001; SCR-010).
/// </summary>
public sealed record DocumentStatusDto
{
    public Guid            Id                   { get; init; }
    public string          FileName             { get; init; } = string.Empty;
    public string          Status               { get; init; } = string.Empty;
    public DateTimeOffset  ProcessingStartedAt  { get; init; }
}
