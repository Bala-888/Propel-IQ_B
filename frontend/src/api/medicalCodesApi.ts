/**
 * medicalCodesApi — `POST /patients/{id}/medical-codes` and `PATCH /code-suggestions/{id}`
 * (us_044; AC-002, AC-003, AC-004).
 *
 * Raw fetch wrapper following the project's API module pattern (conflictsApi.ts, bookingsApi.ts).
 * OWASP A02: no PHI (corrected code values) is written to any logger in this module.
 */

const API_BASE = '/api'

// ── Types ──────────────────────────────────────────────────────────────────────────────────────────

export interface AcceptCodePayload {
  suggestionId: string
  codeType:     string
  code:         string
  description?: string
  source:       'AI'
  reviewStatus: 'Accepted'
}

export interface CorrectCodePayload {
  suggestionId:  string
  codeType:      string
  code:          string            // original AI code
  description?:  string
  source:        'AI-Corrected'
  reviewStatus:  'Corrected'
  correctedCode: string            // clinician-corrected code
}

export type MedicalCodePayload = AcceptCodePayload | CorrectCodePayload

// ── POST /patients/{id}/medical-codes ─────────────────────────────────────────────────────────────

/**
 * Accepts or corrects a pending code suggestion for the specified patient (us_044; AC-002, AC-004).
 *
 * Resolves with `void` on HTTP 201.
 * Throws an `Error` with `.status` set to the HTTP status code and `.message` set to the
 * server-returned `{ error }` string on non-2xx:
 *   - 400 — invalid code format (AC-005); message contains the format error for inline display.
 *   - 409 — suggestion already reviewed (Edge); message is shown inline in the card.
 *
 * OWASP A02: corrected code value may be PHI-adjacent — never log the payload body.
 *
 * @param accessToken  JWT Bearer token from AuthContext.
 * @param patientId    Patient integer PK (URL segment).
 * @param payload      Accept or Correct payload (discriminated union).
 */
export async function submitMedicalCode(
  accessToken: string,
  patientId:   string | number,
  payload:     MedicalCodePayload,
): Promise<void> {
  const res = await fetch(`${API_BASE}/patients/${patientId}/medical-codes`, {
    method:  'POST',
    headers: {
      'Content-Type':  'application/json',
      'Authorization': `Bearer ${accessToken}`,
    },
    body: JSON.stringify(payload),
  })

  if (res.status === 201) return

  let message: string
  try {
    const body = await res.json() as { error?: string }
    message = body.error ?? `Request failed (HTTP ${res.status})`
  } catch {
    message = `Request failed (HTTP ${res.status})`
  }

  throw Object.assign(new Error(message), { status: res.status })
}

// ── PATCH /code-suggestions/{id} ──────────────────────────────────────────────────────────────────

/**
 * Rejects a pending code suggestion (us_044; AC-003).
 *
 * Resolves with `void` on HTTP 200.
 * Throws an `Error` with `.status` and `.message` on non-2xx:
 *   - 409 — suggestion already reviewed (Edge).
 *   - 404 — suggestion not found.
 *
 * @param accessToken  JWT Bearer token from AuthContext.
 * @param suggestionId UUID of the code_suggestions row.
 */
export async function rejectCodeSuggestion(
  accessToken:  string,
  suggestionId: string,
): Promise<void> {
  const res = await fetch(`${API_BASE}/code-suggestions/${suggestionId}`, {
    method:  'PATCH',
    headers: {
      'Content-Type':  'application/json',
      'Authorization': `Bearer ${accessToken}`,
    },
    // Body not needed — backend infers Rejected from the route (PATCH = reject action)
  })

  if (res.ok) return

  let message: string
  try {
    const body = await res.json() as { error?: string }
    message = body.error ?? `Request failed (HTTP ${res.status})`
  } catch {
    message = `Request failed (HTTP ${res.status})`
  }

  throw Object.assign(new Error(message), { status: res.status })
}
