using Api.DTOs;
using Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>
/// Provides the <c>GET /slots</c> endpoint for browsing paginated appointment slots (us_019;
/// AC-001; AC-002).
///
/// <para>
/// All authenticated roles (Patient, Staff, Admin) may call this endpoint — no PHI is returned
/// because <see cref="SlotDto"/> contains only schedule data
/// (OWASP A01 — broken access control; OWASP A02 — minimum data exposure; HIPAA).
/// Unauthenticated callers receive 401.
/// </para>
/// </summary>
[ApiController]
[Route("slots")]
[Authorize] // All authenticated roles permitted; unauthenticated → 401 (OWASP A01; AC-001)
public sealed class SlotsController : ControllerBase
{
    private readonly SlotsService _slotsService;

    public SlotsController(SlotsService slotsService)
    {
        _slotsService = slotsService;
    }

    // ── GET /slots ────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a paginated list of appointment slots.
    /// When <c>available=true</c>, only slots with <c>IsAvailable = true</c> are returned;
    /// Booked slots are excluded at the database level (AC-001).
    /// </summary>
    /// <param name="query">Query parameters bound from the query string.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Page of slots and pagination metadata (AC-001; AC-002).</response>
    /// <response code="400">
    /// <c>page &lt; 1</c> or <c>pageSize &lt; 1</c> or <c>pageSize &gt; 100</c>
    /// (OWASP A03 — validate at boundary; AC-002).
    /// </response>
    /// <response code="401">Caller is not authenticated (OWASP A01).</response>
    [HttpGet]
    [ProducesResponseType(typeof(SlotsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAsync(
        [FromQuery] GetSlotsQuery query,
        CancellationToken ct)
    {
        // ── Boundary validation (OWASP A03; AC-002) ──────────────────────────────────────────────
        if (query.Page < 1)
        {
            ModelState.AddModelError(nameof(query.Page), "'page' must be greater than or equal to 1.");
            return ValidationProblem(ModelState);
        }

        if (query.PageSize < 1 || query.PageSize > 100)
        {
            ModelState.AddModelError(
                nameof(query.PageSize),
                "'pageSize' must be between 1 and 100.");
            return ValidationProblem(ModelState);
        }

        var response = await _slotsService.GetSlotsAsync(query, ct);
        return Ok(response);
    }
}
