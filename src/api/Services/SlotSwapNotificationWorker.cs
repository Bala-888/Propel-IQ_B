using System.Threading.Channels;

namespace Api.Services;

/// <summary>
/// Background worker that consumes <see cref="SlotSwapCompletedEvent"/> items from the bounded
/// in-process channel registered in us_025 and delegates notification delivery to
/// <see cref="ISlotSwapNotificationService"/> (us_026; AC-001).
///
/// <para>
/// A fresh DI scope is created per event so that scoped services (<c>AppDbContext</c>,
/// <c>ISlotSwapNotificationService</c>) are properly lifetime-managed without a
/// singleton-captures-scoped warning (DI lifetime correctness; OWASP A04).
/// </para>
///
/// <para>
/// Per-event <c>try/catch</c> ensures a single notification failure does not stop the worker
/// from processing subsequent events — the committed booking is never reverted on notification
/// error (Edge: both channels fail all retries).
/// </para>
/// </summary>
public sealed class SlotSwapNotificationWorker : BackgroundService
{
    private readonly Channel<SlotSwapCompletedEvent>       _channel;
    private readonly IServiceScopeFactory                  _scopeFactory;
    private readonly ILogger<SlotSwapNotificationWorker>   _logger;

    public SlotSwapNotificationWorker(
        Channel<SlotSwapCompletedEvent>      channel,
        IServiceScopeFactory                 scopeFactory,
        ILogger<SlotSwapNotificationWorker>  logger)
    {
        _channel      = channel;
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SlotSwapNotificationWorker started.");

        // ReadAllAsync completes when the channel is closed on application shutdown
        await foreach (var evt in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            await ProcessEventAsync(evt, stoppingToken);
        }

        _logger.LogInformation("SlotSwapNotificationWorker stopped.");
    }

    // ── Per-event processing ──────────────────────────────────────────────────────────────────

    private async Task ProcessEventAsync(SlotSwapCompletedEvent evt, CancellationToken ct)
    {
        try
        {
            // Fresh scope per event: AppDbContext + ISlotSwapNotificationService have scoped lifetime
            await using var scope       = _scopeFactory.CreateAsyncScope();
            var notificationService     = scope.ServiceProvider
                .GetRequiredService<ISlotSwapNotificationService>();

            await notificationService.SendAsync(evt, ct);
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown — do not log as error; re-throw to stop the loop cleanly
            throw;
        }
        catch (Exception ex)
        {
            // Per-event catch: worker continues processing the channel after a failure (AC-001)
            _logger.LogError(ex,
                "SlotSwapNotificationWorkerError: unhandled exception for NewBookingId={NewBookingId} PatientId={PatientId}",
                evt.NewBookingId, evt.PatientId);
        }
    }
}
