namespace Api.Services;

/// <summary>
/// Configuration for the <see cref="PreferredSlotMonitorJob"/> hosted service (us_025; AC-001).
///
/// Bind from <c>appsettings.json</c> section <c>"PreferredSlotMonitor"</c> so the interval and
/// batch size are adjustable per environment without recompilation.
/// </summary>
public sealed class PreferredSlotMonitorOptions
{
    /// <summary>
    /// How often the monitor job runs, in minutes.  Default: 5.
    /// Set to 1 in test environments to verify swap behaviour quickly (AC-001 validation plan).
    /// </summary>
    public int IntervalMinutes { get; set; } = 5;

    /// <summary>
    /// Maximum number of candidates processed per batch within a single job tick.
    /// Default: 50.  Each batch runs in its own <see cref="Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction"/>
    /// to avoid long-held locks and lock escalation (Edge: 500 candidates).
    /// </summary>
    public int BatchSize { get; set; } = 50;
}
