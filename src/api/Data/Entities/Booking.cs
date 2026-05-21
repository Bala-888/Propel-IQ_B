using System.Text.Json;

namespace Api.Data.Entities;

public class Booking
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public int AppointmentSlotId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime BookedAt { get; set; }

    // AC-002: 0–100 no-show risk score produced by the prediction model; CHECK constraint in task_002 migration
    public int? NoShowRiskScore { get; set; }

    // AC-002 (us_021): risk tier derived from the normalised score — "Low" | "Medium" | "High" | "Unknown"
    // Defaults to "Unknown" until the async scoring pipeline writes the computed value.
    public string NoShowRiskTier { get; set; } = "Unknown";

    // AC-003: structured risk factor key/value data stored as jsonb; JsonDocument maps to Npgsql jsonb directly
    public JsonDocument? RiskFactors { get; set; }

    public Patient Patient { get; set; } = null!;
    public AppointmentSlot AppointmentSlot { get; set; } = null!;
}
