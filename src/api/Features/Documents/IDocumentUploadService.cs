using Microsoft.AspNetCore.Http;

namespace Api.Features.Documents;

/// <summary>Thrown when the uploaded file exceeds the 25 MB limit (AC-002).</summary>
public sealed class DocumentTooLargeException : Exception
{
    public DocumentTooLargeException()
        : base("File exceeds the 25 MB size limit.") { }
}

/// <summary>Thrown when magic byte inspection reveals a non-PDF and non-DOCX file (AC-001; OWASP A03).</summary>
public sealed class UnsupportedFileTypeException : Exception
{
    public UnsupportedFileTypeException()
        : base("Unsupported file type. Only PDF and DOCX are accepted.") { }
}

/// <summary>The result of a successful document upload — returned as the 201 response body (AC-004).</summary>
public sealed record DocumentUploadResult(Guid DocumentId, string Status);

/// <summary>
/// Handles the full upload pipeline for <c>POST /api/documents/upload</c>:
/// size guard → MIME magic byte check → AES-256-GCM encryption → atomic blob write → DB insert (AC-001–004).
/// </summary>
public interface IDocumentUploadService
{
    /// <summary>
    /// Validates, encrypts, and persists a patient document.
    /// </summary>
    /// <param name="file">Uploaded file from the multipart form body.</param>
    /// <param name="patientId">
    /// Numeric patient ID extracted from the JWT <c>sub</c> claim by the controller.
    /// Must never originate from the request body (OWASP A01).
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Upload result containing the new <c>documentId</c> and <c>status</c> (AC-004).</returns>
    /// <exception cref="DocumentTooLargeException">File is larger than 25 MB (AC-002).</exception>
    /// <exception cref="UnsupportedFileTypeException">Magic bytes do not match PDF or DOCX (AC-001).</exception>
    Task<DocumentUploadResult> UploadAsync(IFormFile file, int patientId, CancellationToken ct = default);
}
