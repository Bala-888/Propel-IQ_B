using System.ComponentModel.DataAnnotations;

namespace Api.DTOs;

/// <summary>
/// Request body for <c>POST /api/bookings/{bookingId}/preferred-slot</c> (us_024; AC-001).
/// </summary>
public sealed class SetPreferredSlotRequest
{
    /// <summary>ID of the <c>appointment_slots</c> row the patient wants as their preferred alternative.</summary>
    [Required]
    public int SlotId { get; init; }
}
