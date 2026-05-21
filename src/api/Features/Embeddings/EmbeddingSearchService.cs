using System.Globalization;
using Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Api.Features.Embeddings;

/// <summary>Result row returned by <see cref="EmbeddingSearchService.GetSimilarChunksAsync"/>.</summary>
public sealed record SimilarChunkResult
{
    public Guid   ChunkId    { get; init; }
    public string Content    { get; init; } = string.Empty;
    public float  Similarity { get; init; }
}

/// <summary>
/// Executes HNSW cosine similarity search against <c>document_chunk_embeddings</c>.
/// Registered as scoped so each call site receives a fresh <see cref="AppDbContext"/> (OWASP A04).
/// </summary>
public sealed class EmbeddingSearchService
{
    private readonly AppDbContext _db;

    public EmbeddingSearchService(AppDbContext db) => _db = db;

    /// <summary>
    /// Returns the top <paramref name="limit"/> document chunks most similar to
    /// <paramref name="queryVector"/> using cosine distance (AC-003; AIR-001).
    /// </summary>
    /// <param name="queryVector">1,536-dimensional query embedding.</param>
    /// <param name="limit">Maximum rows to return (default 5).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="queryVector"/> is not length 1,536 (OWASP A03 — input validation
    /// at service boundary prevents a malformed vector from reaching raw SQL).
    /// </exception>
    public async Task<IReadOnlyList<SimilarChunkResult>> GetSimilarChunksAsync(
        float[]           queryVector,
        int               limit = 5,
        CancellationToken ct    = default)
    {
        // Input validation: guard before raw SQL execution (OWASP A03; AC-003)
        if (queryVector.Length != 1536)
            throw new ArgumentException(
                $"queryVector must have 1536 dimensions; got {queryVector.Length}.",
                nameof(queryVector));

        // Build the pgvector text representation, e.g. "[0.1,-0.2,...]".
        // Values are float32 formatted with "G9" round-trip precision.
        // This is NOT user-controlled input — it is constructed from a validated float[] (OWASP A03).
        var vectorLiteral = "[" + string.Join(",",
            queryVector.Select(f => f.ToString("G9", CultureInfo.InvariantCulture))) + "]";

        // Parameterised raw SQL — $1::vector and $2 are positional Npgsql parameters (OWASP A03).
        // Aliases must match SimilarChunkResult property names for EF Core column mapping.
        var results = await _db.Database
            .SqlQueryRaw<SimilarChunkResult>(
                """
                SELECT  ce.chunk_id                              AS "ChunkId",
                        dc.content                               AS "Content",
                        1 - (ce.embedding <=> $1::vector)        AS "Similarity"
                FROM    document_chunk_embeddings ce
                JOIN    document_chunks           dc ON dc.id = ce.chunk_id
                ORDER BY ce.embedding <=> $1::vector
                LIMIT $2
                """,
                new Npgsql.NpgsqlParameter { Value = vectorLiteral, NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Text },
                new Npgsql.NpgsqlParameter { Value = limit,         NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Integer })
            .ToListAsync(ct);

        return results;
    }
}
