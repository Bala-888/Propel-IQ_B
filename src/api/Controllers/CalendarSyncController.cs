using System.Threading.Channels;
using Api.Constants;
using Api.Data;
using Api.DTOs;
using Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Api.Controllers;

/// <summary>
/// Handles <c>POST /api/calendar/sync</c> — enqueues a calendar sync command for asynchronous
/// processing, returning 202 Accepted immediately (us_028; Edge: SCR-007 loads before sync completes).
///
/// <para>
/// <b>Ownership check</b>: <c>booking.PatientId</c> is compared to the JWT <c>sub</c> claim before
/// any command is enqueued — a patient cannot sync another patient's booking (OWASP A01; A07).
/// </para>
///
/// <para>
/// <b>Non-blocking</b>: the HTTP response is never blocked by external provider latency.
/// The actual Google Calendar / Microsoft Graph API call runs in
/// <see cref="CalendarSyncWorker"/> (Edge: 202 before sync completes; AC-005).
/// </para>
/// </summary>
[ApiController]
[Route("api/calendar")]
[Authorize(Roles = Roles.Patient)]
public sealed class CalendarSyncController : ControllerBase
{
    private readonly AppDbContext                  _db;
    private readonly Channel<CalendarSyncCommand>  _channel;

    public CalendarSyncController(
        AppDbContext                 db,
        Channel<CalendarSyncCommand> channel)
    {
        _db      = db;
        _channel = channel;
    }

    /// <summary>
    /// Enqueues a calendar sync command for the specified booking and returns
    /// <c>202 Accepted</c> immediately.
    ///
    /// <para>Returns 400 when <c>provider</c> is not "Google" or "Outlook".</para>
    /// <para>Returns 403 when the authenticated patient does not own the booking (OWASP A01).</para>
    /// <para>Returns 404 when the booking does not exist.</para>
    /// </summary>
    [HttpPost("sync")]
    public async Task<IActionResult> SyncAsync(
        [FromBody] CalendarSyncRequest request,
        CancellationToken ct)
    {
        // ── Provider validation (boundary check; OWASP A03) ────────────────────────────────
        if (request.Provider is not ("Google" or "Outlook"))
        {
            return BadRequest(new { error = "Provider must be 'Google' or 'Outlook'." });
        }

        // ── Extract patientId from JWT sub claim (OWASP A01) ──────────────────────────────
        var sub = User.FindFirstValue("sub");
        if (!int.TryParse(sub, out var patientId))
            return Unauthorized();

        // ── Ownership check: booking must belong to the authenticated patient (OWASP A01) ──
        var booking = await _db.Bookings.FindAsync(new object[] { request.BookingId }, ct);
        if (booking is null)
            return NotFound();

        if (booking.PatientId != patientId)
            return StatusCode(StatusCodes.Status403Forbidden,
                new { error = "Access denied. You can only sync your own bookings." });

        // ── Enqueue — 202 returned immediately; CalendarSyncWorker handles the API call ────
        // TryWrite is safe here because the channel capacity is large (500) and this is a
        // patient-action endpoint (rate limited by JWT auth overhead). Use WriteAsync as
        // a fallback if the channel is unexpectedly full under load.
        await _channel.Writer.WriteAsync(
            new CalendarSyncCommand(request.BookingId, patientId, request.Provider),
            ct);

        return Accepted();
    }

    /// <summary>
    /// Returns the current sync status for the given booking and provider.
    /// Used by the frontend to detect <c>TokenExpired</c> after a 202 response (Edge: expired token).
    ///
    /// <para>Returns <c>{"status":"Pending"}</c> when no sync row exists yet (worker has not run).</para>
    /// <para>Returns 403 when the authenticated patient does not own the booking (OWASP A01).</para>
    /// </summary>
    [HttpGet("sync/status/{bookingId:int}")]
    public async Task<IActionResult> GetSyncStatusAsync(
        int bookingId,
        [FromQuery] string provider,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(provider))
            return BadRequest(new { error = "provider query parameter is required." });

        var sub = User.FindFirstValue("sub");
        if (!int.TryParse(sub, out var patientId))
            return Unauthorized();

        // Ownership check (OWASP A01)
        var booking = await _db.Bookings.FindAsync(new object[] { bookingId }, ct);
        if (booking is null)
            return NotFound();
        if (booking.PatientId != patientId)
            return StatusCode(StatusCodes.Status403Forbidden,
                new { error = "Access denied. You can only view sync status for your own bookings." });

        var syncRow = await _db.BookingCalendarSyncs
            .Where(s => s.BookingId == bookingId && s.Provider == provider)
            .OrderByDescending(s => s.UpdatedAt)
            .FirstOrDefaultAsync(ct);

        var status = syncRow?.Status ?? "Pending";
        return Ok(new { status });
    }
}
