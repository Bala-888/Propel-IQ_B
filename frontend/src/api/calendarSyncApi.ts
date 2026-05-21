/**
 * Typed fetch wrappers for the calendar sync endpoints (us_028; AC-001, AC-002; Edge: token expired).
 *
 * All functions accept the JWT `accessToken` as a parameter — no global mutable state (OWASP A01).
 * A non-202 / non-2xx response throws `CalendarSyncError` so callers can set their status state
 * without relying on error boundary propagation.
 */

const BASE = '/api/calendar'

// ── Shared error type ────────────────────────────────────────────────────────────────────────────

/** Thrown when the calendar sync endpoint returns a non-202 / non-2xx response. */
export class CalendarSyncError extends Error {
  constructor(public readonly status: number) {
    super(`Calendar sync request failed with HTTP ${status}`)
    this.name = 'CalendarSyncError'
    Object.setPrototypeOf(this, new.target.prototype)
  }
}

// ── Types ────────────────────────────────────────────────────────────────────────────────────────

export type CalendarProvider = 'Google' | 'Outlook'

/**
 * Status values returned by `GET /api/calendar/sync/status/{bookingId}`.
 * `"Pending"` means the worker has not yet processed the command.
 */
export type CalendarSyncStatus = 'Synced' | 'Failed' | 'TokenExpired' | 'Pending'

// ── API functions ────────────────────────────────────────────────────────────────────────────────

/**
 * `POST /api/calendar/sync` — enqueues a calendar sync command.
 *
 * Returns `void` on 202 Accepted (the actual API call runs asynchronously in the worker).
 * Throws {@link CalendarSyncError} on any non-202 response (5xx, 403, 400, etc.).
 *
 * @param bookingId  Numeric booking PK.
 * @param provider   "Google" or "Outlook".
 * @param accessToken JWT access token (Bearer).
 */
export async function triggerCalendarSync(
  bookingId:   number,
  provider:    CalendarProvider,
  accessToken: string,
): Promise<void> {
  const response = await fetch(`${BASE}/sync`, {
    method:  'POST',
    headers: {
      'Content-Type':  'application/json',
      'Authorization': `Bearer ${accessToken}`,
    },
    body: JSON.stringify({ bookingId, provider }),
  })

  if (response.status !== 202) {
    throw new CalendarSyncError(response.status)
  }
}

/**
 * `GET /api/calendar/sync/status/{bookingId}?provider={provider}` — polls for the sync outcome.
 *
 * Returns `"Pending"` when no `booking_calendar_syncs` row exists yet (worker has not run).
 * Returns `"TokenExpired"` when the server indicates the stored OAuth token could not be refreshed.
 *
 * Throws {@link CalendarSyncError} on non-2xx responses.
 *
 * @param bookingId  Numeric booking PK.
 * @param provider   "Google" or "Outlook".
 * @param accessToken JWT access token (Bearer).
 */
export async function getCalendarSyncStatus(
  bookingId:   number,
  provider:    CalendarProvider,
  accessToken: string,
): Promise<CalendarSyncStatus> {
  const url = `${BASE}/sync/status/${encodeURIComponent(String(bookingId))}?provider=${encodeURIComponent(provider)}`
  const response = await fetch(url, {
    headers: { 'Authorization': `Bearer ${accessToken}` },
  })

  if (!response.ok) {
    throw new CalendarSyncError(response.status)
  }

  const data = await response.json() as { status: CalendarSyncStatus }
  return data.status ?? 'Pending'
}
