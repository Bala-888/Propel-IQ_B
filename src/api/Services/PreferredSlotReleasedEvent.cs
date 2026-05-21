namespace Api.Services;

/// <summary>
/// Raised when a confirmed booking is cancelled and its preferred slot registration is released
/// (us_024 Edge: booking cancelled; upstream hook for the us_025 monitoring worker).
/// </summary>
public sealed record PreferredSlotReleasedEvent(int BookingId);
