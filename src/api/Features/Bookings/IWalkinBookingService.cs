using Api.DTOs;

namespace Api.Features.Bookings;

/// <summary>Result discriminated union for <see cref="IWalkinBookingService.CreateAsync"/>.</summary>
public abstract class WalkinBookingResult { }

public sealed class WalkinBookingSuccess : WalkinBookingResult
{
    public int BookingId      { get; init; }
    public string Status      { get; init; } = "Confirmed";
    public int QueuePosition  { get; init; }
}

public sealed class WalkinBookingDuplicateToday : WalkinBookingResult
{
    public int ExistingBookingId { get; init; }
}

public sealed class WalkinBookingSlotUnavailable : WalkinBookingResult { }

public sealed class WalkinBookingPatientNotFound : WalkinBookingResult { }

public sealed class WalkinBookingLockTimeout : WalkinBookingResult { }

/// <summary>
/// Handles <c>POST /api/bookings/walkin</c> with ACID slot lock, duplicate-today guard,
/// and computed queue position (us_030/AC-003; OWASP A01, A04).
/// </summary>
public interface IWalkinBookingService
{
    /// <summary>
    /// Creates a confirmed walk-in booking inside an <c>IDbContextTransaction</c>.
    /// Performs duplicate-today check, acquires a <c>SELECT … FOR UPDATE</c> slot lock,
    /// writes the booking row, and returns the computed queue position.
    /// </summary>
    /// <param name="request">Validated walk-in booking payload.</param>
    /// <param name="staffId">JWT sub claim of the acting staff member — never from request body (OWASP A01).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<WalkinBookingResult> CreateAsync(
        WalkinBookingRequest request,
        string staffId,
        CancellationToken ct = default);
}
