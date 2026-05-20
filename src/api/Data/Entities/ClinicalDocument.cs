namespace Api.Data.Entities;

public class ClinicalDocument
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }

    // AC-005: SHA-256 hex digest of the uploaded file; always 64 chars — enforced via HasMaxLength(64) in AppDbContext
    public string? FileHash { get; set; }

    public Patient Patient { get; set; } = null!;
    public ICollection<ExtractedRecord> ExtractedRecords { get; set; } = new List<ExtractedRecord>();
}
