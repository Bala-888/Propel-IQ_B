namespace Api.Services;

/// <summary>
/// Sends slot-swap update notifications (email + SMS) for a committed preferred-slot swap
/// (us_026; AC-001–AC-004).
/// </summary>
public interface ISlotSwapNotificationService
{
    /// <summary>
    /// Loads the new booking from the database, checks per-channel opt-outs, generates the
    /// updated confirmation PDF, and dispatches email and SMS concurrently (AC-002).
    ///
    /// <para>
    /// All notification content is derived from the fresh DB record identified by
    /// <see cref="SlotSwapCompletedEvent.NewBookingId"/> — event payload fields are used only
    /// for routing and logging (Edge: payload freshness; AC-001).
    /// </para>
    ///
    /// <para>
    /// Notification failure never reverts the committed booking.  All exceptions are caught
    /// and logged inside the implementation so the caller (worker) can safely continue with
    /// the next event (Edge: both channels fail all retries).
    /// </para>
    /// </summary>
    /// <param name="evt">Domain event emitted by <c>PreferredSlotMonitorJob</c> after a successful swap commit.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SendAsync(SlotSwapCompletedEvent evt, CancellationToken ct = default);
}
