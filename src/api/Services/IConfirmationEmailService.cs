namespace Api.Services;

/// <summary>
/// Sends an appointment confirmation email with a PDF attachment (us_022).
/// </summary>
public interface IConfirmationEmailService
{
    /// <summary>
    /// Sends a confirmation email for the given booking event.
    /// </summary>
    Task SendAsync(BookingConfirmedEvent evt, CancellationToken ct = default);
}
