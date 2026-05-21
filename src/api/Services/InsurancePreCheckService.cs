using Api.Data;
using Api.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

/// <summary>
/// Queries the <c>InsuranceRecord</c> table to determine insurance completeness (us_023; AC-001).
///
/// <para>
/// Uses a single <c>FirstOrDefaultAsync</c> on the indexed <c>PatientId</c> column with no joins
/// to satisfy the 2-second SLA (AC-001).
/// </para>
///
/// <para>
/// Status rules:
/// <list type="bullet">
///   <item><c>"Missing"</c>   — no <c>InsuranceRecord</c> row exists for the patient.</item>
///   <item><c>"Incomplete"</c> — row exists but <c>PolicyNumber</c> is null or whitespace.</item>
///   <item><c>"Complete"</c>  — row exists with a non-empty <c>PolicyNumber</c>.</item>
/// </list>
/// </para>
/// </summary>
public sealed class InsurancePreCheckService : IInsurancePreCheckService
{
    private readonly AppDbContext _db;

    public InsurancePreCheckService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<InsurancePreCheckResponse> CheckAsync(int patientId, CancellationToken ct = default)
    {
        var record = await _db.InsuranceRecords
            .FirstOrDefaultAsync(r => r.PatientId == patientId, ct);

        if (record is null)
            return new InsurancePreCheckResponse("Missing");

        if (string.IsNullOrWhiteSpace(record.PolicyNumber))
            return new InsurancePreCheckResponse("Incomplete");

        return new InsurancePreCheckResponse("Complete");
    }
}
