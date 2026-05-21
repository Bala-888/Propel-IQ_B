const API_BASE = '/api'

// ── Types ─────────────────────────────────────────────────────────────────────────────────────────

/**
 * Single entry in the same-day queue, mirroring `QueueEntryDto` from the backend (us_031/AC-001).
 *
 * `arrivalTime` is `null` until staff marks the patient as CheckedIn via
 * `PATCH /bookings/{id}/status`; the Wait Time column renders "—" for null values (AC-003).
 * `noShowRiskTier` is always a non-null union member — the API normalises missing scores to
 * "Unknown" before serialisation (AC-002; Edge: null risk).
 */
export interface QueueEntry {
  /** Integer PK of the booking row — used as the path segment for PATCH /api/queue/{id}/arrived (us_032). */
  id:              number
  position:        number
  patientName:     string
  /** ISO-8601 DateTimeOffset — null if patient has not yet checked in (ArrivalTime is nullable at API). */
  arrivalTime:     string | null
  /** ISO-8601 DateTimeOffset */
  appointmentTime: string
  noShowRiskTier:  'High' | 'Medium' | 'Low' | 'Unknown'
  status:          string
}

/** Thrown by {@link getQueue} on any non-2xx response. */
export class ApiError extends Error {
  constructor(public readonly status: number, message: string) {
    super(message)
    this.name = 'ApiError'
    Object.setPrototypeOf(this, new.target.prototype)
  }
}

// ── API wrapper ───────────────────────────────────────────────────────────────────────────────────

/**
 * `GET /api/queue?date={date}&since={since}` — returns today's Confirmed and CheckedIn bookings ordered by position.
 *
 * @param date  Optional ISO date string (yyyy-MM-dd); server defaults to today when absent.
 * @param opts  Optional query options:
 *   - `since`: ISO-8601 DateTimeOffset string; when provided, only entries created or updated
 *              at or after this timestamp are returned (used for reconnection fallback; AC-004).
 *              URL-encoded before appending to prevent query-string injection (OWASP A03).
 *
 * @throws {ApiError} on non-2xx responses.
 */
export async function getQueue(accessToken: string, date?: string, opts?: { since?: string }): Promise<QueueEntry[]> {
  let params = date ? `?date=${encodeURIComponent(date)}` : ''
  if (opts?.since) {
    params += (params ? '&' : '?') + `since=${encodeURIComponent(opts.since)}`
  }
  const res = await fetch(`${API_BASE}/queue${params}`, {
    headers: { Authorization: `Bearer ${accessToken}` },
  })

  if (!res.ok) {
    let message = `Unexpected error (${res.status})`
    try {
      const err = (await res.json()) as { error?: string; title?: string }
      message = err.error ?? err.title ?? message
    } catch {
      // ignore JSON parse failure — keep default message
    }
    throw new ApiError(res.status, message)
  }

  return res.json() as Promise<QueueEntry[]>
}

// ── Mark Arrived ──────────────────────────────────────────────────────────────────────────────────

/** Shape of `PATCH /api/queue/{id}/arrived` 200 response (us_032/AC-002). */
export interface ArrivedResponse {
  /** Always "CheckedIn" on success. */
  status:    string
  /** Server-assigned UTC ISO-8601 timestamp of check-in (AC-003 — never client-supplied). */
  arrivedAt: string
}

/**
 * `PATCH /api/queue/{id}/arrived` — marks a booking as arrived.
 *
 * Sends no request body: the server assigns the timestamp server-side (AC-003).
 * Idempotent: returns 200 for both the transition and an already-arrived booking (AC-004).
 *
 * @throws {ApiError} on non-2xx responses.
 */
export async function markArrived(accessToken: string, bookingId: number): Promise<ArrivedResponse> {
  const res = await fetch(`${API_BASE}/queue/${bookingId}/arrived`, {
    method:  'PATCH',
    headers: { Authorization: `Bearer ${accessToken}` },
    // No body — client must not supply a timestamp (AC-003 — server-side timestamp enforced at API layer)
  })

  if (!res.ok) {
    let message = `Unexpected error (${res.status})`
    try {
      const err = (await res.json()) as { error?: string; title?: string }
      message = err.error ?? err.title ?? message
    } catch {
      // ignore JSON parse failure — keep default message
    }
    throw new ApiError(res.status, message)
  }

  return res.json() as Promise<ArrivedResponse>
}
