namespace Api.Data.Entities;

public class IntakeRecord
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public string ChiefComplaint { get; set; } = string.Empty;
    public string? SymptomNotes { get; set; }
    public DateTime RecordedAt { get; set; }

    // ── AI-intake draft/complete fields (us_016-I/AC-005; us_016-II/AC-003) ─────────────────────
    // Status: "Draft" while the AI intake session is in progress; "Complete" after confirmation.
    // Mode: "AI" for sessions completed via the Ollama dialogue; null for manual intake.
    // Data: AES-256 PGP-symmetric ciphertext of the serialised IntakeFieldState JSON.
    //       PHI-encrypted by IntakeRecordService before write; never stored as plaintext (OWASP A02).
    // UpdatedAt: set to UTC now on every upsert so the record reflects the latest partial state.
    public string? Status { get; set; }
    public string? Mode { get; set; }
    public byte[]? Data { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Patient Patient { get; set; } = null!;
}
