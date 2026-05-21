import type { SlotDto } from './slotsApi'

const API_BASE = '/api'

// ── Types ─────────────────────────────────────────────────────────────────────────────────────────

/**
 * Success shape returned by `POST /api/bookings` HTTP 201 (us_020; AC-001).
 * `bookingId` is the integer PK serialised as a number by ASP.NET Core JSON.
 */
export interface BookingCreatedResponse {
  bookingId: number
  status:    string    // "Confirmed"
  slot:      SlotDto
}

/**
 * Discriminated-union error type for all non-2xx responses from `POST /api/bookings`.
 *
 * - `SLOT_UNAVAILABLE`  — 409 concurrent conflict; `alternatives` contains up to 3 nearby slots (AC-002; UXR-602)
 * - `SLOT_BLOCKED`      — 409 slot blocked by admin; no alternatives (Edge: slot Blocked)
 * - `DUPLICATE_WINDOW`  — 409 patient already has an active booking overlapping this slot (AC-004)
 * - `LOCK_TIMEOUT`      — 503 row-lock contention; patient should retry (Edge: lock timeout)
 */
export type BookingConflictError =
  | { errorCode: 'SLOT_UNAVAILABLE';  message: string; alternatives: SlotDto[] }
  | { errorCode: 'SLOT_BLOCKED';      message: string }
  | { errorCode: 'DUPLICATE_WINDOW';  message: string }
  | { errorCode: 'LOCK_TIMEOUT';      message: string }

// ── API wrapper ───────────────────────────────────────────────────────────────────────────────────

/**
 * `POST /api/bookings` — reserves the given slot for the authenticated patient (us_020; AC-001).
 *
 * Resolves with {@link BookingCreatedResponse} on HTTP 201.
 * Rejects with {@link BookingConflictError} on 409 or 503 — callers must narrow by `errorCode`.
 *
 * @param accessToken JWT Bearer token from AuthContext.
 * @param slotId      Integer primary key of the appointment_slots row to reserve.
 */
export async function createBooking(
  accessToken: string,
  slotId:      number,
): Promise<BookingCreatedResponse> {
  const res = await fetch(`${API_BASE}/bookings`, {
    method:  'POST',
    headers: {
      'Content-Type':  'application/json',
      'Authorization': `Bearer ${accessToken}`,
    },
    body: JSON.stringify({ slotId }),
  })

  if (res.status === 201) {
    return res.json() as Promise<BookingCreatedResponse>
  }

  if (res.status === 409) {
    const body = await res.json() as { error: string; alternatives?: SlotDto[] | null }

    // Discriminate 409 subtypes by alternatives presence and error text content
    if (Array.isArray(body.alternatives)) {
      const err: BookingConflictError = {
        errorCode:    'SLOT_UNAVAILABLE',
        message:      body.error,
        alternatives: body.alternatives,
      }
      throw err
    }

    if (body.error.toLowerCase().includes('time window')) {
      const err: BookingConflictError = { errorCode: 'DUPLICATE_WINDOW', message: body.error }
      throw err
    }

    // Catch-all 409 — treat as slot blocked (admin action, no alternatives)
    const err: BookingConflictError = { errorCode: 'SLOT_BLOCKED', message: body.error }
    throw err
  }

  if (res.status === 503) {
    const body = await res.json().catch(() => ({ error: 'Service temporarily unavailable.' })) as { error: string }
    const err: BookingConflictError = {
      errorCode: 'LOCK_TIMEOUT',
      message:   body.error ?? 'Booking could not be processed. Please try again.',
    }
    throw err
  }

  // Unexpected status — surface the HTTP status text
  throw new Error(`Booking request failed: ${res.status} ${res.statusText}`)
}
