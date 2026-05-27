using Api.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api.Features.Queue;

/// <summary>
/// Same-day queue dashboard endpoints (us_031, us_032).
///
/// <para>
/// Restricted to <c>Staff</c> and <c>Admin</c> roles; Patient role receives 403 (OWASP A01).
/// </para>
/// </summary>
[ApiController]
[Route("queue")]
[Authorize(Roles = $"{Roles.Staff},{Roles.Admin}")]
public sealed class QueueController : ControllerBase
{
    private readonly IQueueService _queueService;

    public QueueController(IQueueService queueService)
    {
        _queueService = queueService;
    }

    /// <summary>
    /// Returns all <c>Confirmed</c> and <c>CheckedIn</c> bookings for the requested date,
    /// ordered by position ascending.
    /// </summary>
    /// <param name="since">
    ///     Optional ISO-8601 DateTimeOffset cursor for reconnection fallback (us_033/AC-004).
    ///     When provided, only entries created or updated at or after this timestamp are returned.
    ///     Validated with <c>DateTimeOffset.TryParse</c> — treated as null if unparseable (OWASP A03).
    /// </param>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<QueueEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetQueue([FromQuery] string? date, [FromQuery] string? since, CancellationToken ct)
    {
        var queryDate = DateOnly.TryParse(date, out var parsed)
            ? parsed
            : DateOnly.FromDateTime(DateTime.UtcNow.Date);

        // Validate since as ISO-8601 DateTimeOffset; treat as null if absent or unparseable (OWASP A03 — no injection risk).
        var querySince = DateTimeOffset.TryParse(since,
            null,
            System.Globalization.DateTimeStyles.RoundtripKind,
            out var parsedSince)
            ? parsedSince : (DateTimeOffset?)null;

        var entries = await _queueService.GetQueueAsync(queryDate, querySince, ct);
        return Ok(entries);
    }

    /// <summary>
    /// Marks the specified booking as arrived (<c>Status = "CheckedIn"</c>).
    /// Sets <c>CheckedInAt = DateTimeOffset.UtcNow</c> server-side — no timestamp is accepted from the client (AC-003).
    /// Idempotent: a re-call for an already-arrived booking returns 200 with unchanged <c>arrivedAt</c> (AC-004).
    /// </summary>
    /// <param name="bookingId">Integer PK of the target booking row; validated via route constraint (OWASP A03).</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPatch("{bookingId:int}/arrived")]
    [ProducesResponseType(typeof(ArrivedResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkArrived([FromRoute] int bookingId, CancellationToken ct)
    {
        // staffId from JWT sub claim only — never from request body or query param (OWASP A01; A07)
        var staffId = User.FindFirstValue("sub") ?? "unknown";

        var result = await _queueService.MarkArrivedAsync(bookingId, staffId, ct);
        if (result is null)
            return NotFound(new { error = "QueueEntryNotFound" });

        return Ok(result);
    }
}
