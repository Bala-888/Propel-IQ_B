namespace Api.DTOs;

/// <summary>
/// Slot response object — contains only schedule data (us_019; OWASP A02; HIPAA).
/// No patient identifiers are included: <c>BookedByPatientId</c> and any patient-linked
/// foreign key are deliberately excluded to prevent PHI exposure in slot listings.
/// </summary>
/// <param name="Id">Slot primary key.</param>
/// <param name="Date">Calendar date derived from <c>SlotStart</c>.</param>
/// <param name="StartTime">Start time of day derived from <c>SlotStart</c>.</param>
/// <param name="DurationMinutes">
/// Slot duration in minutes computed from <c>SlotEnd − SlotStart</c>.
/// </param>
/// <param name="Status">
/// Human-readable availability label — <c>"Available"</c> when <c>IsAvailable = true</c>,
/// <c>"Booked"</c> otherwise.
/// </param>
/// <param name="ProviderName">Optional provider name; not PHI.</param>
public sealed record SlotDto(
    int       Id,
    DateOnly  Date,
    TimeOnly  StartTime,
    int       DurationMinutes,
    string    Status,
    string?   ProviderName
);

/// <summary>
/// Pagination metadata returned alongside every <c>GET /slots</c> response (AC-002).
/// Always present — even when <c>Total = 0</c> (Edge: no available slots).
/// </summary>
/// <param name="Total">Total number of slots matching the filter.</param>
/// <param name="Page">Current 1-based page index.</param>
/// <param name="PageSize">Maximum slots per page.</param>
/// <param name="TotalPages">
/// Total number of pages; computed as <c>Ceiling(Total / PageSize)</c>.
/// Zero when <c>Total = 0</c> (Edge: no available slots).
/// </param>
public sealed record PaginationMeta(
    int Total,
    int Page,
    int PageSize,
    int TotalPages
);

/// <summary>
/// Top-level response envelope for <c>GET /slots</c> (AC-001; AC-002).
/// </summary>
/// <param name="Slots">Page of matching slot DTOs. Empty list when none match (not 404).</param>
/// <param name="Pagination">Pagination metadata; always present.</param>
public sealed record SlotsResponse(
    IReadOnlyList<SlotDto> Slots,
    PaginationMeta         Pagination
);
