using System.Security.Claims;
using Api.Constants;
using Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Features.Bookings;

/// <summary>
/// Handles walk-in appointment booking by front-desk staff — <c>POST /api/bookings/walkin</c>.
///
/// <para>
/// Restricted to <c>Staff</c> and <c>Admin</c> roles (AC-003; OWASP A01).
/// <c>staffId</c> is extracted exclusively from the JWT <c>sub</c> claim — never from the
/// request body — to prevent privilege escalation (OWASP A01; checklist).
/// </para>
/// </summary>
[ApiController]
[Route("bookings")]
[Authorize(Roles = $"{Roles.Staff},{Roles.Admin}")]
public sealed class WalkinBookingController : ControllerBase
{
    private readonly IWalkinBookingService _walkinBookingService;

    public WalkinBookingController(IWalkinBookingService walkinBookingService)
    {
        _walkinBookingService = walkinBookingService;
    }

    /// <summary>
    /// Creates a confirmed walk-in booking for an existing patient.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    ///   <item>201: booking confirmed — body contains <c>{bookingId, status, queuePosition}</c>.</item>
    ///   <item>400: request body is invalid.</item>
    ///   <item>404: referenced patient does not exist.</item>
    ///   <item>409 DuplicateBookingToday: patient already has a Confirmed booking today.</item>
    ///   <item>409 SlotNoLongerAvailable: slot was taken or does not exist.</item>
    ///   <item>503: database row lock timed out — client should retry.</item>
    /// </list>
    /// </remarks>
    [HttpPost("walkin")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CreateWalkinBooking(
        [FromBody] WalkinBookingRequest request,
        CancellationToken ct)
    {
        // staffId from JWT sub claim only — never from request body (OWASP A01; checklist)
        var staffId = User.FindFirstValue("sub") ?? "unknown";

        var result = await _walkinBookingService.CreateAsync(request, staffId, ct);

        return result switch
        {
            WalkinBookingSuccess s => StatusCode(StatusCodes.Status201Created, new
            {
                bookingId     = s.BookingId,
                status        = s.Status,
                queuePosition = s.QueuePosition,
            }),

            WalkinBookingPatientNotFound => NotFound(new { error = "Patient not found." }),

            WalkinBookingDuplicateToday d => Conflict(new
            {
                error             = "DuplicateBookingToday",
                existingBookingId = d.ExistingBookingId,
            }),

            WalkinBookingSlotUnavailable => Conflict(new
            {
                error = "SlotNoLongerAvailable",
            }),

            WalkinBookingLockTimeout => StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "The booking system is temporarily busy. Please try again shortly." }),

            _ => StatusCode(StatusCodes.Status500InternalServerError, new { error = "Unexpected error." })
        };
    }
}
