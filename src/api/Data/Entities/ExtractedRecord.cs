namespace Api.Data.Entities;

public class ExtractedRecord
{
    public int Id { get; set; }
    public int ClinicalDocumentId { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public string FieldValue { get; set; } = string.Empty;
    public decimal Confidence { get; set; }
    public DateTime ExtractedAt { get; set; }

    public ClinicalDocument ClinicalDocument { get; set; } = null!;
    public ICollection<ChunkEmbedding> ChunkEmbeddings { get; set; } = new List<ChunkEmbedding>();
    public ICollection<MedicalCodeSuggestion> MedicalCodeSuggestions { get; set; } = new List<MedicalCodeSuggestion>();
}
