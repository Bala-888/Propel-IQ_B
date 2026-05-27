using System.Security.Claims;
using Api.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Api.Features.Patients;

/// <summary>
/// Patient typeahead search for front-desk staff — <c>GET /api/patients/search</c> (us_030/AC-002).
///
/// <para>
/// Restricted to <c>Staff</c> and <c>Admin</c> roles (OWASP A01).
/// The <c>q</c> parameter must be at least 3 characters; shorter values are rejected at the
/// controller boundary without touching the database (OWASP A03; AC-002 SLA guard).
/// </para>
/// <para>
/// Search covers <c>first_name</c> and <c>last_name</c> only. The <c>email</c> column is stored
/// as <c>bytea</c> ciphertext via PHI value converters and cannot be searched with <c>ILIKE</c>
/// without decrypting every row — omitted by design (OWASP A02; DR-001).
/// </para>
/// </summary>
[ApiController]
[Route("patients")]
[Authorize(Roles = $"{Roles.Staff},{Roles.Admin}")]
public sealed class PatientSearchController : ControllerBase
{
    private readonly AppDbContext _db;

    public PatientSearchController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Returns up to 10 patients whose first or last name matches <paramref name="q"/>.
    /// </summary>
    /// <param name="q">Search term — minimum 3 characters (OWASP A03; AC-002).</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("search")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Search([FromQuery] string? q, CancellationToken ct)
    {
        // Input validation at the controller boundary — no DB query for under-length input (OWASP A03; AC-002)
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 3)
            return BadRequest(new { error = "Search term must be at least 3 characters." });

        var term = q.Trim();
        var pattern = $"%{term}%";

        // ILIKE uses a parameterised EF Core call — the pattern value is never interpolated
        // into raw SQL, preventing SQL injection (OWASP A03).
        var results = await _db.Patients
            .Where(p =>
                EF.Functions.ILike(p.FirstName, pattern) ||
                EF.Functions.ILike(p.LastName,  pattern))
            .OrderBy(p => p.LastName)
            .ThenBy(p => p.FirstName)
            .Take(10)
            .Select(p => new
            {
                patientId   = p.Id,
                firstName   = p.FirstName,
                lastName    = p.LastName,
                dateOfBirth = p.DateOfBirth, // decrypted transparently by EF value converter
            })
            .ToListAsync(ct);

        return Ok(results);
    }
}
