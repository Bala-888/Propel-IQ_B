namespace Api.DTOs;

// Decision[2026-05-20]: UserId is int (not Guid) — existing User entity and schema use
// SERIAL int PK. AC-001 requests UUID; a schema migration to Guid PK is out of scope for
// this task. Callers should treat UserId as an opaque identifier.
public sealed class RegisterPatientResponse
{
    public int UserId { get; init; }
    public string Role { get; init; } = string.Empty;
}
