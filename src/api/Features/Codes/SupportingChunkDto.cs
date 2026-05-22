namespace Api.Features.Codes;

/// <summary>
/// A single document chunk that supports a code suggestion (us_043/AC-004).
/// Enriches <c>supportingChunkIds</c> with <c>chunkText</c> and <c>sourceFilename</c> so
/// the frontend can render the "View Evidence" panel without a second API call.
/// OWASP A02: chunk text may contain PHI — returned to the authorised Clinician/Admin caller only;
/// never written to any ILogger.
/// </summary>
public sealed class SupportingChunkDto
{
    public Guid   ChunkId        { get; set; }
    public string ChunkText      { get; set; } = string.Empty;
    public string SourceFilename { get; set; } = string.Empty;
}
