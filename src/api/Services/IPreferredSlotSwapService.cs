namespace Api.Services;

// ── Discriminated-union result returned by IPreferredSlotSwapService.SwapAsync ────────────────────

/// <summary>Base type for the swap result union.</summary>
public abstract class SwapResult { }

/// <summary>
/// The ACID swap committed successfully.  All four writes landed:
/// original booking cancelled, new booking confirmed on the preferred slot,
/// preferred slot marked booked, original slot restored to available.
/// </summary>
public sealed class SwapSuccess : SwapResult
{
    /// <summary>Patient ID — used to correlate the <see cref="SlotSwapCompletedEvent"/> (AC-005).</summary>
    public int PatientId    { get; init; }
    /// <summary>Original slot ID that was freed up.</summary>
    public int OldSlotId    { get; init; }
    /// <summary>Preferred slot ID now assigned to the patient.</summary>
    public int NewSlotId    { get; init; }
    /// <summary>Newly created booking ID on the preferred slot.</summary>
    public int NewBookingId { get; init; }
}

/// <summary>
/// Candidate was skipped without modifying any row.
/// Reasons: slot became unavailable (Blocked/Booked) since the candidate query ran,
/// or the preferred-slot row was already processed by a concurrent tick.
/// The <c>preferred_slots</c> row is preserved for re-evaluation on the next cycle (Edge: Blocked; AC-004).
/// </summary>
public sealed class SwapSkipped : SwapResult { }

/// <summary>
/// An unexpected exception caused the transaction to roll back.
/// Neither the original booking nor the <c>preferred_slots</c> row was modified (AC-004).
/// The candidate will be re-evaluated on the next cycle.
/// </summary>
public sealed class SwapFailed : SwapResult { }

// ── Interface ─────────────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Executes the ACID four-write swap for a single preferred-slot candidate (us_025; AC-002).
/// </summary>
public interface IPreferredSlotSwapService
{
    /// <summary>
    /// Atomically cancels the original booking, creates a new confirmed booking on the preferred
    /// slot, marks the preferred slot as booked, and restores the original slot to available.
    ///
    /// <para>
    /// The slot is re-fetched inside the transaction with <c>SELECT … FOR UPDATE</c> to prevent
    /// concurrent double-booking.  If the slot is no longer available, the transaction is rolled
    /// back and the <c>preferred_slots</c> row is left untouched (Edge: Blocked; AC-004).
    /// </para>
    /// </summary>
    /// <param name="preferredSlotId">PK of the <c>preferred_slots</c> row to process.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <see cref="SwapSuccess"/> on commit; <see cref="SwapSkipped"/> when the slot is
    /// unavailable or the row was already processed; <see cref="SwapFailed"/> on exception.
    /// </returns>
    Task<SwapResult> SwapAsync(int preferredSlotId, CancellationToken ct = default);
}
