using Pgvector;

namespace Api.Data.Entities;

public class ChunkEmbedding
{
    public int Id { get; set; }
    public int ExtractedRecordId { get; set; }
    public string ChunkText { get; set; } = string.Empty;
    public Vector Embedding { get; set; } = null!;

    public ExtractedRecord ExtractedRecord { get; set; } = null!;
}
