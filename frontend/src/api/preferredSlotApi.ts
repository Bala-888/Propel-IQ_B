const API_BASE = '/api'

// ── Types ─────────────────────────────────────────────────────────────────────────────────────────

/** Response body from a successful `POST /api/bookings/{bookingId}/preferred-slot` (us_024; AC-001). */
export interface SetPreferredSlotResponse {
  preferredSlotId: number
  status:          string  // "Registered"
}

/**
 * Typed error thrown by {@link setPreferredSlot} on non-201 responses.
 * Callers should narrow on {@link httpStatus} to route to the correct inline feedback:
 * - `400` → same-as-active guard (defensive; should not occur when UI filters correctly)
 * - `409` → booking not confirmed OR slot no longer available (error message distinguishes)
 * - `403` → ownership failure
 */
export class ApiError extends Error {
  /** HTTP status code from the API response. */
  readonly httpStatus: number

  constructor(httpStatus: number, message: string) {
    super(message)
    this.name       = 'ApiError'
    this.httpStatus = httpStatus
  }
}

// ── API wrapper ───────────────────────────────────────────────────────────────────────────────────

/**
 * `POST /api/bookings/{bookingId}/preferred-slot` — registers or replaces the preferred
 * alternative slot for a confirmed booking (us_024; AC-001).
 *
 * Resolves with {@link SetPreferredSlotResponse} on HTTP 201.
 * Throws {@link ApiError} carrying the HTTP status and server error message on all non-201 responses
 * so the caller can display precise inline feedback (Edge: 409 slot unavailable, AC-004).
 *
 * @param accessToken JWT Bearer token from AuthContext.
 * @param bookingId   Integer primary key of the booking to associate the preferred slot with.
 * @param slotId      Integer primary key of the preferred alternative appointment slot.
 */
export async function setPreferredSlot(
  accessToken: string,
  bookingId:   number,
  slotId:      number,
): Promise<SetPreferredSlotResponse> {
  const res = await fetch(`${API_BASE}/bookings/${bookingId}/preferred-slot`, {
    method:  'POST',
    headers: {
      'Content-Type':  'application/json',
      'Authorization': `Bearer ${accessToken}`,
    },
    body: JSON.stringify({ slotId }),
  })

  if (res.status === 201) {
    return res.json() as Promise<SetPreferredSlotResponse>
  }

  // Extract server-provided error message; fall back to a generic message on parse failure
  let message = `Request failed with status ${res.status}`
  try {
    const body = await res.json() as { error?: string }
    if (body.error) message = body.error
  } catch { /* ignore JSON parse failure */ }

  throw new ApiError(res.status, message)
}
