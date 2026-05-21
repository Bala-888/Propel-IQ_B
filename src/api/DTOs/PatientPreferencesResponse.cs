namespace Api.DTOs;

/// <summary>
/// Response body for GET and PATCH /api/patients/{id}/preferences (us_029/AC-002).
/// Contains all five current preference field values so the frontend can confirm the persisted
/// state without relying on local state alone (AC-002 — contract reliability).
/// No PHI included (OWASP A02 / HIPAA minimum-necessary).
/// </summary>
public sealed class PatientPreferencesResponse
{
    public bool EmailNotificationsEnabled    { get; set; }
    public bool SmsNotificationsEnabled      { get; set; }
    public bool SlotSwapNotificationsEnabled { get; set; }
    public bool GoogleCalendarSyncEnabled    { get; set; }
    public bool OutlookCalendarSyncEnabled   { get; set; }
}
