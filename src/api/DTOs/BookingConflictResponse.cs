namespace Api.DTOs;

/// <summary>
/// 409 response body for <c>POST /bookings</c> when the requested slot is unavailable (us_020; AC-003).
/// Includes up to three alternative available slots ordered by proximity to the contested slot's time.
/// </summary>
public sealed class BookingConflictResponse
{
    /// <summary>Human-readable error description.</summary>
    public string Error { get; init; } = string.Empty;

    /// <summary>
    /// Up to three nearest available slots — may be <c>null</c> when no alternatives exist.
    /// </summary>
    public IReadOnlyList<SlotDto>? Alternatives { get; init; }
}
