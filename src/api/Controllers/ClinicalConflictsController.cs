using System.Security.Claims;
using Api.Audit;
using Api.Constants;
using Api.Data;
using Api.Features.Conflicts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

/// <summary>
/// Exposes conflict lifecycle operations — currently the resolve/dismiss mutation (us_042/AC-002, AC-003).
///
/// <para>
/// All routes require Staff, Admin, or Clinician JWT role claims (OWASP A01).
/// The <c>resolved_by</c> field is sourced exclusively from the JWT sub claim — never from the
/// request body — preventing cross-user impersonation attacks (OWASP A01; AC-002).
/// </para>
///
/// <para>
/// <c>[ResponseCache(Duration = 0, NoStore = true)]</c> ensures no proxy or browser caches a
/// response that contains or implies PHI (HIPAA minimum-necessary; OWASP A02).
/// </para>
/// </summary>
[ApiController]
[Route("clinical-conflicts")]
[Authorize(Roles = $"{Roles.Staff},{Roles.Admin},{Roles.Clinician}")]
[ResponseCache(Duration = 0, NoStore = true)]
public sealed class ClinicalConflictsController : ControllerBase
{
    private static readonly HashSet<string> ValidResolutions =
        new(StringComparer.Ordinal) { "Resolved", "Dismissed" };

    private readonly AppDbContext                          _db;
    private readonly Api.Audit.IAuditLogger                _auditLogger;
    private readonly ILogger<ClinicalConflictsController>  _logger;

    public ClinicalConflictsController(
        AppDbContext                          db,
        Api.Audit.IAuditLogger                auditLogger,
        ILogger<ClinicalConflictsController>  logger)
    {
        _db          = db;
        _auditLogger = auditLogger;
        _logger      = logger;
    }

    /// <summary>
    /// Resolves or dismisses an open clinical conflict.
    ///
    /// <para>
    /// Returns HTTP 200 with no body on success.
    /// Returns HTTP 400 if resolution value is invalid, note exceeds 1,000 chars,
    /// or note is absent when resolution = "Resolved".
    /// Returns HTTP 404 when the conflict does not exist.
    /// Returns HTTP 409 when the conflict is already resolved or dismissed.
    /// </para>
    ///
    /// <para>
    /// OWASP A04 TOCTOU guard: <c>ExecuteUpdateAsync</c> includes a <c>WHERE status = 'Open'</c>
    /// predicate; a zero-row update signals a concurrent resolution and returns 409.
    /// </para>
    /// </summary>
    [HttpPatch("{id:guid}/resolve")]
    public async Task<IActionResult> ResolveAsync(
        Guid                    id,
        [FromBody] ResolveConflictRequest request,
        CancellationToken       ct)
    {
        // ── 1. Extract actor identity from JWT (OWASP A01 — never from request body) ──────────
        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? User.FindFirstValue("sub")
                   ?? "unknown";
        var actorRole = User.FindFirstValue(ClaimTypes.Role)
                     ?? User.FindFirstValue("role")
                     ?? string.Empty;

        // ── 2. Validate resolution enum before any DB round-trip (OWASP A03) ─────────────────
        if (!ValidResolutions.Contains(request.Resolution))
            return BadRequest(new { error = "Resolution must be 'Resolved' or 'Dismissed'." });

        // ── 3. Note length cap — defence-in-depth after [MaxLength(1000)] annotation (OWASP A03)
        if (request.Note?.Length > 1000)
            return BadRequest(new { error = "Resolution note must be 1,000 characters or fewer." });

        // ── 4. Note required when marking as Resolved (AC-002) ───────────────────────────────
        if (request.Resolution == "Resolved" && string.IsNullOrWhiteSpace(request.Note))
            return BadRequest(new { error = "Resolution note is required when marking as Resolved." });

        // ── 5. Parse resolved_by from JWT sub claim — NOT from request body (OWASP A01) ──────
        int? resolvedBy = int.TryParse(actorId, out var parsedId) ? parsedId : null;

        // ── 6. Load conflict — 404 if missing ────────────────────────────────────────────────
        var conflict = await _db.ClinicalConflicts.FindAsync([id], ct);
        if (conflict is null)
            return NotFound(new { error = $"Clinical conflict {id} not found." });

        // ── 7. Optimistic open-status check (fast path before ExecuteUpdateAsync) ─────────────
        if (conflict.Status != "Open")
            return Conflict(new { error = "This conflict has already been resolved or dismissed." });

        // ── 8. Atomic update with TOCTOU guard: WHERE id=? AND status='Open' (OWASP A04) ─────
        var updated = await _db.ClinicalConflicts
            .Where(cc => cc.Id == id && cc.Status == "Open")
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(cc => cc.Status,         request.Resolution)
                    .SetProperty(cc => cc.ResolvedBy,     resolvedBy)
                    .SetProperty(cc => cc.ResolvedAt,     DateTimeOffset.UtcNow)
                    .SetProperty(cc => cc.ResolutionNote, request.Note),
                ct);

        // Zero rows updated means a concurrent request resolved it between steps 7 and 8
        if (updated == 0)
            return Conflict(new { error = "This conflict has already been resolved or dismissed." });

        // ── 9. Audit: conflictId + actionType only — resolution note is PHI and must not appear
        //       in any ILogger or Serilog sink (OWASP A02; HIPAA minimum-necessary §164.312(b)) ──
        var actionType = request.Resolution == "Resolved"
            ? AuditActionTypes.ConflictResolved
            : AuditActionTypes.ConflictDismissed;

        await _auditLogger.RecordAsync(new AuditEntry(
            ActorId:      actorId,
            ActorRole:    actorRole,
            ActionType:   actionType,
            ResourceType: "ClinicalConflict",
            ResourceId:   id.ToString(),
            IpAddress:    HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            UserAgent:    Request.Headers.UserAgent.ToString(),
            OccurredAt:   DateTime.UtcNow), ct);

        // Structured log: only IDs and action type — no PHI (OWASP A02)
        _logger.LogInformation(
            "Clinical conflict {ConflictId} {ActionType} by actor {ActorId}",
            id, actionType, actorId);

        return Ok();
    }
}
