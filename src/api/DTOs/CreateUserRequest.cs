using System.ComponentModel.DataAnnotations;

namespace Api.DTOs;

/// <summary>
/// Validated request body for <c>POST /admin/users</c>.
/// Role must be one of the platform-defined values; any other value fails model validation
/// with "Invalid role value" before the action method is reached (AC-001; OWASP A03).
/// </summary>
public sealed class CreateUserRequest
{
    [Required(ErrorMessage = "Name is required")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "A valid email address is required")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Role is required")]
    [AllowedValues("Staff", "Admin", ErrorMessage = "Invalid role value")]
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Admin-set initial password. If omitted, a random temporary password is generated and
    /// emailed to the new user. When provided, MustChangePassword is still set so the user
    /// is prompted to change it on first login (AC-001).
    /// </summary>
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters")]
    public string? Password { get; set; }
}
