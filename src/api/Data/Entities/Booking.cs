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

    // ── Reminder tracking columns (us_027; AC-001, AC-002) ───────────────────────────────────────
    // Populated by AppointmentReminderJob after a successful notification dispatch.
    // Non-null = reminder was already sent for that tier; prevents duplicate fires on subsequent ticks.
    // NOTE: When BookingService.RescheduleAsync is implemented, it MUST reset both columns to null
    //       so the job re-evaluates both windows from the new appointment_datetime (Edge: reschedule).
    public DateTimeOffset? Reminder24hSentAt { get; set; }
    public DateTimeOffset? Reminder2hSentAt  { get; set; }

    // ── Walk-in booking supplemental columns (us_030/AC-003) ──────────────────────────────────
    // Nullable: only populated for walk-in bookings created via POST /bookings/walkin.
    // Regular patient-self-service bookings leave these null (backward-compatible; no default needed).

    /// <summary>Free-text reason supplied by front-desk staff at walk-in time.</summary>
    public string? ReasonForVisit { get; set; }

    /// <summary>Priority tier assigned at intake — e.g. "Normal", "Urgent".</summary>
    public string? Priority { get; set; }

    /// <summary>String ID of the staff user who created this walk-in booking (from JWT sub claim).</summary>
    public string? CreatedByStaffId { get; set; }

    // ── Queue / check-in tracking (us_031/AC-001) ─────────────────────────────────────────────
    /// <summary>
    /// UTC timestamp when staff marked the patient as arrived (PATCH /bookings/{id}/status → CheckedIn).
    /// Null until the patient checks in — used as <c>ArrivalTime</c> in <c>QueueEntryDto</c>.
    /// </summary>
    public DateTimeOffset? CheckedInAt { get; set; }

    public Patient Patient { get; set; } = null!;
    public AppointmentSlot AppointmentSlot { get; set; } = null!;
}
