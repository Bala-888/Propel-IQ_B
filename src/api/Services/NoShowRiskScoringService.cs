using Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

/// <summary>
/// Computes a weighted linear no-show risk score and persists it to the <c>bookings</c> table
/// without loading or tracking the entity (us_021; AC-001; AC-002; AC-003).
///
/// <para>
/// <b>Score formula</b> (output range [0.0, 1.0]; all denominators are positive constants —
/// no divide-by-zero is possible; <see cref="Math.Clamp"/> enforces hard bounds):
/// <code>
/// score = Clamp(
///     0.5 * Min(historyCount / 5.0, 1.0)        // history factor  — capped at 1.0 for ≥5 no-shows
///   + 0.3 * Max(0.0, 1.0 - daysToAppt / 30.0)  // proximity factor — higher score for near-term appts
///   + 0.2 * Max(0.0, 1.0 - leadDays / 14.0),   // lead-time factor — higher score for last-minute bookings
///   0.0, 1.0)
/// </code>
/// For a first-time patient (historyCount = 0) booked ≥ 1 day ahead, only the proximity and
/// lead-time terms contribute — the score stays below 0.50 (Edge: first-time patient; AC-001).
/// </para>
/// </summary>
public sealed class NoShowRiskScoringService : INoShowRiskScoringService
{
    // Tier thresholds (AC-002; checklist — no magic numbers)
    private const double TierLowMax    = 0.40;
    private const double TierMediumMax = 0.70;

    // Formula constants (AC-001; checklist — no magic numbers)
    private const double HistoryCapCount  = 5.0;
    private const double ProximityDaysCap = 30.0;
    private const double LeadDaysCap      = 14.0;

    private readonly AppDbContext _db;
    private readonly ILogger<NoShowRiskScoringService> _logger;

    public NoShowRiskScoringService(AppDbContext db, ILogger<NoShowRiskScoringService> logger)
    {
        _db     = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task ComputeAndPersistAsync(BookingCreatedEvent evt, CancellationToken ct = default)
    {
        var historyCount = await _db.Bookings
            .CountAsync(b => b.PatientId == evt.PatientId && b.Status == "NoShow", ct);

        var apptDateTime = evt.SlotDate.ToDateTime(evt.SlotStartTime, DateTimeKind.Utc);
        var daysToAppt   = Math.Max(0.0, (apptDateTime - DateTime.UtcNow).TotalDays);
        // leadDays = same as daysToAppt when booking is made in advance; 0 for same-day bookings
        var leadDays     = daysToAppt;

        var score = ComputeScore(historyCount, daysToAppt, leadDays);
        var tier  = DeriveTier(score);

        // Store score as 0-100 integer to match the DB constraint (chk_no_show_risk_score BETWEEN 0 AND 100)
        var scoreInt = (int)Math.Round(score * 100.0);

        // ExecuteUpdateAsync issues a single UPDATE without loading or tracking the entity (AC-001; performance)
        await _db.Bookings
            .Where(b => b.Id == evt.BookingId)
            .ExecuteUpdateAsync(s =>
                s.SetProperty(b => b.NoShowRiskScore, scoreInt)
                 .SetProperty(b => b.NoShowRiskTier, tier),
                ct);

        _logger.LogInformation(
            "NoShowRiskScore computed: BookingId={BookingId} Score={Score} Tier={Tier}",
            evt.BookingId, score, tier);
    }

    /// <inheritdoc />
    public async Task RecomputeAsync(
        int               bookingId,
        int               patientId,
        DateOnly          newSlotDate,
        TimeOnly          newSlotTime,
        CancellationToken ct = default)
    {
        var historyCount = await _db.Bookings
            .CountAsync(b => b.PatientId == patientId && b.Status == "NoShow", ct);

        var apptDateTime = newSlotDate.ToDateTime(newSlotTime, DateTimeKind.Utc);
        var daysToAppt   = Math.Max(0.0, (apptDateTime - DateTime.UtcNow).TotalDays);
        var leadDays     = daysToAppt;

        var score    = ComputeScore(historyCount, daysToAppt, leadDays);
        var tier     = DeriveTier(score);
        var scoreInt = (int)Math.Round(score * 100.0);

        await _db.Bookings
            .Where(b => b.Id == bookingId)
            .ExecuteUpdateAsync(s =>
                s.SetProperty(b => b.NoShowRiskScore, scoreInt)
                 .SetProperty(b => b.NoShowRiskTier, tier),
                ct);

        _logger.LogInformation(
            "NoShowRiskScore recomputed: BookingId={BookingId} Score={Score} Tier={Tier}",
            bookingId, score, tier);
    }

    // ── Private helpers ───────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Weighted linear formula over three rule-based features.
    /// All denominators are positive constants — no divide-by-zero is possible (AC-001; OWASP A05).
    /// </summary>
    private static double ComputeScore(int historyCount, double daysToAppt, double leadDays) =>
        Math.Clamp(
            0.5 * Math.Min(historyCount / HistoryCapCount, 1.0)
          + 0.3 * Math.Max(0.0, 1.0 - daysToAppt / ProximityDaysCap)
          + 0.2 * Math.Max(0.0, 1.0 - leadDays    / LeadDaysCap),
            0.0, 1.0);

    /// <summary>
    /// Derives the risk tier string from the normalised score [0.0, 1.0] (AC-002).
    /// </summary>
    private static string DeriveTier(double score) =>
        score < TierLowMax    ? "Low"    :
        score < TierMediumMax ? "Medium" :
                                "High";
}
