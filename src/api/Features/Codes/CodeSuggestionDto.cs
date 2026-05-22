namespace Api.Features.Codes;

/// <summary>
/// A single ICD-10 or CPT code suggestion produced by the RAG pipeline (us_043/AC-001).
/// OWASP A02: <see cref="SupportingChunks"/> texts may contain PHI — returned to
/// authorised Clinician/Admin callers only; never written to any ILogger.
/// </summary>
public sealed class CodeSuggestionDto
{
    /// <summary>Stable UUID from the <c>code_suggestions</c> row; populated after upsert (AC-001).</summary>
    public Guid Id { get; set; }

    /// <summary>Review state — <c>"Pending"</c> | <c>"Accepted"</c> | <c>"Corrected"</c> | <c>"Rejected"</c> (AC-001).</summary>
    public string ReviewStatus { get; set; } = "Pending";

    /// <summary>Code type — <c>"ICD10"</c> or <c>"CPT"</c>.</summary>
    public string CodeType { get; set; } = string.Empty;

    /// <summary>
    /// Code value validated against the appropriate regex before inclusion.
    /// ICD-10: <c>^[A-Z][0-9]{2}(\.[0-9A-Z]{1,4})?$</c> (AC-002).
    /// CPT:    <c>^\d{5}$</c> (AC-003).
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Human-readable description produced by the Ollama model.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Ollama-reported confidence in [0.0, 1.0].</summary>
    public double Confidence { get; set; }

    /// <summary>
    /// <see langword="true"/> when <see cref="Confidence"/> &lt; 0.3.
    /// Suggestions are never dropped for low confidence — flagged and included (Edge: confidence &lt; 0.3).
    /// </summary>
    public bool LowConfidence { get; set; }

    /// <summary>
    /// Document chunks from the similarity search that support this suggestion (AC-004).
    /// Includes <c>chunkText</c> and <c>sourceFilename</c> for the "View Evidence" panel.
    /// Chunk IDs cross-referenced against the actual pgvector search results — any Ollama-hallucinated
    /// IDs not present in the search result set are silently filtered out (OWASP A01).
    /// </summary>
    public List<SupportingChunkDto> SupportingChunks { get; set; } = new();
}
