namespace Api.Data.Entities;

public class Patient
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    // ── PHI columns — stored as pgcrypto-compatible bytea ciphertext (us_006/AC-001, DR-001) ──
    // EF Core value converters in AppDbContext transparently encrypt on write / decrypt on read.
    // Type is string? so ValueConverter<string?, byte[]?> applies uniformly to all five fields.

    /// <summary>ISO-8601 date string ("yyyy-MM-dd") — encrypted as bytea in the database.</summary>
    public string? DateOfBirth { get; set; }

    /// <summary>Patient email address — PHI, encrypted.</summary>
    public string? Email { get; set; }

    /// <summary>Patient phone number — PHI, encrypted.</summary>
    public string? Phone { get; set; }

    /// <summary>Insurance provider name — PHI, encrypted.</summary>
    public string? InsuranceProvider { get; set; }

    /// <summary>Insurance policy / member ID — PHI, encrypted.</summary>
    public string? InsuranceId { get; set; }

    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<InsuranceRecord> InsuranceRecords { get; set; } = new List<InsuranceRecord>();
}
