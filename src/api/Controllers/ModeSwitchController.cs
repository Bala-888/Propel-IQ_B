using Api.Constants;
using Api.DTOs;
using Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api.Controllers;

/// <summary>
/// Provides the <c>POST /intake/mode-switch</c> endpoint that transfers collected intake data
/// bidirectionally between AI and Manual intake modes (us_018; AC-001 AI→Manual;
/// AC-002 Manual→AI; AC-003 reviewItems).
///
/// <para>
/// Restricted to the <c>Patient</c> role — Staff and Admin receive 403; unauthenticated callers
/// receive 401 (OWASP A01 — broken access control).
/// </para>
///
/// <para>
/// PHI transferred between modes stays in memory only; no PHI is written to logs
/// (AIR guardrails; OWASP A09; HIPAA minimum-necessary).
/// </para>
/// </summary>
[ApiController]
[Route("intake")]
[Authorize(Roles = Roles.Patient)]
public sealed class ModeSwitchController : ControllerBase
{
    private readonly IntakeModeSwitchService          _switchService;
    private readonly ILogger<ModeSwitchController>    _logger;

    public ModeSwitchController(
        IntakeModeSwitchService       switchService,
        ILogger<ModeSwitchController> logger)
    {
        _switchService = switchService;
        _logger        = logger;
    }

    // ── POST /intake/mode-switch ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Transfers collected intake data from the source mode to the target mode.
    ///
    /// <para>
    /// AI → Manual: maps AI session fields to a <see cref="SaveDraftRequest"/>; unmapped
    /// free-text blobs are returned in <c>reviewItems[]</c> (AC-001; AC-003).
    /// </para>
    ///
    /// <para>
    /// Manual → AI: maps the patient's Draft record to a new <see cref="Api.AI.IntakeSessionState"/>
    /// and returns the new <c>sessionId</c> in the response (AC-002).
    /// </para>
    ///
    /// <para>
    /// Both directions return a <c>cacheVersion</c> token the frontend uses to suppress concurrent
    /// draft auto-saves that would overwrite the mode-switch result (Edge: concurrent auto-save;
    /// OWASP A04).
    /// </para>
    /// </summary>
    /// <response code="200">Mode switch succeeded; response body is <see cref="ModeSwitchResponse"/>.</response>
    /// <response code="400">Input validation failed (<c>from</c>, <c>to</c>, or <c>sessionId</c> invalid).</response>
    /// <response code="401">Caller is not authenticated.</response>
    /// <response code="403">Caller is authenticated but does not own the requested AI session.</response>
    [HttpPost("mode-switch")]
    [ProducesResponseType(typeof(ModeSwitchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SwitchModeAsync(
        [FromBody] ModeSwitchRequest request,
        CancellationToken ct)
    {
        // Authenticated patient identity (OWASP A01)
        var patientId = GetPatientId();
        if (patientId is null)
            return Unauthorized(new { error = "Authentication required." });

        // ── Boundary validation (OWASP A03) ──────────────────────────────────────────────────────

        if (!IsValidMode(request.From) || !IsValidMode(request.To))
            return BadRequest(new { error = "Both 'from' and 'to' must be 'AI' or 'Manual'." });

        if (string.Equals(request.From, request.To, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "'from' and 'to' must be different modes." });

        if (string.Equals(request.From, "AI", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(request.SessionId))
        {
            return BadRequest(new { error = "'sessionId' is required when switching from AI mode." });
        }

        // ── Dispatch ──────────────────────────────────────────────────────────────────────────────

        try
        {
            ModeSwitchResponse response;

            if (string.Equals(request.From, "AI", StringComparison.OrdinalIgnoreCase))
            {
                // AI → Manual: sessionId is guaranteed non-null/non-whitespace after validation above
                if (!Guid.TryParse(request.SessionId, out var sessionId))
                    return BadRequest(new { error = "Invalid 'sessionId' — expected a GUID." });

                response = await _switchService.SwitchAiToManualAsync(sessionId, patientId.Value, ct);
            }
            else
            {
                // Manual → AI: patient's Draft record is looked up by patientId
                response = await _switchService.SwitchManualToAiAsync(patientId.Value, ct);
            }

            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            // Session does not belong to the authenticated patient (OWASP A01; AC-001)
            _logger.LogWarning(
                "Mode-switch ownership check failed for patientId={PatientId}: {Message}",
                patientId, ex.Message);

            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { error = "You are not authorised to access this session." });
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────────────────────

    private static bool IsValidMode(string? mode) =>
        string.Equals(mode, "AI",     StringComparison.OrdinalIgnoreCase) ||
        string.Equals(mode, "Manual", StringComparison.OrdinalIgnoreCase);

    /// <summary>Parses the numeric patient ID from the JWT <c>sub</c> claim.</summary>
    private int? GetPatientId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(raw, out var id) ? id : null;
    }
}
