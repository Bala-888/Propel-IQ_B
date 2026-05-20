namespace Upacip.Api.Domain.Entities;

public enum ReminderType { Email, Sms }
public enum DeliveryStatus { Pending, Sent, Failed, Skipped }

public sealed class ReminderSchedule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BookingId { get; set; }
    public Guid PatientId { get; set; }
    public ReminderType ReminderType { get; set; }
    public DateTime ScheduledAt { get; set; }
    public DeliveryStatus DeliveryStatus { get; set; } = DeliveryStatus.Pending;
    public int AttemptCount { get; set; } = 0;
    public DateTime? LastAttemptAt { get; set; }
    public string? FailureReason { get; set; }

    // Navigation
    public Booking Booking { get; set; } = null!;
}
