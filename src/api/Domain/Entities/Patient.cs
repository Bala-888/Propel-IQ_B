namespace Upacip.Api.Domain.Entities;

public enum CalendarSyncStatus
{
    NotConnected,
    Synced,
    Failed,
    CheckSkipped
}

/// <summary>
/// Patient-specific profile data linked to an AppUser.
/// PHI fields are AES-256 encrypted at rest.
/// </summary>
public sealed class Patient
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }

    // PHI — encrypted at rest
    public string? InsuranceProvider { get; set; }
    public string? InsuranceId { get; set; }

    // Calendar sync
    public CalendarSyncStatus GoogleCalendarStatus { get; set; } = CalendarSyncStatus.NotConnected;
    public string? GoogleCalendarTokenEncrypted { get; set; }
    public CalendarSyncStatus OutlookCalendarStatus { get; set; } = CalendarSyncStatus.NotConnected;
    public string? OutlookCalendarTokenEncrypted { get; set; }

    // Notification preferences (opt-out flags)
    public bool EmailNotificationsEnabled { get; set; } = true;
    public bool SmsNotificationsEnabled { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public AppUser User { get; set; } = null!;
    public ICollection<IntakeRecord> IntakeRecords { get; set; } = [];
    public ICollection<Booking> Bookings { get; set; } = [];
    public ICollection<ClinicalDocument> ClinicalDocuments { get; set; } = [];
}
