namespace Api.DTOs;

/// <summary>
/// Read model returned by <c>GET /admin/users</c>.
/// Contains only non-sensitive fields — no password hash or PHI.
/// </summary>
public sealed class UserResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>The login email — stored as <c>Username</c> on the <c>User</c> entity.</summary>
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
