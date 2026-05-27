using System.Security.Claims;
using Api.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Features.Documents;

/// <summary>
/// Handles <c>POST /api/documents/upload</c> — restricted to the Patient role (OWASP A01).
///
/// <para>
/// <c>patientId</c> is extracted exclusively from the JWT <c>sub</c> claim.
/// It is never accepted from the multipart form body or query string (OWASP A01; AC-004; checklist).
/// </para>
/// </summary>
[ApiController]
[Route("documents")]
[Authorize(Roles = Roles.Patient)]
public sealed class DocumentUploadController : ControllerBase
{
    private readonly IDocumentUploadService _uploadService;

    public DocumentUploadController(IDocumentUploadService uploadService)
    {
        _uploadService = uploadService;
    }

    /// <summary>
    /// Uploads and encrypts a patient document (PDF or DOCX, max 25 MB).
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    ///   <item>201: accepted — body contains <c>{"documentId": "&lt;uuid&gt;", "status": "Uploaded"}</c> (AC-004).</item>
    ///   <item>400: file type is not PDF or DOCX — MIME magic byte check failed (AC-001).</item>
    ///   <item>413: file exceeds the 25 MB limit (AC-002).</item>
    ///   <item>401: unauthenticated request.</item>
    ///   <item>403: authenticated user is not the Patient role (OWASP A01).</item>
    /// </list>
    /// </remarks>
    [HttpPost("upload")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status413RequestEntityTooLarge)]
    public async Task<IActionResult> UploadAsync(
        IFormFile file,
        CancellationToken ct)
    {
        // patientId from JWT pid claim (patients.id) — falls back to sub for backward compat.
        // Never accepted from request body or query string (OWASP A01; AC-004; checklist).
        var raw = User.FindFirstValue("pid") ?? User.FindFirstValue("sub");
        if (!int.TryParse(raw, out var patientId))
            return Unauthorized();

        try
        {
            var result = await _uploadService.UploadAsync(file, patientId, ct);

            return StatusCode(StatusCodes.Status201Created, new
            {
                documentId = result.DocumentId,
                status     = result.Status,
            });
        }
        catch (DocumentTooLargeException ex)
        {
            return StatusCode(StatusCodes.Status413RequestEntityTooLarge,
                new { error = ex.Message });
        }
        catch (UnsupportedFileTypeException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
