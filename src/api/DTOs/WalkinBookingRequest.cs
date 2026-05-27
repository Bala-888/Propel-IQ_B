using System.ComponentModel.DataAnnotations;

namespace Api.DTOs;

/// <summary>
/// Request body for <c>POST /api/bookings/walkin</c> (us_030/AC-003; OWASP A03).
/// <c>PatientId</c> and <c>SlotId</c> are sourced from prior search/slot-list calls;
/// <c>StaffId</c> is never accepted in the body — extracted from the JWT sub claim only (OWASP A01).
/// </summary>
public sealed class WalkinBookingRequest
{
    [Required]
    public int PatientId { get; init; }

    [Required]
    public int SlotId { get; init; }

    [MaxLength(500)]
    public string? ReasonForVisit { get; init; }

    [MaxLength(20)]
    public string? Priority { get; init; }

    /// <summary>
    /// When <c>true</c>, bypasses the duplicate-today guard and creates a second confirmed booking
    /// for the same patient on the same day. Set only after the front-end confirms the override
    /// banner (AC-003; edge: duplicate walk-in).
    /// </summary>
    public bool OverrideDuplicate { get; init; } = false;

    /// <summary>Patient phone number captured at the desk — written to <c>patients.phone</c> after booking commits.</summary>
    [MaxLength(30)]
    public string? Phone { get; init; }

    /// <summary>Insurance provider name selected by staff — written to <c>patients.insurance_provider</c> after booking commits.</summary>
    [MaxLength(100)]
    public string? InsuranceProvider { get; init; }
}
