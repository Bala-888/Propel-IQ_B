namespace Api.DTOs;

/// <summary>
/// Bound from the <c>GET /slots</c> query string (us_019; AC-001; AC-002).
/// Validated at the API boundary before any database access (OWASP A03).
/// </summary>
/// <param name="Available">
/// When <c>true</c> (default), only slots where <c>IsAvailable = true</c> are returned.
/// </param>
/// <param name="Page">1-based page index. Must be ≥ 1 (AC-002; OWASP A03).</param>
/// <param name="PageSize">
/// Number of slots per page. Must be ≥ 1 and ≤ 100 (AC-002; OWASP A03 — caps unbounded retrieval).
/// </param>
public sealed record GetSlotsQuery(
    bool Available = true,
    int  Page      = 1,
    int  PageSize  = 10
);
