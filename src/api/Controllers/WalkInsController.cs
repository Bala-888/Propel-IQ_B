using System.Security.Claims;
using Api.DTOs;
using Api.Exceptions;
using Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>
/// Handles walk-in patient booking requests from front-desk staff.
/// All routes require the <c>Staff</c> JWT role claim — enforced at the class level
/// so no per-action decoration drift is possible (AC-001, AC-002, AC-003; OWASP A01).
/// </summary>
[ApiController]
[Route("walkins")]
[Authorize(Roles = "Staff")]
public sealed class WalkInsController : ControllerBase
{
    private readonly IWalkInService _walkInService;

    public WalkInsController(IWalkInService walkInService)
    {
        _walkInService = walkInService;
    }

    /// <summary>
    /// Creates a walk-in booking.
    /// Returns HTTP 201 with <see cref="CreateWalkInResponse"/> on success.
    /// Returns HTTP 400 when <c>createAccount = true</c> and <c>email</c> is missing.
    /// Returns HTTP 404 when <c>linkExistingAccountId</c> references an unknown user.
    /// Returns HTTP 409 when <c>createAccount = true</c> and the email is already registered.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<CreateWalkInResponse>> CreateAsync(
        [FromBody] CreateWalkInRequest request,
        CancellationToken ct)
    {
        // ModelState validation (including IValidatableObject.Validate for the conditional email
        // requirement) is handled by [ApiController] + InvalidModelStateResponseFactory before
        // this action body runs (OWASP A03: validate at system boundary).
        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? User.FindFirstValue("sub")
                   ?? "unknown";

        try
        {
            var response = await _walkInService.CreateAsync(request, actorId, ct);
            return StatusCode(StatusCodes.Status201Created, response);
        }
        catch (DuplicateEmailException ex)
        {
            // AC-004: safe 409 — prompts front desk to use the link-existing flow.
            // existingUserId is intentionally included here (unlike AdminUsersController) because
            // the link-existing UX flow requires the front desk to supply it in the follow-up call.
            return Conflict(new
            {
                error = "An account with this email already exists. Would you like to link this walk-in to that account?",
                existingUserId = ex.ExistingUserId
            });
        }
        catch (KeyNotFoundException ex)
        {
            // Path 3: linkExistingAccountId referenced a user that does not exist.
            return NotFound(new { error = ex.Message });
        }
    }
}
