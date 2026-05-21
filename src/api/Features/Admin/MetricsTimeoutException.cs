namespace Api.Features.Admin;

/// <summary>
/// Thrown by <see cref="AdminMetricsService.GetMetricsAsync"/> when the aggregation query
/// exceeds the 5-second timeout (Edge: query > 5s; us_034/AC-001).
/// </summary>
public sealed class MetricsTimeoutException : Exception
{
    public MetricsTimeoutException()
        : base("Metrics aggregation query exceeded the 5-second safety timeout.") { }
}
