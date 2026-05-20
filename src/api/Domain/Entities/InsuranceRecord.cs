namespace Upacip.Api.Domain.Entities;

/// <summary>
/// Seed/reference data for insurance provider pattern matching.
/// Used by the insurance pre-check (soft validation) feature.
/// </summary>
public sealed class InsuranceRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ProviderName { get; set; } = string.Empty;

    // Regex pattern or prefix that a valid member ID must match for this provider
    public string MemberIdPattern { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
