namespace Api.Services;

/// <summary>
/// Computes and persists no-show risk scores for newly created and rescheduled bookings (us_021).
/// Implementations are scoped — one instance per DI scope created by <see cref="NoShowRiskScoringWorker"/>.
/// </summary>
public interface INoShowRiskScoringService
{
    /// <summary>
    /// Computes the no-show risk score and tier for a booking that was just created and persists
    /// both values to <c>bookings.no_show_risk_score</c> and <c>bookings.no_show_risk_tier</c>
    /// via <c>ExecuteUpdateAsync</c> (AC-001; AC-002).
    /// </summary>
    /// <param name="evt">The booking-created event carrying scheduling metadata.</param>
    /// <param name="ct">Cancellation token.</param>
    Task ComputeAndPersistAsync(BookingCreatedEvent evt, CancellationToken ct = default);

    /// <summary>
    /// Recomputes the no-show risk score and tier after a booking's slot is changed and overwrites
    /// both columns for the specified booking (Edge: reschedule recomputation).
    /// </summary>
    /// <param name="bookingId">Integer PK of the booking to recompute.</param>
    /// <param name="patientId">Patient ID — used to count historical no-shows.</param>
    /// <param name="newSlotDate">The new appointment date.</param>
    /// <param name="newSlotTime">The new appointment start time.</param>
    /// <param name="ct">Cancellation token.</param>
    Task RecomputeAsync(int bookingId, int patientId, DateOnly newSlotDate, TimeOnly newSlotTime, CancellationToken ct = default);
}
