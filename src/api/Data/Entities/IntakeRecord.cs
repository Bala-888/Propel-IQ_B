namespace Api.Data.Entities;

public class IntakeRecord
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public string ChiefComplaint { get; set; } = string.Empty;
    public string? SymptomNotes { get; set; }
    public DateTime RecordedAt { get; set; }

    public Patient Patient { get; set; } = null!;
}
