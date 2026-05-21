using Api.DTOs;

namespace Api.Services;

/// <summary>
/// Checks the insurance record completeness for a patient (us_023; AC-001).
/// </summary>
public interface IInsurancePreCheckService
{
    /// <summary>
    /// Returns the insurance pre-check status for <paramref name="patientId"/>.
    /// Result is one of <c>"Complete"</c>, <c>"Incomplete"</c>, or <c>"Missing"</c>.
    /// </summary>
    Task<InsurancePreCheckResponse> CheckAsync(int patientId, CancellationToken ct = default);
}
