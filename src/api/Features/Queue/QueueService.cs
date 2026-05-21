using Api.Audit;
using Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Api.Features.Queue;


/// <summary>
/// Implements <see cref="IQueueService"/> for <c>GET /api/queue</c> (us_031/AC-001).
///
/// <para>
/// Query design decisions:
/// <list type="bullet">
///   <item>
///     <b>PatientName</b>: constructed from <c>Patient.FirstName + " " + Patient.LastName</c> — these
///     are plain-string columns in the current schema. PHI columns (email, phone, DOB) are excluded
///     from the projection; they are never fetched, logged, or serialised by this service (OWASP A02).
///   </item>
///   <item>
///     <b>Position</b>: computed as 1-based row index in C# after ordering by <c>BookedAt, Id</c> —
///     there is no stored <c>queue_position</c> column; ordering is deterministic via the secondary Id tiebreaker.
///   </item>
///   <item>
///     <b>ArrivalTime</b>: sourced from <c>Booking.CheckedInAt</c> (nullable) — null until staff
///     marks the patient as arrived via <c>PATCH /bookings/{id}/status → CheckedIn</c>.
///   </item>
/// </list>
/// </para>
/// </summary>
public sealed class QueueService : IQueueService
{
    private readonly AppDbContext               _db;
    private readonly Api.Services.IAuditLogger  _audit;
    private readonly IQueueHubService           _hub;

    public QueueService(AppDbContext db, Api.Services.IAuditLogger audit, IQueueHubService hub)
    {
        _db    = db;
        _audit = audit;
        _hub   = hub;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<QueueEntryDto>> GetQueueAsync(DateOnly date, DateTimeOffset? since = null, CancellationToken ct = default)
    {
        // Derive UTC window for the requested date — filter uses half-open interval [dayStart, dayEnd)
        // to avoid timezone boundary drift when comparing against SlotStart (DateTime, UTC).
        var dayStart = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var dayEnd   = dayStart.AddDays(1);

        // Base query: status filter restricts to visible queue entries (OWASP A02 — no PHI columns).
        var baseQuery = _db.Bookings
            .Where(b =>
                b.AppointmentSlot.SlotStart >= dayStart &&
                b.AppointmentSlot.SlotStart <  dayEnd   &&
                (b.Status == "Confirmed" || b.Status == "CheckedIn"));

        // AC-004 reconnection fallback: when `since` is provided, narrow to entries created or
        // status-updated at or after the cursor timestamp. Booking entity has no UpdatedAt column;
        // BookedAt (creation) and CheckedInAt (status change) are the canonical event timestamps.
        // Decision logged: F011 (surrogate for UpdatedAt).
        if (since.HasValue)
        {
            var sinceUtc = since.Value.UtcDateTime; // DateTime for comparison with BookedAt (DateTime)
            baseQuery = baseQuery.Where(b =>
                b.BookedAt >= sinceUtc ||
                (b.CheckedInAt != null && b.CheckedInAt >= since.Value));
        }

        // Single-query projection: Select only the columns needed for the DTO to avoid fetching
        // PHI bytea columns (email, phone, DOB) that are not required here (OWASP A02).
        var raw = await baseQuery
            .OrderBy(b => b.BookedAt)
            .ThenBy(b => b.Id)
            .Select(b => new
            {
                b.Id,
                b.Patient.FirstName,
                b.Patient.LastName,
                b.CheckedInAt,
                SlotStart      = b.AppointmentSlot.SlotStart,
                b.NoShowRiskTier,
                b.Status,
            })
            .AsNoTracking()
            .ToListAsync(ct);

        // Position is 1-based ordinal from the ordered result — computed in C# because SQL ROW_NUMBER
        // is not required when the full result set is already loaded for the DTO projection.
        return raw
            .Select((r, idx) => new QueueEntryDto(
                Id              : r.Id,
                Position        : idx + 1,
                PatientName     : $"{r.FirstName} {r.LastName}",
                ArrivalTime     : r.CheckedInAt,
                AppointmentTime : new DateTimeOffset(DateTime.SpecifyKind(r.SlotStart, DateTimeKind.Utc)),
                NoShowRiskTier  : r.NoShowRiskTier ?? "Unknown",
                Status          : r.Status))
            .ToList()
            .AsReadOnly();
    }

    /// <inheritdoc />
    public async Task<ArrivedResponseDto?> MarkArrivedAsync(int bookingId, string staffId, CancellationToken ct = default)
    {
        // OWASP A03: bookingId is used as a parameterised EF Core predicate — never in raw SQL.
        var booking = await _db.Bookings.FindAsync([bookingId], ct);
        if (booking is null)
            return null;    // controller maps null → 404

        // AC-004 idempotent path — already CheckedIn: return existing timestamp, no DB write, no audit.
        if (booking.Status == "CheckedIn")
            return new ArrivedResponseDto("CheckedIn", booking.CheckedInAt!.Value);

        // AC-003 — server-side timestamp only; no timestamp accepted from caller.
        booking.Status      = "CheckedIn";
        booking.CheckedInAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);

        // Audit after save, on transition only — never before save, never on idempotent path (OWASP A02; AC-004).
        _audit.Log(staffId, AuditActionTypes.PatientMarkedArrived, bookingId.ToString());

        // us_033/AC-002: broadcast QueueEntryUpdated after a successful save.
        // Load navigation properties for the DTO — needed for PatientName and AppointmentTime.
        // CancellationToken.None: client disconnect after commit must not cancel the broadcast (OWASP A04).
        await _db.Entry(booking).Reference(b => b.Patient).LoadAsync(CancellationToken.None);
        await _db.Entry(booking).Reference(b => b.AppointmentSlot).LoadAsync(CancellationToken.None);

        var broadcastDto = new QueueEntryDto(
            Id              : booking.Id,
            Position        : 0,  // Position is a live ordering concern; frontend preserves existing position on update (F014)
            PatientName     : $"{booking.Patient.FirstName} {booking.Patient.LastName}",
            ArrivalTime     : booking.CheckedInAt,
            AppointmentTime : new DateTimeOffset(DateTime.SpecifyKind(booking.AppointmentSlot.SlotStart, DateTimeKind.Utc)),
            NoShowRiskTier  : booking.NoShowRiskTier ?? "Unknown",
            Status          : booking.Status);

        await _hub.BroadcastEntryUpdatedAsync(broadcastDto);

        return new ArrivedResponseDto("CheckedIn", booking.CheckedInAt.Value);
    }
}
