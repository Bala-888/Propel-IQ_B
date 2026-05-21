using Api.Infrastructure.Auth;
using Api.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>
/// Exposes patient record data with per-record ownership enforcement (AC-003).
/// The <c>[Authorize]</c> attribute at class level ensures all routes require a valid JWT;
/// <see cref="IOwnershipAuthorizationService"/> then restricts Patients to their own record
/// while Staff and Admin may access any record.
/// </summary>
[ApiController]
[Route("patients")]
[Authorize]
public sealed class PatientsController : ControllerBase
{
    private readonly IPatientRepository _patientRepo;
    private readonly IOwnershipAuthorizationService _ownershipService;
    private readonly RepeatedUnauthorizedAccessTracker _tracker;

    public PatientsController(
        IPatientRepository patientRepo,
        IOwnershipAuthorizationService ownershipService,
        RepeatedUnauthorizedAccessTracker tracker)
    {
        _patientRepo     = patientRepo;
        _ownershipService = ownershipService;
        _tracker         = tracker;
    }

    /// <summary>
    /// Returns the full patient record for <paramref name="id"/>.
    /// Patients may only retrieve their own record; Staff and Admin may retrieve any record.
    /// Returns HTTP 403 with <c>{"error":"Access denied. You can only access your own records."}</c>
    /// when a Patient requests another patient's record (AC-003).
    /// Returns HTTP 404 when no patient with <paramref name="id"/> exists.
    /// </summary>
    [HttpGet("{id:int}/view")]
    public async Task<IActionResult> GetPatientAsync(int id, CancellationToken ct)
    {
        if (!_ownershipService.CanAccessPatientRecord(User, id))
        {
            // AC-005: own-record violations count toward the per-IP 403 threshold
            // (ownership 403s are not routed through IAuthorizationMiddlewareResultHandler,
            // so the tracker must be called directly here).
            var sourceIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            await _tracker.TrackAndAlertAsync(sourceIp);

            // AC-003: exact error body as specified — no deviation in casing or punctuation.
            return StatusCode(StatusCodes.Status403Forbidden,
                new { error = "Access denied. You can only access your own records." });
        }

        var patient = await _patientRepo.GetByIdAsync(id, ct);
        if (patient is null)
            return NotFound();

        return Ok(patient);
    }
}
