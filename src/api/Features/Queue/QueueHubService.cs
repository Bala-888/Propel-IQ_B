using System.Diagnostics;
using Api.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Api.Features.Queue;

/// <summary>
/// Implements <see cref="IQueueHubService"/> using <see cref="IHubContext{QueueHub}"/> for
/// server-initiated SignalR broadcasts (us_033/AC-001, AC-002).
///
/// <para>
/// Each broadcast is measured with a <see cref="Stopwatch"/>; a <see cref="LogLevel.Warning"/> entry
/// is emitted if the latency exceeds the 2-second SLA (Edge: lag > 2s under load).
/// All exceptions are swallowed to ensure broadcast failures cannot propagate to the caller and
/// affect the underlying data transaction (Edge: 0 connected clients; OWASP A04).
/// </para>
/// </summary>
public sealed class QueueHubService : IQueueHubService
{
    private readonly IHubContext<QueueHub>     _hub;
    private readonly ILogger<QueueHubService>  _logger;

    public QueueHubService(IHubContext<QueueHub> hub, ILogger<QueueHubService> logger)
    {
        _hub    = hub;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task BroadcastEntryAddedAsync(QueueEntryDto entry)
        => BroadcastAsync("QueueEntryAdded", entry);

    /// <inheritdoc />
    public Task BroadcastEntryUpdatedAsync(QueueEntryDto entry)
        => BroadcastAsync("QueueEntryUpdated", entry);

    private async Task BroadcastAsync(string eventType, QueueEntryDto entry)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await _hub.Clients.All.SendAsync(eventType, entry);
        }
        catch (Exception ex)
        {
            // Swallow: a failed push must never propagate to the caller's data transaction
            // (Edge: 0 connected clients; OWASP A04 — availability must not block commit).
            _logger.LogWarning(ex, "QueueHub broadcast failed for {EventType}", eventType);
        }
        finally
        {
            sw.Stop();
        }

        // SLA check outside try/catch so it does not mask the exception path above (Edge: lag > 2s).
        if (sw.ElapsedMilliseconds > 2_000)
        {
            _logger.LogWarning(
                "QueueHub broadcast latency {LatencyMs}ms exceeds 2-second SLA for {EventType}",
                sw.ElapsedMilliseconds, eventType);
        }
    }
}
