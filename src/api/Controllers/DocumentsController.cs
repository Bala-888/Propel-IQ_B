using System.Security.Claims;
using System.Threading.Channels;
using Api.Constants;
using Api.Data;
using Api.Features.Documents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

/// <summary>
/// Provides document status and retry endpoints for authenticated patients (us_039/AC-001, AC-004, AC-005).
///
/// <para>
/// Authorization: <c>Patient</c> role only (OWASP A01).
/// Ownership is enforced at the EF Core query layer (<c>WHERE patient_id = jwtPatientId</c>) —
/// not just at route level — to prevent horizontal privilege escalation (OWASP A01; checklist).
/// </para>
/// </summary>
[ApiController]
[Route("api/documents")]
[Authorize(Roles = Roles.Patient)]
public sealed class DocumentsController : ControllerBase
{
    private readonly AppDbContext                   _db;
    private readonly Channel<DocumentUploadedEvent> _uploadChannel;
    private readonly ILogger<DocumentsController>   _logger;

    public DocumentsController(
        AppDbContext                   db,
        Channel<DocumentUploadedEvent> uploadChannel,
        ILogger<DocumentsController>   logger)
    {
        _db            = db;
        _uploadChannel = uploadChannel;
        _logger        = logger;
    }

    /// <summary>
    /// Returns all documents for the authenticated patient, ordered by upload time descending.
    /// (SCR-010 page load; AC-001)
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<DocumentStatusDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetDocuments(CancellationToken ct)
    {
        var patientId = GetPatientId();
        if (patientId is null)
            return Unauthorized();

        // Ownership enforced at DB query layer — not in memory (OWASP A01; AC-001; checklist)
        var documents = await _db.DocumentRecords
            .Where(dr => dr.PatientId == patientId.Value)
            .OrderByDescending(dr => dr.UploadTimestamp)
            .Select(dr => new DocumentStatusDto
            {
                Id                  = dr.Id,
                FileName            = dr.OriginalFilename,
                Status              = dr.Status,
                ProcessingStartedAt = dr.ProcessingStartedAt,
            })
            .ToListAsync(ct);

        return Ok(documents);
    }

    /// <summary>
    /// Returns the current processing status of a single document owned by the authenticated patient.
    /// Returns 404 for both missing and unauthorised documents — no status code leaks ownership (OWASP A01).
    /// </summary>
    [HttpGet("{id:guid}/status")]
    [ProducesResponseType(typeof(DocumentStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetDocumentStatus(Guid id, CancellationToken ct)
    {
        var patientId = GetPatientId();
        if (patientId is null)
            return Unauthorized();

        // Ownership enforced at DB query — returns null for both non-existent and unauthorised (OWASP A01)
        var dto = await _db.DocumentRecords
            .Where(dr => dr.Id == id && dr.PatientId == patientId.Value)
            .Select(dr => new DocumentStatusDto
            {
                Id                  = dr.Id,
                FileName            = dr.OriginalFilename,
                Status              = dr.Status,
                ProcessingStartedAt = dr.ProcessingStartedAt,
            })
            .FirstOrDefaultAsync(ct);

        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>
    /// Retries pipeline processing for a <c>TimedOut</c> or <c>ExtractionFailed</c> document.
    /// Atomically cleans up stale artefacts, resets status, and re-queues <see cref="DocumentUploadedEvent"/>
    /// (AC-004, AC-005; OWASP A01, A03).
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    ///   <item>200: retry enqueued (or no-op if a concurrent call already reset the document).</item>
    ///   <item>404: document does not exist or belongs to another patient (OWASP A01 — no status code discrimination).</item>
    ///   <item>409: document is already <c>EntitiesExtracted</c> — pipeline is complete.</item>
    /// </list>
    /// </remarks>
    [HttpPost("{id:guid}/retry")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RetryDocument(Guid id, CancellationToken ct)
    {
        var patientId = GetPatientId();
        if (patientId is null)
            return Unauthorized();

        // Load the record fields needed for re-publish; ownership enforced at query (OWASP A01)
        var record = await _db.DocumentRecords
            .Where(dr => dr.Id == id && dr.PatientId == patientId.Value)
            .Select(dr => new { dr.Status, dr.BlobPath, dr.WrappedKey, dr.MimeType })
            .FirstOrDefaultAsync(ct);

        if (record is null)
            return NotFound();

        // 409 guard BEFORE any DELETE operations — never delete data on a complete document (AC-004; checklist)
        if (record.Status == "EntitiesExtracted")
            return Conflict(new { error = "Document processing is already complete." });

        // ── Cleanup stale artefacts via parameterised ExecuteSqlAsync (OWASP A03; checklist) ─────
        // patient_entities first (no CASCADE from document_records to patient_entities)
        await _db.Database.ExecuteSqlAsync(
            $"DELETE FROM patient_entities WHERE document_id = {id}", ct);

        // document_chunks CASCADE → document_chunk_embeddings; one delete covers both tables
        await _db.Database.ExecuteSqlAsync(
            $"DELETE FROM document_chunks WHERE document_id = {id}", ct);

        // ── Atomic status reset with idempotency guard (Edge: concurrent retry; AC-004; checklist) ─
        // WHERE clause includes status filter — if a concurrent call already reset to 'Uploaded',
        // 0 rows are updated and no event is re-published (idempotent, no duplicate pipeline job).
        var updated = await _db.DocumentRecords
            .Where(dr => dr.Id == id
                      && dr.PatientId == patientId.Value
                      && (dr.Status == "TimedOut" || dr.Status == "ExtractionFailed"))
            .ExecuteUpdateAsync(s =>
                s.SetProperty(dr => dr.Status, "Uploaded")
                 .SetProperty(dr => dr.ProcessingStartedAt, DateTimeOffset.UtcNow),
                ct);

        if (updated == 0)
        {
            // Concurrent call already reset this document — return 200 no-op (Edge: concurrent retry)
            return Ok(new { message = "Document is already being reprocessed." });
        }

        // ── Re-publish upload event to restart the pipeline (AC-004) ─────────────────────────────
        if (!_uploadChannel.Writer.TryWrite(new DocumentUploadedEvent(
                DocumentId: id,
                PatientId:  patientId.Value,
                BlobPath:   record.BlobPath,
                WrappedKey: record.WrappedKey,
                MimeType:   record.MimeType)))
        {
            _logger.LogWarning(
                "DocumentUploadedEvent channel full on retry; document {DocumentId} may require manual reprocessing",
                id);
        }

        _logger.LogInformation("Document {DocumentId} retry enqueued. status=Uploaded", id);
        return Ok(new { message = "Retry enqueued." });
    }

    /// <summary>Parses the numeric patient ID from the JWT <c>sub</c> claim.</summary>
    private int? GetPatientId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(raw, out var id) ? id : null;
    }
}
