namespace Api.Services;

/// <summary>
/// Result returned by <see cref="ICalendarSyncService"/> methods to communicate outcome to callers
/// without propagating exceptions (us_028; AC-005; OWASP A04).
/// </summary>
public enum SyncResult
{
    /// <summary>Calendar API call succeeded; sync row persisted.</summary>
    Success,

    /// <summary>
    /// OAuth token is expired and the refresh attempt failed (or no token exists).
    /// The frontend should display the "Calendar sync unavailable. Please reconnect your calendar
    /// in Settings." banner (Edge: token expired).
    /// </summary>
    TokenExpired,

    /// <summary>
    /// Calendar API returned a non-success response or a network error occurred.
    /// The sync row is written with <c>Status = "Failed"</c>; a retry may be pending (AC-005).
    /// </summary>
    Failed
}
