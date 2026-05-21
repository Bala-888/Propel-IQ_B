using System.Threading.Channels;
using Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Services;

/// <summary>
/// Hosted service that monitors preferred-slot designations and performs ACID slot swaps
/// when a patient's preferred slot becomes available (us_025; AC-001–AC-005).
///
/// <para>
/// Scheduling: <see cref="PeriodicTimer"/> driven by
/// <see cref="PreferredSlotMonitorOptions.IntervalMinutes"/> (default 5 min).
/// <c>PeriodicTimer</c> avoids drift compared with <c>Task.Delay</c> and is the recommended
/// approach for interval-based hosted services in .NET 8.
/// </para>
///
/// <para>
/// DI lifetime: this service is a <b>singleton</b> <see cref="BackgroundService"/>.
/// A fresh <see cref="IServiceScope"/> (and therefore a fresh <see cref="AppDbContext"/> and
/// <see cref="IPreferredSlotSwapService"/> instance) is created for every cycle tick via
/// <see cref="IServiceScopeFactory"/>, so scoped services are never held across ticks
/// (DI lifetime correctness; OWASP A04).
/// </para>
///
/// <para>
/// PHI guardrail: no PHI appears in any Serilog log field emitted by this class — only
/// opaque integer identifiers are logged (OWASP A02; HIPAA minimum-necessary).
/// </para>
/// </summary>
public sealed class PreferredSlotMonitorJob : BackgroundService
{
    private readonly IServiceScopeFactory                    _scopeFactory;
    private readonly IOptions<PreferredSlotMonitorOptions>   _options;
    private readonly Channel<SlotSwapCompletedEvent>         _slotSwapChannel;
    private readonly ILogger<PreferredSlotMonitorJob>        _logger;

    public PreferredSlotMonitorJob(
        IServiceScopeFactory                  scopeFactory,
        IOptions<PreferredSlotMonitorOptions> options,
        Channel<SlotSwapCompletedEvent>       slotSwapChannel,
        ILogger<PreferredSlotMonitorJob>      logger)
    {
        _scopeFactory    = scopeFactory;
        _options         = options;
        _slotSwapChannel = slotSwapChannel;
        _logger          = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "PreferredSlotMonitorJob started — IntervalMinutes={IntervalMinutes} BatchSize={BatchSize}",
            _options.Value.IntervalMinutes,
            _options.Value.BatchSize);

        // PeriodicTimer: tick-aligned scheduling without drift (AC-001; .NET 8 recommended pattern)
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(_options.Value.IntervalMinutes));

        // WaitForNextTickAsync returns false when stoppingToken is cancelled (graceful shutdown)
        while (!stoppingToken.IsCancellationRequested
               && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await RunCycleAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Log and continue — a single failed cycle must not stop the hosted service.
                // Individual swap failures are already handled inside RunCycleAsync.
                _logger.LogError(ex, "PreferredSlotMonitorCycleUnhandledError");
            }
        }

        _logger.LogInformation("PreferredSlotMonitorJob stopped.");
    }

    // ── Cycle execution ───────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// One complete monitor cycle: query candidates → deduplicate → batch-process swaps → log summary.
    /// </summary>
    private async Task RunCycleAsync(CancellationToken ct)
    {
        // Fresh scope per tick: prevents scoped AppDbContext / IPreferredSlotSwapService from being
        // shared across cycle ticks by the singleton BackgroundService (DI lifetime; OWASP A04).
        using var scope    = _scopeFactory.CreateScope();
        var db             = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var swapService    = scope.ServiceProvider.GetRequiredService<IPreferredSlotSwapService>();

        // ── AC-001: candidate query ───────────────────────────────────────────────────────────
        // Ordered by CreatedAt ASC so the earliest-registrant per slot is the first element when
        // deduplicating — no secondary sort is required after GroupBy (AC-003).
        // AsNoTracking: the list is read-only here; SwapAsync re-fetches with tracking + FOR UPDATE.
        var candidates = await db.PreferredSlots
            .AsNoTracking()
            .Include(ps => ps.Booking)
            .Include(ps => ps.Slot)
            .Where(ps => ps.Slot.IsAvailable && ps.Booking.Status == "Confirmed")
            .OrderBy(ps => ps.CreatedAt)
            .ToListAsync(ct);

        if (candidates.Count == 0)
        {
            _logger.LogDebug("PreferredSlotMonitorCycleComplete: no eligible candidates");
            return;
        }

        // ── AC-003: deduplicate by SlotId ─────────────────────────────────────────────────────
        // GroupBy preserves the OrderBy(CreatedAt) ordering within each group, so First() always
        // picks the earliest-registrant.  All other candidates for the same slot are skipped this
        // cycle — their preferred_slots rows are preserved and re-evaluated next tick.
        var deduplicated = candidates
            .GroupBy(ps => ps.SlotId)
            .Select(g => g.First())
            .ToList();

        int batchCount   = 0;
        int successCount = 0;
        int skippedCount = 0;
        int failedCount  = 0;

        // ── Batch loop (Edge: 500 candidates in batches of BatchSize) ─────────────────────────
        // Chunk partitions the deduplicated list without allocating extra collections.
        // Each SwapAsync call opens its own IDbContextTransaction — no single transaction spans
        // more than one swap, preventing lock escalation (Edge: 500 candidates; AC-002).
        foreach (var batch in deduplicated.Chunk(_options.Value.BatchSize))
        {
            batchCount++;

            foreach (var candidate in batch)
            {
                var result = await swapService.SwapAsync(candidate.Id, ct);

                switch (result)
                {
                    case SwapSuccess success:
                        successCount++;

                        // AC-005: enqueue notification event only after CommitAsync — never on
                        // rollback or skip (event consumer must not fire for failed swaps).
                        var evt = new SlotSwapCompletedEvent(
                            success.PatientId,
                            success.OldSlotId,
                            success.NewSlotId,
                            success.NewBookingId);

                        if (!_slotSwapChannel.Writer.TryWrite(evt))
                        {
                            // Channel at capacity — log and continue; the swap is already committed.
                            // The us_026 worker may miss this notification but the booking is correct.
                            _logger.LogWarning(
                                "SlotSwapCompletedEventDropped: channel full for PatientId={PatientId}",
                                success.PatientId);
                        }
                        break;

                    case SwapSkipped:
                        skippedCount++;
                        break;

                    case SwapFailed:
                        failedCount++;
                        break;
                }
            }
        }

        _logger.LogInformation(
            "PreferredSlotMonitorCycleComplete: Candidates={Candidates} Deduplicated={Deduplicated} Batches={Batches} Success={Success} Skipped={Skipped} Failed={Failed}",
            candidates.Count,
            deduplicated.Count,
            batchCount,
            successCount,
            skippedCount,
            failedCount);
    }
}
