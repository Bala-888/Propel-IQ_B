using System.ComponentModel.DataAnnotations;

namespace Api.DTOs;

/// <summary>
/// Request body for <c>POST /walkins</c>.
/// Implements <see cref="IValidatableObject"/> to enforce that <see cref="Email"/> is
/// non-empty when <see cref="CreateAccount"/> is true — a conditional requirement that
/// standard data annotations cannot express (Edge: empty email; AC-002; OWASP A03).
/// </summary>
public sealed class CreateWalkInRequest : IValidatableObject
{
    [Required]
    public string PatientName { get; set; } = string.Empty;

    /// <summary>Patient's date of birth — required for record identification.</summary>
    [Required]
    public DateOnly DateOfBirth { get; set; }

    /// <summary>
    /// When <c>true</c>, a new Patient User account is created and linked to this booking (AC-002).
    /// When <c>false</c>, the booking is created without a patient account (AC-001).
    /// </summary>
    public bool CreateAccount { get; set; }

    /// <summary>
    /// Required when <see cref="CreateAccount"/> is <c>true</c> (validated in <see cref="Validate"/>).
    /// Optional otherwise. <see cref="EmailAddressAttribute"/> applies only when value is non-null/empty.
    /// </summary>
    [EmailAddress(ErrorMessage = "Must be a valid email address")]
    public string? Email { get; set; }

    /// <summary>
    /// When set, the walk-in booking is linked to this existing User ID without creating a new account.
    /// Takes precedence: if this is provided, <see cref="CreateAccount"/> is ignored (AC-004 Yes path).
    /// </summary>
    public int? LinkExistingAccountId { get; set; }

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (CreateAccount && string.IsNullOrWhiteSpace(Email))
        {
            yield return new ValidationResult(
                "Email is required to create an account",
                new[] { nameof(Email) });
        }
    }
}
