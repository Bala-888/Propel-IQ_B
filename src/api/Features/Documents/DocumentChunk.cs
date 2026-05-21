namespace Api.Features.Documents;

/// <summary>
/// EF Core entity for a single sliding-window chunk of extracted PDF text.
/// Rows are bulk-inserted by <c>DocumentTextExtractionWorker</c> after PdfPig extraction
/// and <see cref="SlidingWindowChunker"/> produce the chunks (us_036/AC-003; AIR-003, AIR-004).
///
/// <para>
/// The <c>document_chunks</c> table is the input surface for downstream vector embedding
/// (Ollama / Llama 3.1 8B).  Plain-text <see cref="Content"/> is stored here; PHI may be
/// present in document content — access must be gated by patient ownership checks (OWASP A01).
/// </para>
/// </summary>
public sealed class DocumentChunk
{
    /// <summary>UUID primary key generated per chunk row.</summary>
    public Guid Id { get; set; }

    /// <summary>FK to <see cref="DocumentRecord.Id"/> — the source document (AC-003).</summary>
    public Guid DocumentId { get; set; }

    /// <summary>0-based position of this chunk within the document (AC-003).</summary>
    public int ChunkIndex { get; set; }

    /// <summary>
    /// Space-joined words for this window (window = 500 tokens, overlap = 50; AIR-003, AIR-004).
    /// TokenCount ≤ 500; exact count is in <see cref="TokenCount"/> (AC-002).
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Number of whitespace-separated tokens in this chunk.  Always ≤ 500 (window size; AC-002; AIR-003).
    /// The last chunk of a document may be smaller than 500 tokens.
    /// </summary>
    public int TokenCount { get; set; }

    /// <summary>UTC timestamp when this chunk row was inserted (AC-003).</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Navigation property to the parent <see cref="DocumentRecord"/>.</summary>
    public DocumentRecord? Document { get; set; }
}
