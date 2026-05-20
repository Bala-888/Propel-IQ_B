using Pgvector;

namespace Upacip.Api.Domain.Entities;

/// <summary>
/// A text chunk from a clinical document with its vector embedding for RAG retrieval.
/// ChunkText is AES-256 encrypted at rest.
/// </summary>
public sealed class ChunkEmbedding
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentId { get; set; }
    public Guid PatientId { get; set; }
    public int ChunkSequence { get; set; }
    public string ChunkRef { get; set; } = string.Empty;

    // PHI — encrypted at rest
    public string ChunkTextEncrypted { get; set; } = string.Empty;

    // pgvector embedding (dim=1536, Llama 3.1 embedding dimension)
    public Vector? Embedding { get; set; }

    public int TokenCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ClinicalDocument Document { get; set; } = null!;
}
