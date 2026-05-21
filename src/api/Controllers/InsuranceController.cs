using System.Security.Claims;
using Api.Audit;
using Api.Constants;
using Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>
/// Exposes insurance pre-check for the authenticated patient (us_023; AC-001).
///
/// <para>
/// All routes require the <c>Patient</c> JWT role claim — Staff and Admin receive 403 (OWASP A01).
/// The <c>patientId</c> is sourced exclusively from the JWT sub claim; no query-string
/// parameter is exposed, preventing cross-patient enumeration (OWASP A01; A07).
/// </para>
/// </summary>
[ApiController]
[Route("api/insurance")]
[Authorize(Roles = Roles.Patient)]
public sealed class InsuranceController : ControllerBase
{
    private readonly IInsurancePreCheckService      _preCheckService;
    private readonly Api.Audit.IAuditLogger         _auditLogger;

    public InsuranceController(
        IInsurancePreCheckService  preCheckService,
        Api.Audit.IAuditLogger     auditLogger)
    {
        _preCheckService = preCheckService;
        _auditLogger     = auditLogger;
    }

    /// <summary>
    /// Returns the insurance pre-check status for the authenticated patient.
    ///
    /// <para>Response body: <c>{ "status": "Complete" | "Incomplete" | "Missing" }</c></para>
    ///
    /// <para>
    /// <c>patientId</c> is sourced from the JWT <c>sub</c> claim only — the query string does not
    /// accept or expose a <c>patientId</c> parameter (OWASP A01; A07; AC-001 — 2s SLA).
    /// </para>
    ///
    /// <para>An audit log entry is written for every call regardless of outcome (AC-004).</para>
    /// </summary>
    [HttpGet("pre-check")]
    public async Task<IActionResult> PreCheckAsync(CancellationToken ct)
    {
        // patientId is always sourced from the JWT sub claim — no query-string parameter (OWASP A01; A07)
        var subClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? User.FindFirstValue("sub");

        if (!int.TryParse(subClaim, out var patientId))
            return Unauthorized(new { error = "Invalid or missing patient identity claim." });

        var response = await _preCheckService.CheckAsync(patientId, ct);

        // Audit every pre-check call — all three outcomes logged (AC-004; OWASP A09)
        var actorRole = User.FindFirstValue(ClaimTypes.Role)
                     ?? User.FindFirstValue("role")
                     ?? Roles.Patient;

        await _auditLogger.RecordAsync(new AuditEntry(
            ActorId:      patientId.ToString(),
            ActorRole:    actorRole,
            ActionType:   AuditActionTypes.InsurancePreCheck,
            ResourceType: "InsuranceRecord",
            ResourceId:   patientId.ToString(),
            IpAddress:    HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            UserAgent:    Request.Headers.UserAgent.ToString(),
            OccurredAt:   DateTime.UtcNow), ct);

        return Ok(response);
    }
}
