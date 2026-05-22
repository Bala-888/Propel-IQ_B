using System.Data;
using Api.Constants;
using Api.Data;
using Api.Data.Entities;
using Api.Features.Conflicts;
using Api.Features.Patients;
using Api.Infrastructure.Auth;
using Api.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
// OWASP A02: PHI responses must never be cached by HTTP intermediaries
[ResponseCache(Duration = 0, NoStore = true)]
public sealed class PatientsController : ControllerBase
{
    private readonly IPatientRepository _patientRepo;
    private readonly IOwnershipAuthorizationService _ownershipService;
    private readonly RepeatedUnauthorizedAccessTracker _tracker;
    private readonly AppDbContext _db;
    private readonly ILogger<PatientsController> _logger;

    public PatientsController(
        IPatientRepository patientRepo,
        IOwnershipAuthorizationService ownershipService,
        RepeatedUnauthorizedAccessTracker tracker,
        AppDbContext db,
        ILogger<PatientsController> logger)
    {
        _patientRepo      = patientRepo;
        _ownershipService = ownershipService;
        _tracker          = tracker;
        _db               = db;
        _logger           = logger;
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

    // ── Search ─────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Prefix search across first/last name — <c>GET /patients/search?q=term</c> (us_040/AC-001).
    /// Returns ≤20 results. Rejects terms shorter than 3 characters with HTTP 400.
    /// OWASP A02: search term <paramref name="q"/> is never written to any ILogger call —
    /// it may contain partial patient names (PHI).
    /// OWASP A03: LIKE metacharacters escaped before passing to EF.Functions.ILike.
    /// </summary>
    [HttpGet("search")]
    [Authorize(Roles = $"{Roles.Staff},{Roles.Admin},{Roles.Clinician}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SearchAsync([FromQuery] string? q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 3)
            return BadRequest(new { error = "Search term must be at least 3 characters." });

        // Defence-in-depth RBAC check — [Authorize(Roles=...)] above is the primary gate (AC-004)
        if (!User.IsInRole(Roles.Staff) && !User.IsInRole(Roles.Admin) && !User.IsInRole(Roles.Clinician))
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Access denied." });

        var term = q.Trim();
        // Escape LIKE metacharacters so user-supplied %, _, \ are treated as literals (OWASP A03; AC-001)
        var escaped = term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
        var pattern = escaped + "%";

        var results = await _db.Patients
            .Where(p =>
                EF.Functions.ILike(p.LastName,  pattern, "\\") ||
                EF.Functions.ILike(p.FirstName, pattern, "\\"))
            .AsNoTracking()
            .Take(20)
            .Select(p => new PatientSearchResultDto
            {
                Id          = p.Id,
                FullName    = p.FirstName + " " + p.LastName,
                DateOfBirth = p.DateOfBirth, // decrypted by EF Core PHI value converter
                PatientCode = $"P{p.Id:D6}",
            })
            .ToListAsync(ct);

        return Ok(results);
    }

    // ── 360° Summary ───────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Aggregated 360° patient view — <c>GET /patients/{id}/summary</c> (us_040/AC-002, AC-003).
    /// Three round trips inside a ReadCommitted transaction; guarded by a 2-second CTS.
    /// OWASP A02: only patient UUID logged on error — no name, email, or entity values in logs.
    /// </summary>
    [HttpGet("{id:int}/summary")]
    [Authorize(Roles = $"{Roles.Staff},{Roles.Admin},{Roles.Clinician}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetSummaryAsync(
        int id,
        [FromQuery] int page     = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct     = default)
    {
        // Defence-in-depth RBAC — AC-004; OWASP A01
        if (!User.IsInRole(Roles.Staff) && !User.IsInRole(Roles.Admin) && !User.IsInRole(Roles.Clinician))
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Access denied." });

        // Clamp pagination params at the boundary — prevents page=0 exception and pageSize=10000 bypass (OWASP A04; Edge: 100+ docs)
        page     = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        // 2-second SLA guard — linkedCt fires on HTTP disconnect OR 2s timeout, whichever is first (AC-002; NFR-003)
        using var cts      = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var linkedCt       = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, ct).Token;

        try
        {
            // ReadCommitted transaction wraps all three round trips (AC-003; OWASP A03)
            await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, linkedCt);

            // RT1: patient demographics + entities in a single Include JOIN (AC-003 — 1 round trip)
            var patient = await _db.Patients
                .Include(p => p.PatientEntities)
                .AsNoTracking()
                .Where(p => p.Id == id)
                .FirstOrDefaultAsync(linkedCt);

            if (patient is null)
                return NotFound();

            // RT2: active (Confirmed) bookings ordered by slot start ascending (AC-002)
            // Select projects via AppointmentSlot navigation — EF Core adds the JOIN without Include (AC-003)
            var bookings = await _db.Bookings
                .AsNoTracking()
                .Where(b => b.PatientId == id && b.Status == "Confirmed")
                .OrderBy(b => b.AppointmentSlot.SlotStart)
                .Select(BookingSummaryDto.Selector)
                .ToListAsync(linkedCt);

            // RT3a: total document count for pagination metadata (AC-002)
            var totalDocs = await _db.DocumentRecords
                .CountAsync(dr => dr.PatientId == id, linkedCt);

            // RT3b: paginated document page (SKIP/TAKE — never loads all rows into change tracker; Edge: 100+ docs; OWASP A04)
            var docs = await _db.DocumentRecords
                .AsNoTracking()
                .Where(dr => dr.PatientId == id)
                .OrderByDescending(dr => dr.UploadTimestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(DocumentSummaryDto.Selector)
                .ToListAsync(linkedCt);

            // RT4: detected clinical conflicts with entity navigation (AC-004)
            // OWASP A02: conflict descriptions are PHI — never logged; returned only in the response body.
            var conflicts = await _db.ClinicalConflicts
                .AsNoTracking()
                .Where(c => c.PatientId == id)
                .Include(c => c.EntityA)
                .Include(c => c.EntityB)
                .Select(c => new ConflictDto
                {
                    Id           = c.Id,
                    ConflictType = c.ConflictType,
                    Description  = c.Description,
                    Severity     = c.Severity,
                    Status       = c.Status,
                    EntityA      = new EntitySummaryDto
                    {
                        Id    = c.EntityA.Id,
                        Type  = c.EntityA.Type,
                        Value = c.EntityA.Value,
                    },
                    EntityB      = new EntitySummaryDto
                    {
                        Id    = c.EntityB.Id,
                        Type  = c.EntityB.Type,
                        Value = c.EntityB.Value,
                    },
                })
                .ToListAsync(linkedCt);

            await tx.CommitAsync(linkedCt);

            var summary = new PatientSummaryDto
            {
                Demographics = new DemographicsDto
                {
                    Id               = patient.Id,
                    FullName         = patient.FirstName + " " + patient.LastName,
                    DateOfBirth      = patient.DateOfBirth,
                    InsuranceProvider = patient.InsuranceProvider,
                    InsuranceId      = patient.InsuranceId,
                },
                // ToArray() ensures non-null collection even when PatientEntities is empty (Edge: no entities; AC-002)
                Entities = patient.PatientEntities
                    .Select(pe => new EntityDto
                    {
                        Id           = pe.Id,
                        Type         = pe.Type,
                        Value        = pe.Value, // PHI: never log this field (OWASP A02)
                        Confidence   = (float)pe.Confidence,
                        LowConfidence = pe.LowConfidence,
                        LastSeenAt   = pe.LastSeenAt,
                    })
                    .ToArray(),
                ActiveBookings     = bookings.ToArray(),
                Documents          = docs.ToArray(),
                TotalDocumentCount = totalDocs,
                CurrentPage        = page,
                PageSize           = pageSize,
                Conflicts          = conflicts.ToArray(),
            };

            return Ok(summary);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            // 2-second SLA exceeded — only log the patient ID, never PHI (OWASP A02; AC-002)
            _logger.LogWarning("PatientSummaryTimeout for {PatientId}", id);
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "Summary temporarily unavailable." });
        }
    }
}
