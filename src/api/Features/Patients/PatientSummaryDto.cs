namespace Api.Features.Patients;

/// <summary>
/// Flat aggregate DTO for the 360° patient view (us_040/AC-002).
/// All four sections are always present; empty collections are returned when no data exists
/// (Edge: no entities → <c>Entities = []</c>, never null; AC-002).
/// </summary>
public sealed class PatientSummaryDto
{
    /// <summary>Decrypted patient demographics. Never null for a found patient.</summary>
    public DemographicsDto Demographics { get; set; } = null!;

    /// <summary>
    /// Extracted clinical entities — always an array, never null.
    /// Empty array when <c>patient_entities</c> has no rows for this patient (Edge: no entities).
    /// </summary>
    public EntityDto[] Entities { get; set; } = Array.Empty<EntityDto>();

    /// <summary>Active (Confirmed) bookings ordered by slot start time ascending.</summary>
    public BookingSummaryDto[] ActiveBookings { get; set; } = Array.Empty<BookingSummaryDto>();

    /// <summary>Paginated document list ordered by upload timestamp descending.</summary>
    public DocumentSummaryDto[] Documents { get; set; } = Array.Empty<DocumentSummaryDto>();

    /// <summary>Total document count for this patient — used by frontend pagination controls.</summary>
    public int TotalDocumentCount { get; set; }

    /// <summary>1-based current page number.</summary>
    public int CurrentPage { get; set; }

    /// <summary>Page size used for the Documents slice.</summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Detected clinical conflicts for this patient — always an array, never null.
    /// Empty array when no conflicts have been detected (Edge: no conflicts; AC-004).
    /// OWASP A02: conflict descriptions are PHI — never written to ILogger.
    /// </summary>
    public ConflictDto[] Conflicts { get; set; } = Array.Empty<ConflictDto>();
}
