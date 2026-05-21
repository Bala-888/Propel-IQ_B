using Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Api.Features.Admin;

/// <summary>
/// Implements <see cref="IAdminMetricsService"/> with a 5-second query timeout guard (us_034/AC-001).
///
/// <para>
/// A linked <see cref="CancellationTokenSource"/> combines the 5-second timeout with the
/// incoming HTTP cancellation token. <see cref="OperationCanceledException"/> caused specifically
/// by the 5-second timeout is re-thrown as <see cref="MetricsTimeoutException"/>; exceptions caused
/// by client disconnect are propagated normally (Edge: query > 5s; OWASP A04).
/// </para>
/// <para>
/// All aggregation is performed in-memory after a single <c>ToListAsync</c> call — no raw SQL,
/// no PHI fields selected (OWASP A02, A03; AC-001).
/// </para>
/// </summary>
public sealed class AdminMetricsService : IAdminMetricsService
{
    private readonly AppDbContext _db;

    public AdminMetricsService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<AdminMetricsDto> GetMetricsAsync(DateOnly date, CancellationToken ct = default)
    {
        // 5-second safety timeout — any query exceeding this returns 503 rather than timing out
        // the HTTP connection silently (Edge: query > 5s; OWASP A04).
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var linkedCts  = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, ct);

        List<Api.Data.Entities.Booking> bookings;
        try
        {
            // Single query: load all bookings for the requested date with their slots.
            // Date comparison uses DateTime.Date equality — slot_start column is UTC (OWASP A03 — no raw SQL).
            var dayStart = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var dayEnd   = dayStart.AddDays(1);

            bookings = await _db.Bookings
                .Include(b => b.AppointmentSlot)
                .Where(b =>
                    b.AppointmentSlot.SlotStart >= dayStart &&
                    b.AppointmentSlot.SlotStart <  dayEnd)
                .AsNoTracking()
                .ToListAsync(linkedCts.Token);
        }
        catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
        {
            // Only the 5-second timeout triggered — surface this as MetricsTimeoutException (Edge).
            throw new MetricsTimeoutException();
        }

        // ── LINQ in-memory aggregation ──────────────────────────────────────────────────────────
        // All counts are zero-safe — bookings.Count() on an empty list returns 0 (Edge: no bookings).

        var total     = bookings.Count;
        var confirmed = bookings.Count(b => b.Status == "Confirmed");
        var cancelled = bookings.Count(b => b.Status == "Cancelled");

        // Walk-in proxy: bookings created by a staff member (CreatedByStaffId != null).
        // The Booking entity has no IsWalkin boolean column; CreatedByStaffId is the canonical
        // walk-in indicator established in us_030 (decision logged: F013).
        var walkIns   = bookings.Count(b => b.CreatedByStaffId != null);

        // No-show risk tier distribution.
        var highRisk   = bookings.Count(b => b.NoShowRiskTier == "High");
        var mediumRisk = bookings.Count(b => b.NoShowRiskTier == "Medium");
        var lowRisk    = bookings.Count(b => b.NoShowRiskTier == "Low");

        // Average wait time: elapsed minutes from slot start to check-in for arrived patients.
        // Returns null when no patients have checked in yet (Edge: no arrivals → null, not 0).
        var arrivedBookings = bookings.Where(b => b.CheckedInAt.HasValue).ToList();
        double? averageWaitMinutes = arrivedBookings.Count > 0
            ? arrivedBookings.Average(b =>
                (b.CheckedInAt!.Value -
                 new DateTimeOffset(DateTime.SpecifyKind(b.AppointmentSlot.SlotStart, DateTimeKind.Utc))
                ).TotalMinutes)
            : null;

        return new AdminMetricsDto(
            TotalBookings      : total,
            Confirmed          : confirmed,
            Cancelled          : cancelled,
            WalkIns            : walkIns,
            AverageWaitMinutes : averageWaitMinutes,
            HighRisk           : highRisk,
            MediumRisk         : mediumRisk,
            LowRisk            : lowRisk);
    }
}
