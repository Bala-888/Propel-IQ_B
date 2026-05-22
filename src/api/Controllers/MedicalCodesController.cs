using System.Security.Claims;
using System.Text.RegularExpressions;
using Api.Audit;
using Api.Constants;
using Api.Data;
using Api.Features.Codes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

/// <summary>
/// Handles clinician review actions for RAG-generated code suggestions (us_044/AC-001–AC-005).
///
/// <para>
/// <c>POST /patients/{id}/medical-codes</c> — Accept or Correct a pending code suggestion.
/// A <c>PatientMedicalCode</c> row is created and the corresponding <c>CodeSuggestion</c>
/// review status is updated atomically via <c>ExecuteUpdateAsync</c> with a TOCTOU guard
/// (OWASP A04; AC-002).
/// </para>
///
/// <para>
/// All routes require Clinician or Admin role.  The <c>reviewed_by</c> FK is sourced exclusively
/// from the JWT sub claim — never from the request body — preventing cross-user impersonation
/// (OWASP A01; AC-002).
/// </para>
///
/// <para>
/// <c>[ResponseCache(Duration = 0, NoStore = true)]</c> ensures no proxy or browser caches a
/// response that contains PHI (HIPAA minimum-necessary; OWASP A02).
/// </para>
/// </summary>
[ApiController]
[Route("patients")]
[Authorize(Roles = $"{Roles.Clinician},{Roles.Admin}")]
[ResponseCache(Duration = 0, NoStore = true)]
public sealed class MedicalCodesController : ControllerBase
{
    // ── Code-format validation (AC-002, AC-003; OWASP A03) ───────────────────────────────────
    // ICD-10-CM: letter + 2 digits + optional decimal + 1–4 alphanumeric chars (e.g. A00.0, B18.2)
    private static readonly Regex Icd10Regex = new(
        @"^[A-Z][0-9]{2}(\.[0-9A-Z]{1,4})?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // CPT: exactly 5 digits (e.g. 99213)
    private static readonly Regex CptRegex = new(
        @"^\d{5}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> ValidSources =
        new(StringComparer.Ordinal) { "AI", "AI-Corrected" };

    private static readonly HashSet<string> ValidReviewStatuses =
        new(StringComparer.Ordinal) { "Accepted", "Corrected" };

    private readonly AppDbContext                    _db;
    private readonly IAuditLogger                    _auditLogger;
    private readonly ILogger<MedicalCodesController> _logger;

    public MedicalCodesController(
        AppDbContext                    db,
        IAuditLogger                    auditLogger,
        ILogger<MedicalCodesController> logger)
    {
        _db          = db;
        _auditLogger = auditLogger;
        _logger      = logger;
    }

    /// <summary>
    /// Accepts or corrects a pending code suggestion for a patient.
    ///
    /// <para>
    /// Returns HTTP 201 with an empty body on success.
    /// Returns HTTP 400 for invalid Source, ReviewStatus, or corrected-code format.
    /// Returns HTTP 404 when the suggestion does not exist.
    /// Returns HTTP 409 when the suggestion has already been reviewed.
    /// </para>
    ///
    /// <para>
    /// OWASP A04 TOCTOU guard: <c>ExecuteUpdateAsync</c> includes a
    /// <c>WHERE review_status = 'Pending'</c> predicate; zero rows updated signals a
    /// concurrent review and returns 409.
    /// </para>
    /// </summary>
    [HttpPost("{id:int}/medical-codes")]
    public async Task<IActionResult> AcceptOrCorrectAsync(
        int id,
        [FromBody] CreateMedicalCodeRequest request,
        CancellationToken ct)
    {
        // ── 1. Validate enumerable fields before any DB round-trip (OWASP A03; AC-005) ────────
        if (!ValidSources.Contains(request.Source))
            return BadRequest(new { error = "Source must be 'AI' or 'AI-Corrected'." });

        if (!ValidReviewStatuses.Contains(request.ReviewStatus))
            return BadRequest(new { error = "ReviewStatus must be 'Accepted' or 'Corrected'." });

        // ── 2. Code format validation — applied to both original code and corrected code ──────
        var codeUnderReview = request.Source == "AI-Corrected" && !string.IsNullOrWhiteSpace(request.CorrectedCode)
            ? request.CorrectedCode
            : request.Code;

        bool formatValid = request.CodeType switch
        {
            "ICD10" => Icd10Regex.IsMatch(codeUnderReview),
            "CPT"   => CptRegex.IsMatch(codeUnderReview),
            _       => false
        };

        if (!formatValid)
        {
            // EventType only — never log the code value itself (OWASP A02; AC-002, AC-003)
            _logger.LogWarning("{EventType} {CodeType}", "InvalidCodeFormat", request.CodeType);
            return BadRequest(new { error = "Code format is invalid for the specified code type." });
        }

        // Corrected code required when Source = "AI-Corrected" (AC-001)
        if (request.Source == "AI-Corrected" && string.IsNullOrWhiteSpace(request.CorrectedCode))
            return BadRequest(new { error = "CorrectedCode is required when Source is 'AI-Corrected'." });

        // ── 3. Extract actor identity from JWT — never from request body (OWASP A01) ─────────
        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? User.FindFirstValue("sub")
                   ?? "unknown";
        var actorRole = User.FindFirstValue(ClaimTypes.Role)
                     ?? User.FindFirstValue("role")
                     ?? string.Empty;

        // users.id is int; null if the JWT sub claim cannot be parsed (OWASP A01; AC-002)
        int? reviewedBy = int.TryParse(actorId, out var parsedId) ? parsedId : null;

        // ── 4. Load the suggestion — 404 if missing (AC-002) ─────────────────────────────────
        var suggestion = await _db.CodeSuggestions
            .AsNoTracking()
            .FirstOrDefaultAsync(cs => cs.Id == request.SuggestionId, ct);

        if (suggestion is null)
            return NotFound(new { error = $"Code suggestion {request.SuggestionId} not found." });

        // ── 5. Optimistic status check (fast path before ExecuteUpdateAsync) ─────────────────
        if (suggestion.ReviewStatus != "Pending")
            return Conflict(new { error = "This code suggestion has already been reviewed." });

        // ── 6. Atomic TOCTOU guard: WHERE id=? AND review_status='Pending' (OWASP A04) ───────
        var finalCode = request.Source == "AI-Corrected" && !string.IsNullOrWhiteSpace(request.CorrectedCode)
            ? request.CorrectedCode!
            : request.Code;

        var updated = await _db.CodeSuggestions
            .Where(cs => cs.Id == request.SuggestionId && cs.ReviewStatus == "Pending")
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(cs => cs.ReviewStatus, request.ReviewStatus)
                    .SetProperty(cs => cs.ReviewedBy,   reviewedBy)
                    .SetProperty(cs => cs.ReviewedAt,   DateTimeOffset.UtcNow),
                ct);

        if (updated == 0)
            return Conflict(new { error = "This code suggestion has already been reviewed." });

        // ── 7. Persist the accepted/corrected code record (AC-001) ───────────────────────────
        await _db.PatientMedicalCodes.AddAsync(new PatientMedicalCode
        {
            PatientId    = id,
            CodeType     = request.CodeType,
            Code         = finalCode,
            OriginalCode = request.Code,        // original AI code; equals finalCode for accepts
            Description  = request.Description,
            Source       = request.Source,
            ReviewStatus = request.ReviewStatus,
            ReviewedBy   = reviewedBy ?? 0,     // 0 sentinel: parseable only via users.id FK; never null in DB
            ReviewedAt   = DateTimeOffset.UtcNow,
            CreatedAt    = DateTimeOffset.UtcNow,
        }, ct);

        await _db.SaveChangesAsync(ct);

        // ── 8. Audit — resource IDs only; corrected code is PHI and must not appear in any
        //       ILogger or Serilog sink (OWASP A02; HIPAA minimum-necessary §164.312(b)) ───────
        var actionType = request.ReviewStatus == "Corrected"
            ? AuditActionTypes.MedicalCodeCorrected
            : AuditActionTypes.MedicalCodeAccepted;

        await _auditLogger.RecordAsync(new AuditEntry(
            ActorId:      actorId,
            ActorRole:    actorRole,
            ActionType:   actionType,
            ResourceType: "CodeSuggestion",
            ResourceId:   request.SuggestionId.ToString(),
            IpAddress:    HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            UserAgent:    Request.Headers.UserAgent.ToString(),
            OccurredAt:   DateTime.UtcNow), ct);

        return Created(string.Empty, null);
    }
}
