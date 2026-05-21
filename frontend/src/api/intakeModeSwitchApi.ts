const API_BASE = '/api'

// ── Types ─────────────────────────────────────────────────────────────────────────────────────────

/** Source/target mode for a mode switch. */
export type IntakeMode = 'AI' | 'Manual'

/**
 * Request body for `POST /api/intake/mode-switch` (us_018; AC-001; AC-002).
 * `sessionId` is required when `from = "AI"` — identifies which AI session to read from cache.
 * Omitted when `from = "Manual"` (source data is the DB Draft record keyed by patientId).
 */
export interface ModeSwitchRequest {
  from:       IntakeMode
  to:         IntakeMode
  sessionId?: string
}

/**
 * Response body from `POST /api/intake/mode-switch`.
 *
 * - `mappedFields` — pre-populated data in the target mode's shape.
 *   AI→Manual: `ManualIntakeDraft`-shaped object (chiefComplaint.description mapped; others in reviewItems).
 *   Manual→AI: `AiSessionFields`-shaped object (structured sections serialised to free-text blobs).
 *   `null` when the source contained no collectible data.
 *
 * - `reviewItems` — labelled AI free-text content that has no direct manual field mapping (AC-003).
 *   Always present; empty array when all content was mapped or source was empty.
 *
 * - `cacheVersion` — one-time GUID token; stored in a ref by `ManualIntakeForm` and compared
 *   before each subsequent `POST /api/intake/draft` auto-save to suppress stale writes
 *   (Edge: concurrent auto-save; OWASP A04).
 *
 * - `newSessionId` — new AI session GUID; present only on Manual→AI switch (AC-002).
 */
export interface ModeSwitchResponse {
  mappedFields: Record<string, unknown> | null
  reviewItems:  string[]
  cacheVersion: string
  newSessionId?: string
}

// ── API wrapper ───────────────────────────────────────────────────────────────────────────────────

/**
 * `POST /api/intake/mode-switch` — transfers collected intake data from the source mode to the
 * target mode and returns pre-populated data for the target screen (us_018; AC-001; AC-002).
 *
 * PHI data is in the request/response body only — never written to localStorage,
 * sessionStorage, or any browser-persistent storage
 * (OWASP A02; HIPAA minimum-necessary; AIR guardrails).
 *
 * @throws {Error} on HTTP 400 (invalid request), 401 (unauthenticated), 403 (wrong session owner),
 *   or unexpected 5xx.
 */
export async function switchMode(
  accessToken: string,
  req:         ModeSwitchRequest,
): Promise<ModeSwitchResponse> {
  const res = await fetch(`${API_BASE}/intake/mode-switch`, {
    method:  'POST',
    headers: {
      'Content-Type': 'application/json',
      Authorization:  `Bearer ${accessToken}`,
    },
    body: JSON.stringify(req),
  })

  if (res.status === 401) throw new Error('Authentication required.')
  if (res.status === 403) throw new Error('You are not authorised to access this session.')

  if (!res.ok) {
    let message = `Mode switch failed (${res.status})`
    try {
      const err = (await res.json()) as { error?: string }
      message = err.error ?? message
    } catch { /* ignore body parse errors */ }
    throw new Error(message)
  }

  return res.json() as Promise<ModeSwitchResponse>
}
