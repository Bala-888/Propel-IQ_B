namespace Api.Data.Entities;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    // Decision[2026-05-21]: Added for us_011/task_002 (user management table). Stored on
    // creation by both UserManagementService and AuthController.RegisterAsync. Default empty
    // so existing rows are unaffected by the AddUserName migration.
    public string Name { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    // Decision[2026-05-20]: Added for AC-006 (inactive account check in LoginAsync).
    // Defaults to true so existing rows are unaffected by the AddUserIsActive migration.
    public bool IsActive { get; set; } = true;
    // Decision[2026-05-21]: Added for AC-001 (us_011) to flag admin-created accounts that
    // require a password change on first login. Default false keeps existing rows unchanged.
    public bool MustChangePassword { get; set; } = false;
    // Decision[2026-05-27]: Added to embed the patients.id in the JWT pid claim so
    // patient-facing controllers can resolve patient_id without a DB round-trip (OWASP A01).
    public int? PatientId { get; set; }
}
