using Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Api.BackgroundServices;

/// <summary>
/// Scans the <c>document_records</c> table every 30 seconds and transitions any document that
/// has not reached <c>EntitiesExtracted</c> within 120 seconds of upload to <c>TimedOut</c>
/// (us_039/AC-002, AC-003; SCR-011).
///
/// <para>
/// Design decisions:
/// <list type="bullet">
///   <item>
///     Runs in its own DI scope per tick — prevents long-lived DbContext accumulating state
///     (EF Core best-practice for hosted services).
///   </item>
///   <item>
///     Uses <see cref="Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ExecuteUpdateAsync"/>
///     — single round-trip UPDATE, no entity tracking overhead (AC-002; checklist).
///   </item>
///   <item>
///     Terminal states excluded from timeout: <c>EntitiesExtracted</c>, <c>TimedOut</c>,
///     <c>ExtractionFailed</c> — prevents double-marking or masking real failure causes.
///   </item>
/// </list>
/// </para>
/// </summary>
public sealed class PipelineGuardianWorker : BackgroundService
{
    private static readonly string[] TerminalStatuses = ["EntitiesExtracted", "TimedOut", "ExtractionFailed"];
    private static readonly TimeSpan  PollInterval     = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan  SlaThreshold     = TimeSpan.FromSeconds(120);

    private readonly IServiceScopeFactory         _scopeFactory;
    private readonly ILogger<PipelineGuardianWorker> _logger;

    public PipelineGuardianWorker(
        IServiceScopeFactory            scopeFactory,
        ILogger<PipelineGuardianWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "PipelineGuardianWorker started. poll={PollSeconds}s sla={SlaSeconds}s",
            PollInterval.TotalSeconds, SlaThreshold.TotalSeconds);

        using var timer = new PeriodicTimer(PollInterval);

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await RunCycleAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "PipelineGuardianWorker tick error");
            }
        }

        _logger.LogInformation("PipelineGuardianWorker stopped");
    }

    /// <summary>
    /// Single guardian cycle: finds documents that exceed the SLA threshold and are not in a
    /// terminal state, then batch-updates them to <c>TimedOut</c> and logs each one.
    /// (AC-002; AC-003; checklist: logs document_id + previous status)
    /// </summary>
    private async Task RunCycleAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var threshold = DateTimeOffset.UtcNow.Subtract(SlaThreshold);

        // Collect IDs + statuses before mutating so we can log each one (AC-003; checklist)
        var stale = await db.DocumentRecords
            .Where(dr => !TerminalStatuses.Contains(dr.Status)
                      && dr.ProcessingStartedAt < threshold)
            .Select(dr => new { dr.Id, dr.Status })
            .ToListAsync(ct);

        if (stale.Count == 0)
            return;

        // Batch UPDATE in a single round-trip (AC-002; no entity-tracking overhead; checklist)
        var updatedCount = await db.DocumentRecords
            .Where(dr => !TerminalStatuses.Contains(dr.Status)
                      && dr.ProcessingStartedAt < threshold)
            .ExecuteUpdateAsync(
                s => s.SetProperty(dr => dr.Status, "TimedOut"),
                ct);

        foreach (var doc in stale)
        {
            _logger.LogError(
                "Document {DocumentId} timed out: previous_status={Status} threshold_utc={Threshold:O}",
                doc.Id, doc.Status, threshold);
        }

        _logger.LogInformation(
            "PipelineGuardianWorker marked {Count} document(s) as TimedOut",
            updatedCount);
    }
}
