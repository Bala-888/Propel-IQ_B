using System.Threading.Channels;
using Api.Data;
using Microsoft.AspNetCore.Http;

namespace Api.Features.Documents;

/// <summary>
/// Implements the full upload pipeline for <c>POST /api/documents/upload</c>.
///
/// <para>Pipeline order (AC-001–004; OWASP A02, A03, A04):</para>
/// <list type="number">
///   <item>Size guard — checked via <c>IFormFile.Length</c> before any I/O (AC-002).</item>
///   <item>MIME magic byte check — reads first 8 bytes from the file stream; extension is never consulted (AC-001; OWASP A03).</item>
///   <item>AES-256-GCM encryption via <see cref="IDocumentEncryptionService"/> (AC-003; OWASP A02).</item>
///   <item>Atomic blob write — temp file promoted via <c>File.Move</c>; finally block removes orphaned files (Edge: interrupted upload; AC-003).</item>
///   <item>DB insert only after successful <c>File.Move</c>; SaveChanges failure triggers final-file cleanup (AC-004).</item>
/// </list>
/// <para>Sensitive data (filename, blob path, key bytes) is never written to any log (OWASP A02).</para>
/// </summary>
public sealed class DocumentUploadService : IDocumentUploadService
{
    private const long MaxFileSizeBytes = 25L * 1024 * 1024; // 25 MB (AC-002)

    // PDF magic: %PDF (4 bytes)
    private static readonly byte[] PdfMagic  = [0x25, 0x50, 0x44, 0x46];
    // DOCX/ZIP magic: PK\x03\x04 (4 bytes)
    private static readonly byte[] DocxMagic = [0x50, 0x4B, 0x03, 0x04];

    private readonly AppDbContext                     _db;
    private readonly IDocumentEncryptionService       _encryption;
    private readonly Channel<DocumentUploadedEvent>   _channel;
    private readonly string                           _blobDir;
    private readonly ILogger<DocumentUploadService>   _logger;

    public DocumentUploadService(
        AppDbContext db,
        IDocumentEncryptionService encryption,
        Channel<DocumentUploadedEvent> channel,
        IConfiguration configuration,
        ILogger<DocumentUploadService> logger)
    {
        _db         = db;
        _encryption = encryption;
        _channel    = channel;
        _logger     = logger;

        // DOCUMENT_BLOB_DIR read from environment via IConfiguration — never hard-coded (OWASP A02)
        var blobDir = configuration["DOCUMENT_BLOB_DIR"];
        if (string.IsNullOrWhiteSpace(blobDir))
            throw new InvalidOperationException("DOCUMENT_BLOB_DIR environment variable is not configured");

        _blobDir = blobDir;
    }

