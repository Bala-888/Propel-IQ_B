namespace Upacip.Api.Domain.Entities;

public enum ConflictStatus { Unresolved, Resolved, Dismissed }

/// <summary>
/// A detected contradiction between two ExtractedRecord values for the same patient.
/// Surfaced to staff in the 360° patient view for manual resolution.
/// </summary>
public sealed class ConflictFlag
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PatientId { get; set; }
    public EntityType EntityType { get; set; }

    // Both sides of the conflict — PHI, encrypted at rest
    public string ValueAEncrypted { get; set; } = string.Empty;
    public Guid SourceDocumentAId { get; set; }
    public string ValueBEncrypted { get; set; } = string.Empty;
    public Guid SourceDocumentBId { get; set; }

    public float Confidence { get; set; }
    public ConflictStatus Status { get; set; } = ConflictStatus.Unresolved;
    public Guid? ResolvedByUserId { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionNotes { get; set; }
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
}
