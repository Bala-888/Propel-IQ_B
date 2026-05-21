using System.Security.Claims;
using Api.Constants;
using Api.DTOs;
using Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>
/// Handles appointment booking by authenticated patients — <c>POST /bookings</c> (us_020).
///
/// <para>
/// Authorization: <c>Patient</c> role only (AC-001; OWASP A01).
/// The patient identity is read exclusively from the JWT <c>sub</c> claim — never from the
/// request body — to prevent horizontal privilege escalation (OWASP A01; checklist).
/// </para>
/// </summary>
[ApiController]
[Route("bookings")]
[Authorize(Roles = Roles.Patient)]
public sealed class BookingsController : ControllerBase
{
    private readonly BookingService _bookingService;

    public BookingsController(BookingService bookingService)
    {
        _bookingService = bookingService;
    }

    /// <summary>
    /// Books the requested appointment slot for the authenticated patient.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    ///   <item>201: slot reserved — body contains <c>{bookingId, status, slot}</c>.</item>
    ///   <item>400: request body is invalid.</item>
    ///   <item>404: the requested slot does not exist.</item>
    ///   <item>409: slot already taken — body contains up to three alternative slots.</item>
    ///   <item>503: database row lock timed out — client should retry.</item>
    /// </list>
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(BookingConflictResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CreateBooking(
        [FromBody] CreateBookingRequest request,
        CancellationToken ct)
    {
        // Parse patient ID from JWT sub claim — never from the request body (OWASP A01)
        var patientId = GetPatientId();
        if (patientId is null)
            return Unauthorized();

        var result = await _bookingService.CreateBookingAsync(request.SlotId, patientId.Value, ct);

        return result switch
        {
            BookingSuccess s => CreatedAtAction(
                nameof(CreateBooking),
                new { id = s.BookingId },
                new { bookingId = s.BookingId, status = s.Status, slot = s.Slot }),

            BookingNotFound => NotFound(new { error = "Slot not found." }),

            BookingConflict c => Conflict(new BookingConflictResponse
            {
                Error        = "The requested slot is no longer available.",
                Alternatives = c.Alternatives,
            }),

            BookingDuplicateWindow => Conflict(new BookingConflictResponse
            {
                Error        = "You already have an active booking for this time window.",
                Alternatives = null,
            }),

            BookingLockTimeout => StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "The booking system is temporarily busy. Please try again shortly." }),

            BookingError e => StatusCode(StatusCodes.Status500InternalServerError,
                new { error = e.Message }),

            _ => StatusCode(StatusCodes.Status500InternalServerError, new { error = "Unknown error." })
        };
    }

    /// <summary>Parses the numeric patient ID from the JWT <c>sub</c> claim.</summary>
    private int? GetPatientId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(raw, out var id) ? id : null;
    }
}
