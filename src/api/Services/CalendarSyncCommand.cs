namespace Api.Services;

/// <summary>
/// Command enqueued to <c>Channel&lt;CalendarSyncCommand&gt;</c> by <c>CalendarSyncController</c>
/// and consumed by <see cref="CalendarSyncWorker"/> (us_028; AC-005; Edge: 202 before sync completes).
/// </summary>
/// <param name="BookingId">ID of the booking to sync.</param>
/// <param name="PatientId">Patient who owns the booking — required for OAuth token look-up.</param>
/// <param name="Provider">"Google" or "Outlook".</param>
/// <param name="RetryCount">
/// Number of times this command has been retried after a <see cref="SyncResult.Failed"/> outcome.
/// Worker re-enqueues up to 3 times before discarding (AC-005).
/// </param>
public sealed record CalendarSyncCommand(
    int    BookingId,
    int    PatientId,
    string Provider,
    int    RetryCount = 0);
