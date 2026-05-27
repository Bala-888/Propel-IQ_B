using Api.Data;
using Api.Data.Entities;
using Api.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

/// <summary>
/// Queries <c>appointment_slots</c> with optional availability filtering and SKIP/TAKE pagination
/// for <c>GET /slots</c> (us_019; AC-001; AC-002).
///
/// <para>
/// The <c>WHERE IsAvailable = true</c> filter is applied at the database level — rows are never
/// fetched and then discarded in application code (AC-001; performance).
/// </para>
///
/// <para>
/// No patient identifiers are projected into <see cref="SlotDto"/> — <c>BookedByPatientId</c>
/// and any patient-linked navigation properties are intentionally excluded from the SELECT
/// projection (OWASP A02 — minimum data exposure; HIPAA — no PHI in slot listings).
/// </para>
/// </summary>
public sealed class SlotsService
{
    private readonly AppDbContext _db;

    public SlotsService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Returns a paginated list of appointment slots, optionally filtered to available slots only.
    /// </summary>
    /// <param name="query">Validated query parameters (Available, Page, PageSize).</param>
    /// <param name="ct">Cancellation token forwarded from the controller.</param>
    /// <returns>
    /// <see cref="SlotsResponse"/> with a page of <see cref="SlotDto"/> objects and
    /// <see cref="PaginationMeta"/>. The <c>Slots</c> list is empty — not null — when no rows
    /// match (Edge: no available slots; AC-001).
    /// </returns>
    public async Task<SlotsResponse> GetSlotsAsync(GetSlotsQuery query, CancellationToken ct = default)
    {
        // Build the base query — filter is applied at DB level, not in memory (AC-001; performance)
        var baseQuery = _db.AppointmentSlots.AsNoTracking();

        if (query.Available)
            baseQuery = baseQuery.Where(s => s.IsAvailable);

        // Optional date filter — applied at DB level to avoid full-table scan in application code
        // (AC-001; performance). SlotStart is stored as UTC TIMESTAMPTZ; the supplied date is
        // treated as a UTC calendar day boundary so the filter is timezone-consistent.
        if (query.Date.HasValue)
        {
            var startUtc = query.Date.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var endUtc   = startUtc.AddDays(1);
            baseQuery = baseQuery.Where(s => s.SlotStart >= startUtc && s.SlotStart < endUtc);
        }

        // COUNT before pagination — needed for TotalPages computation (AC-002)
        var total = await baseQuery.CountAsync(ct);

        // Deterministic ordering required for stable pagination (AC-002)
        var page     = query.Page;
        var pageSize = query.PageSize;

        var slots = await baseQuery
            .OrderBy(s => s.SlotStart)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            // Project to DTO — patient-linked columns are intentionally excluded (OWASP A02; HIPAA)
            .Select(s => new SlotDto(
                s.Id,
                DateOnly.FromDateTime(s.SlotStart),
                TimeOnly.FromDateTime(s.SlotStart),
                (int)(s.SlotEnd - s.SlotStart).TotalMinutes,
                s.IsAvailable ? "Available" : "Booked",
                s.ProviderName
            ))
            .ToListAsync(ct);

        // TotalPages = 0 when total = 0 to match the edge-case spec (Edge: no available slots)
        var totalPages = total == 0 ? 0 : (int)Math.Ceiling((double)total / pageSize);

        return new SlotsResponse(
            Slots:      slots,
            Pagination: new PaginationMeta(total, page, pageSize, totalPages)
        );
    }

    /// <summary>
    /// Returns up to <paramref name="count"/> available slots whose <c>slot_start</c> is nearest in time
    /// to <paramref name="contestedSlot"/>. Used by <c>POST /bookings</c> to populate the 409
    /// <c>alternatives</c> list (us_020; AC-003).
    /// </summary>
    public async Task<IReadOnlyList<SlotDto>> GetNearestAvailableSlotsAsync(
        AppointmentSlot contestedSlot,
        int             count = 3,
        CancellationToken ct  = default)
    {
        // Raw SQL is required because EF Core cannot translate ABS(EXTRACT(EPOCH FROM ...)) to
        // a server-side ORDER BY — an in-memory sort would fetch every available row first (perf).
        // The slot_start is cast to UTC text and parsed by Npgsql; id is the tiebreaker.
        var pivot = contestedSlot.SlotStart.ToUniversalTime();

        var rows = await _db.AppointmentSlots
            .FromSqlRaw(
                "SELECT * FROM appointment_slots " +
                "WHERE is_available = TRUE AND id <> {0} " +
                "ORDER BY ABS(EXTRACT(EPOCH FROM (slot_start - {1}::timestamptz))) " +
                "LIMIT {2}",
                contestedSlot.Id,
                pivot,
                count)
            .AsNoTracking()
            .ToListAsync(ct);

        return rows
            .Select(s => new SlotDto(
                s.Id,
                DateOnly.FromDateTime(s.SlotStart),
                TimeOnly.FromDateTime(s.SlotStart),
                (int)(s.SlotEnd - s.SlotStart).TotalMinutes,
                "Available",
                s.ProviderName))
            .ToList();
    }
}
