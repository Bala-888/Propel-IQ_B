namespace Api.Features.Queue;

/// <summary>
/// Service contract for the same-day queue dashboard (us_031/AC-001).
/// </summary>
public interface IQueueService
{
    /// <summary>
    /// Returns all <c>Confirmed</c> and <c>CheckedIn</c> bookings for <paramref name="date"/>,
    /// ordered by <c>BookedAt</c> ascending, projected to <see cref="QueueEntryDto"/>.
    /// </summary>
    /// <param name="date">Calendar date to query; typically today.</param>
    /// <param name="since">
    ///     Optional reconnection cursor (us_033/AC-004). When provided, only entries created or
    ///     updated at or after this timestamp are returned so reconnecting clients can replay
    ///     missed events without re-fetching the entire queue.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<QueueEntryDto>> GetQueueAsync(DateOnly date, DateTimeOffset? since = null, CancellationToken ct = default);

    /// <summary>
    /// Marks the booking identified by <paramref name="bookingId"/> as arrived (Status = <c>"CheckedIn"</c>).
    /// Sets <c>CheckedInAt = DateTimeOffset.UtcNow</c> server-side only (AC-003).
    /// Idempotent: if already <c>"CheckedIn"</c>, returns 200 with existing <c>CheckedInAt</c> without a DB write (AC-004).
    /// </summary>
    /// <param name="bookingId">Integer PK of the target <c>Booking</c> row.</param>
    /// <param name="staffId">JWT <c>sub</c> claim of the acting staff member (OWASP A01).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    ///     <see cref="ArrivedResponseDto"/> on success; <c>null</c> if <paramref name="bookingId"/> is not found (controller returns 404).
    /// </returns>
    Task<ArrivedResponseDto?> MarkArrivedAsync(int bookingId, string staffId, CancellationToken ct = default);
}
