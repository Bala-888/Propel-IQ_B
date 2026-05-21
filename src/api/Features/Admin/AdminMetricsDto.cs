namespace Api.Features.Admin;

/// <summary>
/// Aggregate booking metrics for the requested calendar day (us_034/AC-001).
///
/// <para>
/// All fields are aggregate integers or a nullable double — no patient names, IDs, or PHI are
/// included in this DTO (OWASP A02 — data minimisation; audit-safe broadcast payload).
/// </para>
/// </summary>
public sealed record AdminMetricsDto(
    int     TotalBookings,
    int     Confirmed,
    int     Cancelled,
    int     WalkIns,
    double? AverageWaitMinutes,
    int     HighRisk,
    int     MediumRisk,
    int     LowRisk);
