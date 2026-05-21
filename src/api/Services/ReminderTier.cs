namespace Api.Services;

/// <summary>
/// Identifies which reminder window is being dispatched (us_027; AC-001, AC-002).
/// </summary>
public enum ReminderTier
{
    /// <summary>Sent ~24 hours before the appointment (AC-001).</summary>
    TwentyFourHour,

    /// <summary>Sent ~2 hours before the appointment (AC-002).</summary>
    TwoHour
}
