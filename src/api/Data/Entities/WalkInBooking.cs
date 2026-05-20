namespace Api.Data.Entities;

/// <summary>
/// Records a walk-in patient visit.
/// Decision[2026-05-21]: uses <c>int</c> primary key consistent with all other entities in this
/// codebase (Booking, Patient, User) — the task specification used Guid but int is the
/// established project-wide pattern (us_012/AC-001, AC-002).
/// </summary>
public class WalkInBooking
{
    public int Id { get; set; }

    /// <summary>Full name as entered at the front desk.</summary>
    public string PatientName { get; set; } = string.Empty;

    /// <summary>
    /// Date of birth stored as ISO-8601 string ("yyyy-MM-dd"), consistent with
    /// <see cref="Patient.DateOfBirth"/> — no PHI encryption needed for walk-in form data
    /// that is voluntarily disclosed at the desk.
    /// </summary>
    public string DateOfBirth { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// FK to <see cref="User"/> — null when the walk-in has no linked account (AC-001).
    /// Set to the new or existing User.Id when <c>createAccount = true</c> or
    /// <c>linkExistingAccountId</c> is provided (AC-002, AC-004).
    /// </summary>
    public int? UserId { get; set; }

    public User? User { get; set; }
}
