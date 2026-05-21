using System.ComponentModel.DataAnnotations;

namespace Api.DTOs;

/// <summary>
/// Request body for <c>POST /api/patients/walkin-create</c> (us_030/AC-004).
/// Minimum fields required to pre-fill the walk-in form; no full registration flow is triggered.
/// </summary>
public sealed class WalkinPatientRequest
{
    [Required]
    [MaxLength(100)]
    public string FirstName { get; init; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string LastName { get; init; } = string.Empty;

    [Required]
    public DateOnly DateOfBirth { get; init; }

    [MaxLength(30)]
    public string? PhoneNumber { get; init; }
}
