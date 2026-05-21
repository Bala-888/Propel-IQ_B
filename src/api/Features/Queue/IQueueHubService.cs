namespace Api.Features.Queue;

/// <summary>
/// Broadcasts real-time queue events to all connected Staff/Admin SignalR clients (us_033/AC-001, AC-002).
///
/// <para>
/// All methods are fire-and-forget from the caller's perspective: any broadcast failure is swallowed
/// internally and never propagates to the caller (Edge: 0 connected clients; OWASP A04 — failure must
/// not affect the underlying data transaction).
/// </para>
/// </summary>
public interface IQueueHubService
{
    /// <summary>Broadcasts a <c>QueueEntryAdded</c> event to all connected clients (AC-001).</summary>
    Task BroadcastEntryAddedAsync(QueueEntryDto entry);

    /// <summary>Broadcasts a <c>QueueEntryUpdated</c> event to all connected clients (AC-002).</summary>
    Task BroadcastEntryUpdatedAsync(QueueEntryDto entry);
}
