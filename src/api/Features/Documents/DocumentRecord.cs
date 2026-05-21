namespace Api.Features.Documents;

/// <summary>
/// EF Core entity for the <c>document_records</c> table.
/// Stores metadata and encrypted blob location for patient-uploaded documents.
/// Plaintext file bytes are never persisted — only AES-256-GCM cipher bytes are written to blob
/// storage (AC-003; OWASP A02). The per-document wrapped key is stored here, not inline in the blob.
/// </summary>
public sealed class DocumentRecord
{
    /// <summary>Independent UUID generated per upload — safe for concurrent requests (Edge: concurrent; AC-004).</summary>
    public Guid Id { get; set; }

    /// <summary>Patient's numeric ID from the JWT sub claim (OWASP A01).</summary>
    public int PatientId { get; set; }

    /// <summary>Original client-supplied filename — stored for display only, never used for MIME detection (AC-001).</summary>
    public string OriginalFilename { get; set; } = string.Empty;

    /// <summary>MIME type detected from magic bytes — not the client Content-Type header (AC-001).</summary>
    public string MimeType { get; set; } = string.Empty;

    /// <summary>Size of the original plaintext file in bytes (AC-002).</summary>
    public long SizeBytes { get; set; }

    /// <summary>UTC timestamp when the upload was accepted and persisted (AC-004).</summary>
    public DateTimeOffset UploadTimestamp { get; set; }

    /// <summary>
    /// Upload / processing status. Valid values: <c>"Uploaded"</c>, <c>"Chunked"</c>,
    /// <c>"ExtractionFailed"</c> (us_036/AC-001, AC-003, AC-004).
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Populated when <see cref="Status"/> is <c>"ExtractionFailed"</c>.
    /// <c>"NoTextContent"</c> for empty-text PDFs; truncated exception message (max 500 chars)
    /// for other pipeline failures (us_036/AC-004; OWASP A04 — column size guard).
    /// </summary>
    public string? FailureReason { get; set; }

    /// <summary>Absolute path to the AES-256-GCM encrypted blob file on disk (AC-003).</summary>
    public string BlobPath { get; set; } = string.Empty;

    /// <summary>
    /// Per-document AES-256 key wrapped (AES-256-GCM encrypted) with the platform master key.
    /// Format: [nonce(12) || encryptedKey(32) || tag(16)] = 60 bytes stored as bytea (AC-003; OWASP A02).
    /// </summary>
    public byte[] WrappedKey { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// UTC timestamp when upload was accepted; used as the reference point for the
    /// 120-second processing SLA enforced by <c>PipelineGuardianWorker</c> (us_039/AC-002).
    /// Populated at upload time; reset to <c>UtcNow</c> on retry (AC-004).
    /// </summary>
    public DateTimeOffset ProcessingStartedAt { get; set; }
}
