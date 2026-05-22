namespace Api.Features.Patients;

/// <summary>
/// Summary projection of a single <see cref="Api.Features.Entities.PatientEntity"/> used inside
/// <see cref="ConflictDto"/> to identify each entity in a conflict pair (us_040/AC-004).
/// OWASP A02: Value is PHI — never written to ILogger.
/// </summary>
public sealed class EntitySummaryDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// DTO representation of a detected clinical conflict returned in the 360° patient summary
/// (us_040/AC-004).  Maps from <see cref="Api.Features.Conflicts.ClinicalConflict"/>.
/// </summary>
public sealed class ConflictDto
{
    public Guid Id { get; set; }
    public EntitySummaryDto EntityA { get; set; } = null!;
    public EntitySummaryDto EntityB { get; set; } = null!;
    public string ConflictType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
