namespace Api.Features.Queue;

/// <summary>
/// Response DTO for <c>PATCH /api/queue/{bookingId}/arrived</c> (us_032/AC-002).
///
/// Returned for both the first-call transition and the idempotent re-call so the
/// frontend can unconditionally update its row state without branching on HTTP status.
/// </summary>
/// <param name="Status">Always <c>"CheckedIn"</c> for a successful mark-arrived call.</param>
/// <param name="ArrivedAt">
///     Server-assigned UTC timestamp of check-in.
///     On the idempotent path this is the original <c>Booking.CheckedInAt</c> value, unchanged (AC-004).
/// </param>
public sealed record ArrivedResponseDto(string Status, DateTimeOffset ArrivedAt);
