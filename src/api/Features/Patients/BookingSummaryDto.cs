using System.Linq.Expressions;
using Api.Data.Entities;

namespace Api.Features.Patients;

/// <summary>
/// Active booking entry in the 360° patient summary (us_040/AC-002).
/// SlotStart/SlotEnd accessed via AppointmentSlot navigation — EF Core emits a JOIN (AC-003).
/// </summary>
public sealed class BookingSummaryDto
{
    public int Id { get; set; }
    public DateTime SlotStart { get; set; }
    public DateTime SlotEnd { get; set; }
    public string? ProviderName { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Priority { get; set; }

    /// <summary>
    /// EF Core projection selector — translates to a SQL SELECT without materialising full entities.
    /// AppointmentSlot navigation is accessed without a separate Include because EF Core's
    /// Select can cross navigation properties in a single query (AC-003 — no extra round trip).
    /// </summary>
    public static Expression<Func<Booking, BookingSummaryDto>> Selector =>
        b => new BookingSummaryDto
        {
            Id           = b.Id,
            SlotStart    = b.AppointmentSlot.SlotStart,
            SlotEnd      = b.AppointmentSlot.SlotEnd,
            ProviderName = b.AppointmentSlot.ProviderName,
            Status       = b.Status,
            Priority     = b.Priority,
        };
}
