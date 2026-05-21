namespace Api.DTOs;

/// <summary>
/// Partial update DTO for PATCH /api/patients/{id}/preferences (us_029/AC-002).
/// Only non-null fields are applied to the <c>PatientPreferences</c> entity.
/// A null value means "not changed" — never "set to null/unknown" (OWASP A03: no mass-assignment).
/// A payload that sets all five fields to false is accepted and persisted without error
/// (Edge: all channels disabled is valid persisted state; AC-002 / task spec).
/// </summary>
public sealed class PatchPreferencesDto
{
    public bool? EmailNotificationsEnabled    { get; set; }
    public bool? SmsNotificationsEnabled      { get; set; }
    public bool? SlotSwapNotificationsEnabled { get; set; }
    public bool? GoogleCalendarSyncEnabled    { get; set; }
    public bool? OutlookCalendarSyncEnabled   { get; set; }
}
