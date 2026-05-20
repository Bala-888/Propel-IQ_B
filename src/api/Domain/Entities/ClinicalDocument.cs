namespace Upacip.Api.Domain.Entities;

public enum DocumentProcessingStatus
{
    Pending,
    Extracting,
    Complete,
    Failed
}

/// <summary>
/// Represents a clinical document uploaded by a patient.
/// StoragePath is AES-256 encrypted at rest.
/// </summary>
public sealed class ClinicalDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PatientId { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string Sha256Hash { get; set; } = string.Empty;

    // PHI — encrypted at rest
    public string StoragePathEncrypted { get; set; } = string.Empty;

    public DocumentProcessingStatus ProcessingStatus { get; set; } = DocumentProcessingStatus.Pending;
    public string? ProcessingError { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }

    // Navigation
    public Patient Patient { get; set; } = null!;
    public ICollection<ExtractedRecord> ExtractedRecords { get; set; } = [];
    public ICollection<ChunkEmbedding> ChunkEmbeddings { get; set; } = [];
}
