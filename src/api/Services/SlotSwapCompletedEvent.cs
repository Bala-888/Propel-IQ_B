namespace Api.Services;

/// <summary>
/// Domain event enqueued on the <see cref="System.Threading.Channels.Channel{T}"/> after a
/// successful preferred-slot swap commit (us_025; AC-005).
///
/// Consumed by the us_026 notification worker which sends a slot-change notification to the patient.
///
/// PHI guardrail: only opaque integer identifiers are stored — no patient name, medical data,
/// or other PHI is included (OWASP A02; HIPAA minimum-necessary).
/// </summary>
/// <param name="PatientId">ID of the patient whose booking was swapped.</param>
/// <param name="OldSlotId">ID of the <c>appointment_slots</c> row the patient held before the swap.</param>
/// <param name="NewSlotId">ID of the <c>appointment_slots</c> row the patient now holds after the swap.</param>
/// <param name="NewBookingId">PK of the newly created confirmed booking on the preferred slot.
/// Used by the us_026 notification worker to load fresh booking details from the DB rather than
/// trusting stale event-payload fields (Edge: payload freshness; AC-001).</param>
public sealed record SlotSwapCompletedEvent(
    int PatientId,
    int OldSlotId,
    int NewSlotId,
    int NewBookingId);
