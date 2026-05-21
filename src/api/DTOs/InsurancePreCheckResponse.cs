namespace Api.DTOs;

/// <summary>
/// Response for <c>GET /api/insurance/pre-check</c> (us_023; AC-001).
/// <para>
/// <see cref="Status"/> is one of the exact string literals <c>"Complete"</c>, <c>"Incomplete"</c>,
/// or <c>"Missing"</c> — the frontend in task_002 depends on these exact values.
/// </para>
/// </summary>
public sealed record InsurancePreCheckResponse(string Status);
