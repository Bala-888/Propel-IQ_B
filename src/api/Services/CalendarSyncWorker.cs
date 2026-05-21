using System.Threading.Channels;

namespace Api.Services;

/// <summary>
/// Background worker that consumes <see cref="CalendarSyncCommand"/> items from the bounded
/// in-process channel and delegates to <see cref="ICalendarSyncService.SyncAsync"/> on a fresh
/// DI scope per message (us_028; AC-005; Edge: 202 before sync completes).
///
/// <para>
/// <b>Retry</b>: on <see cref="SyncResult.Failed"/>, the command is re-enqueued with an
/// incremented <see cref="CalendarSyncCommand.RetryCount"/>. After 3 failures, the command is
/// discarded and the <c>booking_calendar_syncs</c> row retains <c>Status = "Failed"</c> for
/// operational monitoring (AC-005).
/// </para>
///
/// <para>
/// <b>Scope isolation</b>: a fresh <see cref="IServiceScope"/> is created per command so each
/// dispatch resolves its own <see cref="Microsoft.EntityFrameworkCore.DbContext"/> instance
/// (OWASP A04; same pattern as <see cref="SlotSwapNotificationWorker"/>).
/// </para>
/// </summary>
public sealed class CalendarSyncWorker : BackgroundService
{
    private readonly Channel<CalendarSyncCommand>       _channel;
    private readonly IServiceScopeFactory               _scopeFactory;
    private readonly ILogger<CalendarSyncWorker>        _logger;

    private const int MaxRetries = 3;

    public CalendarSyncWorker(
        Channel<CalendarSyncCommand>  channel,
        IServiceScopeFactory          scopeFactory,
        ILogger<CalendarSyncWorker>   logger)
    {
        _channel      = channel;
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("CalendarSyncWorker started.");

        await foreach (var command in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            await ProcessCommandAsync(command, stoppingToken);
        }
    }

    private async Task ProcessCommandAsync(CalendarSyncCommand command, CancellationToken ct)
    {
        await using var scope   = _scopeFactory.CreateAsyncScope();
        var service             = scope.ServiceProvider.GetRequiredService<ICalendarSyncService>();

        SyncResult result;
        try
        {
            result = await service.SyncAsync(command.BookingId, command.PatientId, command.Provider, ct);
        }
        catch (OperationCanceledException)
        {
            throw; // propagate graceful-shutdown signal
        }
        catch (Exception ex)
        {
            // Unexpected error outside the service's own catch blocks — still isolated (AC-005)
            _logger.LogError(ex,
                "CalendarSyncWorkerError: unhandled exception for BookingId={BookingId} Provider={Provider}",
                command.BookingId, command.Provider);
            result = SyncResult.Failed;
        }

        if (result == SyncResult.Failed && command.RetryCount < MaxRetries)
        {
            var retryCommand = command with { RetryCount = command.RetryCount + 1 };
            if (!_channel.Writer.TryWrite(retryCommand))
            {
                _logger.LogWarning(
                    "CalendarSyncRetryDropped: channel full. BookingId={BookingId} Provider={Provider} Attempt={Attempt}",
                    command.BookingId, command.Provider, retryCommand.RetryCount);
            }
            else
            {
                _logger.LogInformation(
                    "CalendarSyncRetryEnqueued: BookingId={BookingId} Provider={Provider} Attempt={Attempt}/{Max}",
                    command.BookingId, command.Provider, retryCommand.RetryCount, MaxRetries);
            }
        }
        else if (result == SyncResult.Failed)
        {
            _logger.LogError(
                "CalendarSyncFailed: all {Max} retry attempts exhausted. " +
                "BookingId={BookingId} Provider={Provider}",
                MaxRetries, command.BookingId, command.Provider);
        }
    }
}
