using System.Linq.Expressions;
using Api.Features.Documents;

namespace Api.Features.Patients;

/// <summary>
/// Document entry in the 360° patient summary — metadata only; no decrypted blob bytes (us_040/AC-002).
/// OriginalFilename is client-supplied and stored for display; it is never used for MIME detection
/// (OWASP A03 — content-type determined by magic bytes at upload time).
/// </summary>
public sealed class DocumentSummaryDto
{
    public Guid Id { get; set; }
    public string OriginalFilename { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTimeOffset UploadedAt { get; set; }
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// EF Core projection selector — translates to a SQL SELECT without loading blob bytes
    /// or the wrapped encryption key into memory (AC-002; OWASP A02).
    /// Uses UploadTimestamp (the entity's upload field) mapped to UploadedAt in the DTO.
    /// </summary>
    public static Expression<Func<DocumentRecord, DocumentSummaryDto>> Selector =>
        dr => new DocumentSummaryDto
        {
            Id               = dr.Id,
            OriginalFilename = dr.OriginalFilename,
            MimeType         = dr.MimeType,
            SizeBytes        = dr.SizeBytes,
            UploadedAt       = dr.UploadTimestamp,
            Status           = dr.Status,
        };
}
