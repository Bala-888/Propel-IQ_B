using System.Security.Claims;
using Api.Constants;
using Api.DTOs;
using Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>
/// Exposes patient notification and calendar-sync preferences (us_029).
/// All actions require a Patient role JWT; ownership enforced by comparing the JWT sub claim
/// to the {id} route parameter before any DB access (OWASP A01; AC-002).
/// </summary>
[ApiController]
[Route("api/patients/{id:int}/preferences")]
[Authorize(Roles = Roles.Patient)]
public sealed class PatientPreferencesController : ControllerBase
{
    private readonly IPatientPreferencesService _service;

    public PatientPreferencesController(IPatientPreferencesService service)
    {
        _service = service;
    }

    /// <summary>
    /// Returns the current preference row for the authenticated patient (AC-002).
    /// Returns HTTP 403 if the JWT sub does not match the {id} route parameter (OWASP A01).
    /// Returns HTTP 404 if no preference row exists for the patient.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAsync(int id, CancellationToken ct)
    {
        var ownershipResult = CheckOwnership(id);
        if (ownershipResult is not null) return ownershipResult;

        var prefs = await _service.GetAsync(id, ct);
        if (prefs is null)
            return NotFound();

        return Ok(prefs);
    }

    /// <summary>
    /// Applies a partial update to the patient's preferences (AC-002).
    /// Only non-null fields in the request body are written; other fields are unchanged.
    /// Returns HTTP 200 with the full updated <see cref="PatientPreferencesResponse"/>.
    /// Returns HTTP 403 if the JWT sub does not match the {id} route parameter (OWASP A01).
    /// Returns HTTP 400 for malformed request bodies (model-state validation; OWASP A03).
    /// </summary>
    [HttpPatch]
    public async Task<IActionResult> PatchAsync(
        int                 id,
        [FromBody] PatchPreferencesDto dto,
        CancellationToken   ct)
    {
        // OWASP A01 / AC-002: ownership check — mismatches return 403 before any DB access.
        var ownershipResult = CheckOwnership(id);
        if (ownershipResult is not null) return ownershipResult;

        var updated = await _service.PatchAsync(id, dto, ct);
        return Ok(updated);
    }

    // ── Private helpers ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a 403 result if the authenticated patient's JWT sub claim does not equal
    /// <paramref name="requestedPatientId"/>; otherwise returns null (ownership confirmed).
    /// OWASP A01: check fires before any DB query so no data is fetched for unauthorised requests.
    /// </summary>
    private IActionResult? CheckOwnership(int requestedPatientId)
    {
        var subClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? User.FindFirstValue("sub");

        if (!int.TryParse(subClaim, out var patientId) || patientId != requestedPatientId)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { error = "Access denied. You can only access your own records." });
        }

        return null;
    }
}
