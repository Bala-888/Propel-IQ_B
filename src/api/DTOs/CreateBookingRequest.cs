namespace Api.DTOs;

/// <summary>
/// Request body for <c>POST /bookings</c> (us_020; AC-001).
/// The patient identity is sourced from the JWT sub claim — never from the request body (OWASP A01).
/// </summary>
/// <param name="SlotId">The <c>appointment_slots.id</c> the patient wants to reserve.</param>
public sealed record CreateBookingRequest(int SlotId);
