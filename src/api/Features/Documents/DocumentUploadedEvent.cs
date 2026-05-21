namespace Api.Features.Documents;

/// <summary>
/// Immutable event published to the bounded <see cref="System.Threading.Channels.Channel{T}"/>
/// after <see cref="DocumentUploadService"/> has persisted both the encrypted blob and the
/// <see cref="DocumentRecord"/> row (us_036/AC-001; OWASP A02).
///
/// <para>
/// Consumed by <see cref="Api.BackgroundServices.DocumentTextExtractionWorker"/> which decrypts
/// the blob, extracts PDF text, chunks it, and bulk-inserts <c>document_chunks</c> rows.
/// </para>
/// <para>
/// <b>Security:</b> <see cref="WrappedKey"/> is AES-256-GCM-wrapped key material — it must
/// never appear in any log output at any level (OWASP A02; checklist).
/// <see cref="BlobPath"/> is the on-disk path to the encrypted cipher blob — treated as
/// internal infrastructure data; not logged at Information or higher (OWASP A02).
/// </para>
/// </summary>
/// <param name="DocumentId">UUID that identifies the <see cref="DocumentRecord"/> row.</param>
/// <param name="PatientId">Numeric patient ID from the JWT sub claim — used for audit log correlation only; never included in log messages (OWASP A02).</param>
/// <param name="BlobPath">Absolute path to the encrypted cipher blob on disk (AC-001).</param>
/// <param name="WrappedKey">Per-document AES-256 key wrapped with the platform master key; 60 bytes (AC-001; OWASP A02).</param>
/// <param name="MimeType">MIME type detected from magic bytes during upload — used to guard non-PDF documents (Edge: DOCX).</param>
public sealed record DocumentUploadedEvent(
    Guid   DocumentId,
    int    PatientId,
    string BlobPath,
    byte[] WrappedKey,
    string MimeType);
