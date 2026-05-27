using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Api.Constants;
using Api.DTOs;
using Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>
/// Provides the three manual patient intake endpoints (us_017).
/// <list type="bullet">
///   <item><c>POST /intake/manual</c> — validates all 5 sections, encrypts, persists a Complete
///     record, and writes an <c>IntakeCompleted</c> audit log entry (AC-002; AC-003).</item>
///   <item><c>POST /intake/draft</c> — upserts a partial encrypted Draft record when the patient
///     navigates away from an incomplete form (AC-004).</item>
///   <item><c>GET /intake/draft</c> — returns decrypted Draft data for form pre-population; 404
///     when no Draft exists (AC-005).</item>
/// </list>
///
/// All three endpoints are restricted to the <c>Patient</c> role — Staff and Admin receive 403;
/// unauthenticated requests receive 401 (OWASP A01 — broken access control; AC-002).
///
/// PHI values in request bodies and decrypted draft responses must never be written to structured
/// logs — only structural IDs (patient ID, record ID) are emitted
/// (AIR guardrails; OWASP A09; HIPAA minimum-necessary).
/// </summary>
[ApiController]
[Route("intake")]
[Authorize(Roles = Roles.Patient)]
public sealed class ManualIntakeController : ControllerBase
{
    private readonly IntakeRecordService _recordService;
    private readonly ILogger<ManualIntakeController> _logger;

    public ManualIntakeController(
        IntakeRecordService              recordService,
        ILogger<ManualIntakeController>  logger)
    {
        _recordService = recordService;
        _logger        = logger;
    }

    // ── POST /intake/manual ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Validates all 5 intake sections, encrypts the payload with AES-256, persists an
    /// <see cref="Api.Data.Entities.IntakeRecord"/> with <c>Status = "Complete"</c> and
    /// <c>Mode = "Manual"</c>, and writes an audit log entry (AC-002).
    ///
    /// <para>Validation errors across all sections are collected before responding — the 422
    /// <see cref="ValidationProblemDetails"/> body contains all field-level paths at once rather
    /// than stopping at the first failure (AC-003; OWASP A03).</para>
    ///
    /// <para>A medication entry with a name but no dosage is not an error — the 201 response
    /// body includes a <c>warnings</c> array containing
    /// <c>"Consider adding dosage for clarity"</c> (Edge: brand-only medication; AC-002).</para>
    /// </summary>
    /// <response code="201">
    /// Record persisted; optional <c>warnings</c> array present when brand-only medications detected.
    /// </response>
    /// <response code="401">JWT missing or expired.</response>
    /// <response code="403">Caller is not in the <c>Patient</c> role.</response>
    /// <response code="422">
    /// One or more mandatory fields absent or date fields in an unrecognised format.
    /// Body is <see cref="ValidationProblemDetails"/> with field-level error paths.
    /// </response>
    [HttpPost("manual")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SubmitManualAsync(
        [FromBody] CreateManualIntakeRequest request,
        CancellationToken ct)
    {
        var patientId = GetPatientId();
        if (patientId is null)
            return Unauthorized(new { error = "Authentication required." });

        // Validate all 5 sections at the API boundary before any encryption or DB call (OWASP A03)
        if (!ManualIntakeValidator.TryValidate(request, out var problem, out var warnings))
            return UnprocessableEntity(problem);

        var recordId = await _recordService.SubmitManualAsync(patientId.Value, request, ct);

        // 201 with optional warnings array — an empty array is omitted from the response body
        return StatusCode(StatusCodes.Status201Created, new
        {
            id       = recordId,
            warnings = warnings.Count > 0 ? warnings : null,
        });
    }

    // ── POST /intake/draft ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Upserts a partial manual intake record with <c>Status = "Draft"</c> for the authenticated
    /// patient (AC-004).
    /// All request fields are optional — a save with only one of five sections populated is
    /// accepted without validation errors.
    /// Calling this endpoint twice for the same patient results in a single Draft row (upsert;
    /// AC-004; OWASP A04 — insecure design prevention).
    /// </summary>
    /// <response code="201">Draft saved or updated successfully.</response>
    /// <response code="401">JWT missing or expired.</response>
    /// <response code="403">Caller is not in the <c>Patient</c> role.</response>
    [HttpPost("draft")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> SaveDraftAsync(
        [FromBody] SaveDraftRequest request,
        CancellationToken ct)
    {
        var patientId = GetPatientId();
        if (patientId is null)
            return Unauthorized(new { error = "Authentication required." });

        await _recordService.SaveDraftAsync(patientId.Value, request, ct);
        return StatusCode(StatusCodes.Status201Created);
    }

    // ── GET /intake/draft ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the most recent manual Draft record for the authenticated patient with all PHI
    /// fields decrypted for form pre-population (AC-005).
    /// Returns 404 when no Draft record exists — either because one was never saved or because
    /// a successful <c>POST /intake/manual</c> submission cleaned up the draft.
    /// </summary>
    /// <response code="200">Decrypted draft data in <see cref="SaveDraftRequest"/> shape.</response>
    /// <response code="401">JWT missing or expired.</response>
    /// <response code="403">Caller is not in the <c>Patient</c> role.</response>
    /// <response code="404">No Draft record exists for this patient.</response>
    [HttpGet("draft")]
    [ProducesResponseType(typeof(SaveDraftRequest), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDraftAsync(CancellationToken ct)
    {
        var patientId = GetPatientId();
        if (patientId is null)
            return Unauthorized(new { error = "Authentication required." });

        var draft = await _recordService.GetDraftAsync(patientId.Value, ct);
        if (draft is null)
            return NotFound(new { error = "No draft intake record found." });

        // PHI values are in the response body for the authenticated patient only (OWASP A01)
        return Ok(draft);
    }

    // ── Private helpers ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Parses the numeric patient ID from the <c>sub</c> JWT claim.
    /// Returns <c>null</c> when the claim is absent or the value is not a valid integer
    /// (should not occur for a well-formed token; guarded here for defence-in-depth; OWASP A01).
    /// </summary>
    private int? GetPatientId()
    {
        // pid claim contains patients.id for Patient-role users (set during registration).
        // Falls back to sub (user id) for backward-compat with tokens issued before pid was added.
        var raw = User.FindFirstValue("pid") ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return int.TryParse(raw, out var id) ? id : null;
    }
}
