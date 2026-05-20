namespace Api.Data.Entities;

public class MedicalCodeSuggestion
{
    public int Id { get; set; }
    public int ExtractedRecordId { get; set; }
    public string CodeSystem { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Confidence { get; set; }
    public DateTime SuggestedAt { get; set; }

    // AC-004: workflow review status; DB DEFAULT 'Pending' set via HasDefaultValue in AppDbContext
    // C# default initialiser ensures non-null on in-memory construction before any DB round-trip
    public string ReviewStatus { get; set; } = "Pending";

    public ExtractedRecord ExtractedRecord { get; set; } = null!;
}
