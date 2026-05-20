namespace Upacip.Api.Domain.Entities;

public enum UserRole
{
    Patient,
    Staff,
    Admin
}

/// <summary>
/// Platform user. PHI fields (Phone, DateOfBirth) are AES-256 encrypted at rest
/// via application-layer value converters before persistence.
/// </summary>
public sealed class AppUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;

    // PHI — encrypted at rest
    public string? Phone { get; set; }
    public string? DateOfBirth { get; set; }  // stored as ISO-8601 string, encrypted

    public UserRole Role { get; set; } = UserRole.Patient;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }

    // Navigation
    public Patient? Patient { get; set; }
    public ICollection<AuditLog> AuditLogs { get; set; } = [];
}
