namespace Api.Services;

/// <summary>
/// Orchestrates calendar event creation, update, and deletion for confirmed bookings
/// (us_028; AC-001–AC-005).
///
/// <para>
/// All methods return <see cref="SyncResult"/> instead of throwing — calendar failures are fully
/// isolated from booking state (AC-005; OWASP A04).
/// </para>
/// </summary>
public interface ICalendarSyncService
{
    /// <summary>
    /// Creates a calendar event for <paramref name="bookingId"/> on the specified
    /// <paramref name="provider"/> using the patient's stored OAuth token.
    /// Persists the <c>booking_calendar_syncs</c> row with the returned
    /// <c>ExternalEventId</c> on success, or <c>Status = "Failed"</c> on error (AC-001, AC-002, AC-005).
    /// </summary>
    Task<SyncResult> SyncAsync(
        int               bookingId,
        int               patientId,
        string            provider,
        CancellationToken ct = default);

    /// <summary>
    /// Issues a PATCH to the existing calendar event for <paramref name="bookingId"/>
    /// with updated start/end times from the rescheduled slot.
    /// No new event is created if a <c>booking_calendar_syncs</c> row with
    /// <c>Status = "Synced"</c> already exists (AC-003).
    /// </summary>
    Task<SyncResult> UpdateAsync(
        int               bookingId,
        string            provider,
        CancellationToken ct = default);

    /// <summary>
    /// Issues a DELETE to the calendar event for <paramref name="bookingId"/> and sets
    /// <c>booking_calendar_syncs.Status = "Deleted"</c> (AC-004).
    /// Returns <see cref="SyncResult.Success"/> immediately when no sync row is found
    /// (idempotent — safe to call on uncalendared bookings).
    /// </summary>
    Task<SyncResult> DeleteAsync(
        int               bookingId,
        string            provider,
        CancellationToken ct = default);
}
