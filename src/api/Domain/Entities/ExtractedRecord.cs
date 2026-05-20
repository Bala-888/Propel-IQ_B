namespace Upacip.Api.Domain.Entities;

public enum EntityType { Vital, Medication, Diagnosis, Note, Allergy }
public enum ExtractionStatus { Active, LowConfidence, Duplicate }

/// <summary>
/// A single structured clinical entity extracted from a document by the AI pipeline.
/// Value is AES-256 encrypted at rest.
/// </summary>
public sealed class ExtractedRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PatientId { get; set; }
    public Guid DocumentId { get; set; }
    public EntityType EntityType { get; set; }

    // PHI — encrypted at rest
    public string ValueEncrypted { get; set; } = string.Empty;

    public float Confidence { get; set; }
    public string? ChunkRef { get; set; }
    public ExtractionStatus Status { get; set; } = ExtractionStatus.Active;
    public bool IsDuplicate { get; set; } = false;
    public Guid? DuplicateOfId { get; set; }
    public DateTime ExtractedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ClinicalDocument Document { get; set; } = null!;
}
