namespace Api.Features.Admin;

/// <summary>
/// Service contract for the admin KPI metrics endpoint (us_034/AC-001).
/// </summary>
public interface IAdminMetricsService
{
    /// <summary>
    /// Returns aggregate booking metrics for <paramref name="date"/>.
    /// </summary>
    /// <exception cref="MetricsTimeoutException">
    ///     Thrown when the aggregation query exceeds the 5-second safety timeout (Edge: query > 5s).
    /// </exception>
    Task<AdminMetricsDto> GetMetricsAsync(DateOnly date, CancellationToken ct = default);
}
