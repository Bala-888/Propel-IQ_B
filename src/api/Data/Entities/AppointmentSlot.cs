namespace Api.Data.Entities;

public class AppointmentSlot
{
    public int Id { get; set; }
    public DateTime SlotStart { get; set; }
    public DateTime SlotEnd { get; set; }
    public bool IsAvailable { get; set; }
    public string? ProviderName { get; set; }

    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}
