namespace Upacip.Api.Domain.Entities;

public enum MedicalCodeType { ICD10, CPT }

public enum ReviewStatus { Pending, Accepted, Rejected, Corrected }

/// <summary>
/// An AI-generated ICD-10 or CPT code suggestion awaiting staff review.
/// Human-in-the-loop: all suggestions stay Pending until explicitly reviewed.
/// </summary>
public sealed class MedicalCodeSuggestion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PatientId { get; set; }
    public MedicalCodeType CodeType { get; set; }
    public string CodeValue { get; set; } = string.Empty;
    public string CodeDescription { get; set; } = string.Empty;
    public float Confidence { get; set; }

    // JSON array of ChunkEmbedding.ChunkRef values used as evidence
    public string SourceChunkRefsJson { get; set; } = "[]";

    public ReviewStatus ReviewStatus { get; set; } = ReviewStatus.Pending;
    public Guid? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }

    // If corrected — the replacement code entered by staff
    public string? CorrectedCodeValue { get; set; }
    public string? CorrectedCodeDescription { get; set; }

    public DateTime SuggestedAt { get; set; } = DateTime.UtcNow;
}
