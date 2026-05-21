namespace Api.Data.Entities;

/// <summary>
/// Per-patient notification and calendar-sync preference row (us_029/AC-004).
/// One row per patient — created at registration with defaults applied by EF Core HasDefaultValue
/// config in AppDbContext.OnModelCreating (and enforced at the PostgreSQL column level in the migration).
/// All bool properties map to DB defaults so new rows inserted via raw SQL also receive correct defaults.
/// </summary>
public class PatientPreferences
{
    public int Id { get; set; }

    public int PatientId { get; set; }

    // ── Notification channels (opt-in by default; AC-004) ─────────────────────────────────────
    // HasDefaultValue(true) in AppDbContext → DB column DEFAULT true.
    // Privacy-by-default: a new patient receives all notification channels enabled.

    /// <summary>Email appointment reminders (us_027). Default: true.</summary>
    public bool EmailNotificationsEnabled { get; set; } = true;

    /// <summary>SMS appointment reminders (us_027). Default: true.</summary>
    public bool SmsNotificationsEnabled { get; set; } = true;

    /// <summary>Slot-swap availability notifications (us_026). Default: true.</summary>
    public bool SlotSwapNotificationsEnabled { get; set; } = true;

    // ── Calendar sync channels (opt-out by default; AC-004) ───────────────────────────────────
    // HasDefaultValue(false) in AppDbContext → DB column DEFAULT false.
    // Calendar sync requires OAuth token grant; default false prevents unexpected sync attempts.

    /// <summary>Google Calendar add-to-calendar sync (us_028). Default: false.</summary>
    public bool GoogleCalendarSyncEnabled { get; set; } = false;

    /// <summary>Outlook Calendar add-to-calendar sync (us_028). Default: false.</summary>
    public bool OutlookCalendarSyncEnabled { get; set; } = false;

    // ── Navigation ────────────────────────────────────────────────────────────────────────────
    public Patient Patient { get; set; } = null!;
}
