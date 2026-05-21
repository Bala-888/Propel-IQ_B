namespace Api.Data.Entities;

/// <summary>
/// Tracks the external calendar event created (or attempted) for a booking on a given provider
/// (us_028; AC-001–AC-005).
///
/// <para>
/// One row per (BookingId, Provider) pair — a UNIQUE constraint enforces this so a single booking
/// can have at most one Google event and one Outlook event.
/// </para>
///
/// <para>
/// <b>Status values</b>:
/// <list type="bullet">
///   <item><description><c>"Synced"</c> — event created or last PATCH succeeded.</description></item>
///   <item><description><c>"Failed"</c> — last attempt failed; retry may be pending (AC-005).</description></item>
///   <item><description><c>"Deleted"</c> — DELETE call succeeded after booking cancellation (AC-004).</description></item>
/// </list>
/// </para>
/// </summary>
public class BookingCalendarSync
{
    public int    Id        { get; set; }
    public int    BookingId { get; set; }

    /// <summary>"Google" or "Outlook".</summary>
    public string  Provider        { get; set; } = string.Empty;

    /// <summary>Event ID returned by the provider API after a successful create/PATCH (AC-001, AC-002).</summary>
    public string? ExternalEventId { get; set; }

    /// <summary>"Synced" | "Failed" | "Deleted".</summary>
    public string  Status    { get; set; } = "Synced";

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Booking Booking { get; set; } = null!;
}
