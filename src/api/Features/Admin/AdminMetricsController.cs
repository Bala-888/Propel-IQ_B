using Api.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Features.Admin;

/// <summary>
/// Admin KPI metrics endpoint — restricted to the Admin role (us_034/AC-001, AC-004).
///
/// <para>
/// Staff and Patient roles receive HTTP 403 with a structured error body via the global
/// <c>OnForbidden</c> handler (OWASP A01). No PHI is returned — the response contains only
/// aggregate integers and a nullable double (OWASP A02; AC-001).
/// </para>
/// </summary>
[ApiController]
[Route("api/admin/metrics")]
[Authorize(Roles = Roles.Admin)]
public sealed class AdminMetricsController : ControllerBase
{
    private readonly IAdminMetricsService _metricsService;

    public AdminMetricsController(IAdminMetricsService metricsService)
    {
        _metricsService = metricsService;
    }

    /// <summary>
    /// Returns aggregate booking metrics for the specified date.
    /// </summary>
    /// <param name="date">
    ///     Calendar date to aggregate (e.g., <c>2026-05-21</c>).
    ///     If absent or unparseable, defaults to today's UTC date.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet]
    [ProducesResponseType(typeof(AdminMetricsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetMetrics([FromQuery] string? date, CancellationToken ct)
    {
        // Validate date param; default to today when absent or unparseable (AC-001; OWASP A03).
        var queryDate = DateOnly.TryParse(date, out var parsed)
            ? parsed
            : DateOnly.FromDateTime(DateTime.UtcNow.Date);

        try
        {
            var metrics = await _metricsService.GetMetricsAsync(queryDate, ct);
            return Ok(metrics);
        }
        catch (MetricsTimeoutException)
        {
            // Edge: aggregation query > 5s — no stack trace in response body (OWASP A04, A09).
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "Metrics temporarily unavailable." });
        }
    }
}
