using System.ComponentModel.DataAnnotations;

namespace Api.DTOs;

/// <summary>
/// Request body for <c>POST /api/calendar/sync</c> (us_028; AC-001, AC-002).
/// </summary>
public sealed class CalendarSyncRequest
{
    /// <summary>Calendar provider. Must be <c>"Google"</c> or <c>"Outlook"</c>.</summary>
    [Required]
    public string Provider { get; set; } = string.Empty;

    /// <summary>ID of the confirmed booking to create a calendar event for.</summary>
    [Required]
    public int BookingId { get; set; }
}
