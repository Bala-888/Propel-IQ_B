/**
 * Typed fetch wrappers for GET and PATCH patient notification/calendar-sync preferences (us_029).
 *
 * All functions accept `accessToken` as a parameter — no global mutable state (OWASP A01).
 * Non-2xx responses throw `ApiError` so callers can detect PATCH failure and revert the toggle
 * (Edge: PATCH fails; AC-002).
 */

const BASE = '/api/patients'

// ── Types ────────────────────────────────────────────────────────────────────────────────────────

/**
 * The five notification/calendar-sync preference fields persisted by the backend (us_029/AC-004).
 * Keys use camelCase to match the JSON response body from the .NET API (System.Text.Json default).
 */
export interface PatientPreferences {
  emailNotificationsEnabled:    boolean
  smsNotificationsEnabled:      boolean
  slotSwapNotificationsEnabled: boolean
  googleCalendarSyncEnabled:    boolean
  outlookCalendarSyncEnabled:   boolean
}

/** Thrown on any non-2xx response from the preferences endpoints (Edge: PATCH failure detection). */
export class ApiError extends Error {
  constructor(public readonly status: number) {
    super(`Preferences request failed with HTTP ${status}`)
    this.name = 'ApiError'
    Object.setPrototypeOf(this, new.target.prototype)
  }
}

// ── API functions ────────────────────────────────────────────────────────────────────────────────

/**
 * `GET /api/patients/{patientId}/preferences`
 *
 * Returns the current saved preference state for the authenticated patient.
 * Throws {@link ApiError} on any non-2xx response.
 */
export async function getPreferences(
  patientId:   string,
  accessToken: string,
): Promise<PatientPreferences> {
  const res = await fetch(`${BASE}/${patientId}/preferences`, {
    headers: { Authorization: `Bearer ${accessToken}` },
  })
  if (!res.ok) throw new ApiError(res.status)
  return res.json() as Promise<PatientPreferences>
}

/**
 * `PATCH /api/patients/{patientId}/preferences`
 *
 * Sends a partial update containing only the single changed field.
 * Returns the full server-confirmed `PatientPreferences` on 200 (AC-002 — contract reliability).
 * Throws {@link ApiError} on any non-2xx response so the caller can revert its optimistic update
 * (Edge: PATCH fails).
 *
 * @param field  The preference key to update (one of the five `PatientPreferences` keys).
 * @param value  The new boolean value for that field.
 */
export async function patchPreference(
  patientId:   string,
  field:       keyof PatientPreferences,
  value:       boolean,
  accessToken: string,
): Promise<PatientPreferences> {
  const res = await fetch(`${BASE}/${patientId}/preferences`, {
    method:  'PATCH',
    headers: {
      'Content-Type':  'application/json',
      Authorization:   `Bearer ${accessToken}`,
    },
    body: JSON.stringify({ [field]: value }),
  })
  if (!res.ok) throw new ApiError(res.status)
  return res.json() as Promise<PatientPreferences>
}
