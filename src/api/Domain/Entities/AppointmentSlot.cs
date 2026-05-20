namespace Upacip.Api.Domain.Entities;

public enum SlotStatus { Available, Booked, Blocked, Cancelled }

public sealed class AppointmentSlot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime ScheduledAt { get; set; }
    public int DurationMinutes { get; set; } = 30;
    public SlotStatus Status { get; set; } = SlotStatus.Available;
    public string? ProviderId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Booking? Booking { get; set; }
}