    /// <inheritdoc />
    public async Task<DocumentUploadResult> UploadAsync(
        IFormFile file,
        int patientId,
        CancellationToken ct = default)
    {
        // ── Step 1: Size check — evaluated before opening the file stream (AC-002; OWASP A04) ────
        // IFormFile.Length is populated by ASP.NET Core multipart binding before this call.
        // If the value is 0 the stream length is unknown; fall through to magic byte check
        // (a 0-byte file will pass the magic check only if it somehow provides 4 matching bytes).
        if (file.Length > MaxFileSizeBytes)
            throw new DocumentTooLargeException();

        // ── Step 2: MIME magic byte check (AC-001; OWASP A03) ────────────────────────────────────
        using var fileStream = file.OpenReadStream();

        var header = new byte[8];
        var bytesRead = await fileStream.ReadAsync(header.AsMemory(0, 8), ct);

        // Extension is not consulted — server-side content-type validation from byte content only
        var mimeType = DetectMimeType(header, bytesRead)
            ?? throw new UnsupportedFileTypeException();

        // Reset stream position so the encryption service reads the full file (not from byte 8)
        fileStream.Position = 0;

        // ── Steps 3–5: Encrypt → atomic write → DB insert ────────────────────────────────────────
        var documentId = Guid.NewGuid(); // independent UUID per request — concurrent-safe (Edge: concurrent; AC-004)
        var tempPath   = Path.Combine(_blobDir, $"{documentId}.tmp");
        var finalPath  = Path.Combine(_blobDir, $"{documentId}.enc");

        bool dbSaved = false;
        try
        {
            // Step 3: AES-256-GCM encryption — plaintext bytes are never written to disk (AC-003; OWASP A02)
            var encDoc = _encryption.Encrypt(fileStream);

            // Step 4: Write cipher bytes to temp file with WriteThrough so OS flushes before Move
            Directory.CreateDirectory(_blobDir); // no-op if already exists; safe for concurrent requests
            await using (var fs = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 65536,
                FileOptions.WriteThrough))
            {
                await fs.WriteAsync(encDoc.CipherBytes, ct);
            }

            // Atomic promotion: temp → final on the same volume; overwrite:false prevents clobber (Edge: concurrent)
            File.Move(tempPath, finalPath, overwrite: false);

            // Step 5: Insert document_records row only after File.Move succeeds (AC-004; Edge: interrupted upload)
            _db.DocumentRecords.Add(new DocumentRecord
            {
                Id                   = documentId,
                PatientId            = patientId,
                OriginalFilename     = file.FileName,  // stored for display; never used for MIME detection (AC-001)
                MimeType             = mimeType,
                SizeBytes            = file.Length,
                UploadTimestamp      = DateTimeOffset.UtcNow,
                Status               = "Uploaded",
                BlobPath             = finalPath,
                WrappedKey           = encDoc.WrappedKey,
                ProcessingStartedAt  = DateTimeOffset.UtcNow, // SLA timer starts at upload (us_039/AC-002)
            });

            await _db.SaveChangesAsync(ct);
            dbSaved = true;

            // Enqueue event for background text extraction (us_036/AC-001; step 2 of impl plan)
            // TryWrite is fire-and-forget — channel backpressure must NEVER block the HTTP 201 response (checklist)
            if (!_channel.Writer.TryWrite(new DocumentUploadedEvent(
                    DocumentId: documentId,
                    PatientId:  patientId,
                    BlobPath:   finalPath,
                    WrappedKey: encDoc.WrappedKey,
                    MimeType:   mimeType)))
            {
                // DropOldest mode means TryWrite only returns false when the channel is closed;
                // log Warning so operators can detect unexpected channel closure (AC-001; OWASP A04)
                _logger.LogWarning(
                    "DocumentUploadedEvent channel unavailable; document {DocumentId} may require manual reprocessing",
                    documentId);
            }

            // Log only the non-PHI UUID — filename, blob path, and key bytes are never logged (OWASP A02)
            _logger.LogInformation(
                "Document uploaded successfully for patient {PatientId}. DocumentId: {DocumentId}",
                patientId, documentId);

            return new DocumentUploadResult(documentId, "Uploaded");
        }
        finally
        {
            // Always remove temp file if it still exists (e.g., failure before or during File.Move)
            if (File.Exists(tempPath))
                File.Delete(tempPath);

            // Remove final file if DB save did not complete — prevents orphaned encrypted blobs (Edge: interrupted upload; AC-003)
            if (!dbSaved && File.Exists(finalPath))
                File.Delete(finalPath);
        }
    }

    /// <summary>
    /// Inspects the first bytes of the uploaded file to determine the MIME type.
    /// Returns <c>"application/pdf"</c>, <c>"application/vnd.openxmlformats-officedocument.wordprocessingml.document"</c>,
    /// or <c>null</c> if the bytes do not match a supported format (AC-001; OWASP A03).
    /// </summary>
    private static string? DetectMimeType(byte[] header, int bytesRead)
    {
        if (bytesRead < 4)
            return null;

        if (header[0] == PdfMagic[0] && header[1] == PdfMagic[1] &&
            header[2] == PdfMagic[2] && header[3] == PdfMagic[3])
            return "application/pdf";

        if (header[0] == DocxMagic[0] && header[1] == DocxMagic[1] &&
            header[2] == DocxMagic[2] && header[3] == DocxMagic[3])
            return "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

        return null;
    }
}
