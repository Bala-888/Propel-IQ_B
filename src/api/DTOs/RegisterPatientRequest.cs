using System.ComponentModel.DataAnnotations;

namespace Api.DTOs;

public sealed class RegisterPatientRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public DateOnly DateOfBirth { get; set; }

    [Required]
    [EmailAddress(ErrorMessage = "Must be a valid email address")]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Phone { get; set; } = string.Empty;

    [Required]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters")]
    public string Password { get; set; } = string.Empty;

    // Insurance fields are optional — null and empty-string are both treated as absent (Edge: insurance optional)
    public string? InsuranceProvider { get; set; }

    public string? InsuranceId { get; set; }
}
