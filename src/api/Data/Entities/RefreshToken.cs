namespace Api.Data.Entities;

/// <summary>
/// Represents a JWT refresh token issued to a user.
/// Tokens are grouped into rotation chains by <see cref="FamilyId"/>:
/// when a replay attack is detected in task_002, a single
/// <c>UPDATE … WHERE family_id = @id SET is_revoked = true</c>
/// invalidates every token in the chain (Edge: refresh token replay attack).
/// </summary>
public class RefreshToken
{
    /// <summary>UUID primary key — maps to <c>uuid</c> in PostgreSQL.</summary>
    public Guid Id { get; set; }

    // Decision[2026-05-20]: Task spec declares UserId as Guid, but the existing User
    // entity uses int PK (SERIAL). UserId is int so the FK type matches users.id.
    /// <summary>Foreign key to <see cref="User"/>. Cascade-deleted when the user is removed.</summary>
    public int UserId { get; set; }

    /// <summary>Opaque refresh token string stored in the database for lookup.</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>UTC instant after which this token must be rejected.</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Set to <c>true</c> when the token is consumed or revoked.
    /// Family-level revocation sets <c>true</c> for all rows sharing <see cref="FamilyId"/>.
    /// </summary>
    public bool IsRevoked { get; set; }

    /// <summary>
    /// Groups all tokens issued in the same rotation chain.
    /// Assigned when the first token in a chain is issued and propagated on every
    /// rotation — enables O(1) family-level revocation (Edge: refresh token replay attack).
    /// </summary>
    public Guid FamilyId { get; set; }

    /// <summary>UTC instant the token was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Navigation property to the owning user (loaded via Include in RefreshAsync).</summary>
    public User User { get; set; } = null!;
}
