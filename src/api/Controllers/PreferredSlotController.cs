using System.Security.Claims;
using Api.Constants;
using Api.DTOs;
using Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>
/// Manages preferred alternative slot registration for confirmed bookings (us_024; AC-001–AC-004).
///
/// <para>
/// All routes are restricted to the <c>Patient</c> role.  <c>patientId</c> is sourced
/// exclusively from the JWT sub claim — the request body and route contain only <c>bookingId</c>
/// and <c>slotId</c> (OWASP A01; A07).
/// </para>
/// </summary>
[ApiController]
[Route("api/bookings")]
[Authorize(Roles = Roles.Patient)]
public sealed class PreferredSlotController : ControllerBase
{
    private readonly IPreferredSlotService _preferredSlotService;

    public PreferredSlotController(IPreferredSlotService preferredSlotService)
    {
        _preferredSlotService = preferredSlotService;
    }

    /// <summary>
    /// Registers or replaces the preferred alternative slot for <paramref name="bookingId"/>.
    ///
    /// <para>Returns HTTP 201 with <c>{"preferredSlotId": &lt;int&gt;, "status": "Registered"}</c> on success.</para>
    /// <para>Returns HTTP 400 when <c>slotId</c> equals the booking's active slot (AC-003).</para>
    /// <para>Returns HTTP 403 when the booking does not belong to the requesting patient (OWASP A01).</para>
    /// <para>Returns HTTP 409 when the booking is not confirmed (AC-004) or the slot is unavailable (Edge).</para>
    /// </summary>
    [HttpPost("{bookingId:int}/preferred-slot")]
    public async Task<IActionResult> SetAsync(
        int                      bookingId,
        [FromBody] SetPreferredSlotRequest request,
        CancellationToken        ct)
    {
        // patientId is always sourced from the JWT sub claim (OWASP A01; A07)
        var subClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? User.FindFirstValue("sub");

        if (!int.TryParse(subClaim, out var patientId))
            return Unauthorized(new { error = "Invalid or missing patient identity claim." });

        var result = await _preferredSlotService.SetPreferredSlotAsync(
            bookingId, patientId, request.SlotId, ct);

        return result switch
        {
            PreferredSlotRegistered r        => StatusCode(StatusCodes.Status201Created, new { preferredSlotId = r.PreferredSlotId, status = "Registered" }),
            PreferredSlotForbidden            => StatusCode(StatusCodes.Status403Forbidden, new { error = "Access denied." }),
            PreferredSlotBookingNotConfirmed  => Conflict(new { error = "Preferred slot selection is only available for active confirmed bookings." }),
            PreferredSlotSameAsActive         => BadRequest(new { error = "Preferred slot cannot be the same as the active booking." }),
            PreferredSlotSlotNotAvailable     => Conflict(new { error = "The selected slot is no longer available." }),
            _                                 => StatusCode(StatusCodes.Status500InternalServerError, new { error = "An unexpected error occurred." }),
        };
    }
}
