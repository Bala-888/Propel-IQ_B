namespace Api.Services;

// ── Result discriminated union ────────────────────────────────────────────────────────────────────

/// <summary>Discriminated-union result returned by <see cref="IPreferredSlotService.SetPreferredSlotAsync"/>.</summary>
public abstract record SetPreferredSlotResult { }

/// <summary>Preferred slot registered (or replaced) successfully (AC-001, AC-002).</summary>
public sealed record PreferredSlotRegistered(int PreferredSlotId) : SetPreferredSlotResult;

/// <summary>Patient does not own the booking — return 403 (OWASP A01; A07).</summary>
public sealed record PreferredSlotForbidden : SetPreferredSlotResult;

/// <summary>Booking status is not <c>"Confirmed"</c> — return 409 (AC-004).</summary>
public sealed record PreferredSlotBookingNotConfirmed : SetPreferredSlotResult;

/// <summary>Requested slot equals the booking's active slot — return 400 (AC-003).</summary>
public sealed record PreferredSlotSameAsActive : SetPreferredSlotResult;

/// <summary>Requested slot is no longer available — return 409 (Edge: slot unavailable).</summary>
public sealed record PreferredSlotSlotNotAvailable : SetPreferredSlotResult;

// ── Interface ─────────────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Registers or replaces the preferred alternative slot for a confirmed booking (us_024).
/// </summary>
public interface IPreferredSlotService
{
    /// <summary>
    /// Applies ownership → status → same-slot → availability guards in order,
    /// then upserts the <c>preferred_slots</c> row.
    /// </summary>
    Task<SetPreferredSlotResult> SetPreferredSlotAsync(
        int               bookingId,
        int               patientId,
        int               slotId,
        CancellationToken ct = default);
}
