using System.Security.Claims;
using Api.Constants;
using Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Features.Patients;

/// <summary>
/// Handles minimal walk-in patient creation — <c>POST /api/patients/walkin-create</c> (us_030/AC-004).
///
/// <para>
/// Restricted to <c>Staff</c> and <c>Admin</c> roles (OWASP A01).
/// <c>staffId</c> is extracted exclusively from the JWT <c>sub</c> claim (OWASP A01; checklist).
/// </para>
/// </summary>
[ApiController]
[Route("api/patients")]
[Authorize(Roles = $"{Roles.Staff},{Roles.Admin}")]
public sealed class WalkinPatientController : ControllerBase
{
    private readonly IWalkinPatientService _walkinPatientService;

    public WalkinPatientController(IWalkinPatientService walkinPatientService)
    {
        _walkinPatientService = walkinPatientService;
    }

    /// <summary>
    /// Creates a minimal patient record for a new walk-in patient.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    ///   <item>201: patient created — body contains <c>{patientId, firstName, lastName}</c>.</item>
    ///   <item>400: request body is invalid.</item>
    /// </list>
    /// </remarks>
    [HttpPost("walkin-create")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateWalkinPatient(
        [FromBody] WalkinPatientRequest request,
        CancellationToken ct)
    {
        // staffId from JWT sub claim only — never from request body (OWASP A01; checklist)
        var staffId = User.FindFirstValue("sub") ?? "unknown";

        var result = await _walkinPatientService.CreateAsync(request, staffId, ct);

        return StatusCode(StatusCodes.Status201Created, new
        {
            patientId = result.PatientId,
            firstName = result.FirstName,
            lastName  = result.LastName,
        });
    }
}
