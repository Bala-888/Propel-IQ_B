namespace Api.Data.Entities;

public class ReminderSchedule
{
    public int Id { get; set; }
    public int BookingId { get; set; }
    public string ReminderType { get; set; } = string.Empty;
    public DateTime ScheduledAt { get; set; }
    public bool IsSent { get; set; }

    public Booking Booking { get; set; } = null!;
}
