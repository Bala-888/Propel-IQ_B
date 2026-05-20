namespace Api.DTOs;

/// <summary>
/// Response body for a successful <c>POST /walkins</c> (HTTP 201).
/// Decision[2026-05-21]: uses <c>int</c> for all IDs consistent with the project-wide
/// integer primary-key pattern — the task specification used Guid but the schema uses int.
/// </summary>
public sealed class CreateWalkInResponse
{
    /// <summary>Primary key of the newly created <c>WalkInBooking</c> record.</summary>
    public int BookingId { get; set; }

    /// <summary>
    /// ID of the linked <see cref="Api.Data.Entities.User"/> record.
    /// <c>null</c> when the walk-in was created without an account (<c>createAccount = false</c>,
    /// no <c>linkExistingAccountId</c> provided).
    /// </summary>
    public int? UserId { get; set; }

    /// <summary>
    /// <c>true</c> when the credentials email failed on all 3 retry attempts.
    /// The HTTP status is still 201 — the booking is committed; the client should display
    /// a warning to the staff member (Edge: delivery failure; AC-003).
    /// </summary>
    public bool CredentialsEmailFailed { get; set; }
}
