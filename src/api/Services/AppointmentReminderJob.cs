using Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

/// <summary>
/// Periodic background job that fires appointment reminders at the 24-hour and 2-hour windows
/// before each confirmed booking (us_027; AC-001, AC-002).
///
/// <para>
/// <b>Tick cadence</b>: every 5 minutes.  On each tick a fresh DI scope is created so the
/// <see cref="AppDbContext"/> and <see cref="IAppointmentReminderService"/> are per-tick scoped,
/// which guarantees change-tracker isolation between ticks (mirrors PreferredSlotMonitorJob).
/// </para>
///
/// <para>
/// <b>Candidate window</b>: a booking is a reminder candidate when:
/// <list type="bullet">
///   <item><description>Status == "Confirmed"</description></item>
///   <item><description>The corresponding tracking column is null (not yet sent)</description></item>
///   <item><description><c>AppointmentSlot.SlotStart</c> falls within ±5 minutes of the target
///   horizon (now+24 h for the 24 h tier; now+2 h for the 2 h tier)</description></item>
/// </list>
/// The ±5-minute window tolerates clock skew and missed ticks while avoiding double-fires on a
/// slot that is right on the boundary.
/// </para>
///
/// <para>
/// <b>Appointment time</b>: the canonical appointment datetime is
/// <c>Booking.AppointmentSlot.SlotStart</c> — Booking has no <c>AppointmentDatetime</c> column.
/// Queries load the related <c>AppointmentSlot</c> via EF Core Include (translated to a SQL JOIN).
/// </para>
///
/// <para>
/// <b>Tracking deduplication</b>: <see cref="IAppointmentReminderService.SendReminderAsync"/>
/// persists the tracking timestamp after <c>Task.WhenAll</c>, so a booking is removed from the
/// candidate set on the very next tick even if all notification dispatches failed
/// (see <see cref="AppointmentReminderService"/> class summary for rationale).
/// </para>
/// </summary>
public sealed class AppointmentReminderJob : BackgroundService
{
    private readonly IServiceScopeFactory            _scopeFactory;
    private readonly ILogger<AppointmentReminderJob> _logger;

    // ±5 minutes tolerance around each reminder horizon — tolerates missed ticks and clock skew
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(5);

    public AppointmentReminderJob(
        IServiceScopeFactory            scopeFactory,
        ILogger<AppointmentReminderJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AppointmentReminderJob started. Tick interval: 5 minutes.");

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await ProcessTickAsync(stoppingToken);
        }
    }

    private async Task ProcessTickAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db      = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IAppointmentReminderService>();

        var now = DateTime.UtcNow;

        // ── 24-hour reminder candidates ────────────────────────────────────────────────────────
        var horizon24h = now.AddHours(24);
        var lower24h   = horizon24h - Window;
        var upper24h   = horizon24h + Window;

        var candidates24h = await db.Bookings
            .Include(b => b.Patient)
            .Include(b => b.AppointmentSlot)
            .Where(b => b.Status == "Confirmed"
                     && b.Reminder24hSentAt == null
                     && b.AppointmentSlot.SlotStart >= lower24h
                     && b.AppointmentSlot.SlotStart <= upper24h)
            .ToListAsync(ct);

        _logger.LogDebug(
            "AppointmentReminderJob 24h scan: {Count} candidate(s) in window [{Lower:u}, {Upper:u}].",
            candidates24h.Count, lower24h, upper24h);

        foreach (var booking in candidates24h)
        {
            await DispatchAsync(service, booking, ReminderTier.TwentyFourHour, ct);
        }

        // ── 2-hour reminder candidates ─────────────────────────────────────────────────────────
        var horizon2h = now.AddHours(2);
        var lower2h   = horizon2h - Window;
        var upper2h   = horizon2h + Window;

        var candidates2h = await db.Bookings
            .Include(b => b.Patient)
            .Include(b => b.AppointmentSlot)
            .Where(b => b.Status == "Confirmed"
                     && b.Reminder2hSentAt == null
                     && b.AppointmentSlot.SlotStart >= lower2h
                     && b.AppointmentSlot.SlotStart <= upper2h)
            .ToListAsync(ct);

        _logger.LogDebug(
            "AppointmentReminderJob 2h scan: {Count} candidate(s) in window [{Lower:u}, {Upper:u}].",
            candidates2h.Count, lower2h, upper2h);

        foreach (var booking in candidates2h)
        {
            await DispatchAsync(service, booking, ReminderTier.TwoHour, ct);
        }
    }

    private async Task DispatchAsync(
        IAppointmentReminderService service,
        Api.Data.Entities.Booking   booking,
        ReminderTier                tier,
        CancellationToken           ct)
    {
        try
        {
            await service.SendReminderAsync(booking, tier, ct);
        }
        catch (OperationCanceledException)
        {
            throw; // propagate graceful-shutdown signal
        }
        catch (Exception ex)
        {
            // Per-booking failure must not abort the tick — remaining candidates continue processing
            _logger.LogError(ex,
                "AppointmentReminderJobError: unhandled exception for BookingId={BookingId} Tier={Tier}. " +
                "Booking will be retried on the next tick if the tracking column was not written.",
                booking.Id, tier);
        }
    }
}
