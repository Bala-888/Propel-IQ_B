using Pgvector;

namespace Api.Features.Embeddings;

/// <summary>
/// EF Core entity for the <c>document_chunk_embeddings</c> table.
/// One row per <see cref="Api.Features.Documents.DocumentChunk"/>; the <c>chunk_id</c> column
/// is both the primary key and the foreign key into <c>document_chunks</c>.
/// </summary>
/// <remarks>
/// Named <c>DocumentChunkEmbedding</c> (not <c>ChunkEmbedding</c>) to avoid namespace collision
/// with the pre-existing <c>Api.Data.Entities.ChunkEmbedding</c> entity (us_006 legacy entity,
/// different schema, maps to <c>chunk_embeddings</c> table).
/// Table <c>document_chunk_embeddings</c> is configured explicitly in
/// <see cref="Api.Data.AppDbContext.OnModelCreating"/>.
/// </remarks>
public sealed class DocumentChunkEmbedding
{
    /// <summary>
    /// Primary key — also a foreign key to <c>document_chunks.id</c>.
    /// No separate surrogate key is needed because each chunk has at most one embedding row
    /// (AC-002 — no duplicate rows; 1:1 relationship).
    /// </summary>
    public Guid ChunkId { get; set; }

    /// <summary>
    /// 1,536-dimensional vector produced by the Ollama <c>llama3.1:8b</c> model.
    /// Stored as <c>vector(1536)</c> in pgvector; validated by
    /// <see cref="Api.BackgroundServices.EmbeddingWorker"/> before insert (Edge: dimension mismatch).
    /// </summary>
    public Vector Embedding { get; set; } = null!;

    /// <summary>UTC timestamp of row creation.</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
