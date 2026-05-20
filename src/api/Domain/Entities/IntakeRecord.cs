namespace Upacip.Api.Domain.Entities;

public enum IntakeMode { AI, Manual }
public enum IntakeStatus { Draft, Complete }

/// <summary>
/// Stores the pre-visit health intake data collected from a patient.
/// The Data JSONB field is AES-256 encrypted at rest.
/// </summary>
public sealed class IntakeRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PatientId { get; set; }
    public IntakeMode Mode { get; set; }
    public IntakeStatus Status { get; set; } = IntakeStatus.Draft;

    // PHI — encrypted JSONB at rest (serialised as JSON string)
    public string? DataEncrypted { get; set; }

    // AI session tracking
    public string? SessionId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    // Navigation
    public Patient Patient { get; set; } = null!;
}
