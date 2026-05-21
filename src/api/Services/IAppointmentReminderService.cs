using Api.Data.Entities;

namespace Api.Services;

/// <summary>
/// Dispatches appointment reminder notifications for a single booking and records the tracking timestamp.
/// Implemented by <see cref="AppointmentReminderService"/> (us_027; AC-001, AC-002).
/// </summary>
public interface IAppointmentReminderService
{
    /// <summary>
    /// Sends the reminder for <paramref name="booking"/> via every available channel (email, SMS),
    /// then persists the appropriate tracking column to prevent duplicate fires on subsequent ticks.
    /// </summary>
    /// <param name="booking">
    /// Booking entity — must be loaded with <c>Include(b => b.Patient)</c> and
    /// <c>Include(b => b.AppointmentSlot)</c> by the caller.
    /// </param>
    /// <param name="tier">
    /// Which reminder window to dispatch (24 h or 2 h).  Determines which tracking column is written.
    /// </param>
    /// <param name="ct">Cancellation token propagated from the host.</param>
    Task SendReminderAsync(Booking booking, ReminderTier tier, CancellationToken ct = default);
}
