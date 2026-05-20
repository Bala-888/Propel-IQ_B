namespace Upacip.Api.Domain.Entities;

public enum BookingStatus
{
    Confirmed,
    Arrived,
    NoShow,
    Cancelled,
    WalkIn
}

public enum BookingChannel { Online, WalkIn, Phone }

public enum InsuranceVerificationStatus
{
    Verified,
    NotVerified,
    CheckSkipped
}

public sealed class Booking
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PatientId { get; set; }
    public Guid SlotId { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.Confirmed;
    public BookingChannel Channel { get; set; } = BookingChannel.Online;

    // Insurance
    public InsuranceVerificationStatus InsuranceStatus { get; set; } = InsuranceVerificationStatus.CheckSkipped;

    // No-show risk
    public int NoShowRiskScore { get; set; }
    public string? RiskFactorsJson { get; set; }  // JSONB

    // Preferred slot swap
    public Guid? PreferredSlotId { get; set; }
    public bool IsSlotMonitorActive { get; set; } = false;

    // Arrival tracking
    public DateTime? ArrivedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Walk-in: patient may not have a platform account
    public string? WalkInPatientName { get; set; }
    public string? WalkInPatientPhone { get; set; }

    // Calendar sync
    public string CalendarSyncStatus { get; set; } = "NotConnected";

    // Navigation
    public Patient Patient { get; set; } = null!;
    public AppointmentSlot Slot { get; set; } = null!;
    public ICollection<ReminderSchedule> Reminders { get; set; } = [];
}
