const API_BASE = '/api'

// ── Shared types ──────────────────────────────────────────────────────────────────────────────────

export interface DemographicsDraft {
  firstName?:   string
  lastName?:    string
  dateOfBirth?: string
  gender?:      string
  phone?:       string
  address?:     string
}

export interface ConditionItemDraft {
  name?:         string
  diagnosedDate?: string
}

export interface SurgeryItemDraft {
  name?: string
  date?: string
}

export interface MedicalHistoryDraft {
  conditions?:       ConditionItemDraft[]
  surgeries?:        SurgeryItemDraft[]
  lastPhysicalExam?: string
}

export interface MedicationItemDraft {
  name?:      string
  dosage?:    string
  frequency?: string
}

export interface AllergyItemDraft {
  allergen?: string
  reaction?: string
}

/**
 * Partial intake record returned by `GET /api/intake/draft` — all sections optional.
 * Used by `ManualIntakeForm` to pre-populate fields when a draft exists (AC-005).
 * PHI values are held exclusively in React component state — never written to
 * localStorage, sessionStorage, or any browser-persistent storage
 * (OWASP A02; HIPAA minimum-necessary; checklist).
 */
export interface ManualIntakeDraft {
  demographics?:   DemographicsDraft
  chiefComplaint?: { description?: string }
  medicalHistory?: MedicalHistoryDraft
  medications?:    { items?: MedicationItemDraft[] }
  allergies?:      { items?: AllergyItemDraft[] }
}

/**
 * Complete intake data sent to `POST /api/intake/manual` (AC-002).
 * Matches the shape of `CreateManualIntakeRequest` on the backend.
 */
export interface ManualIntakeData {
  demographics:   Required<DemographicsDraft>
  chiefComplaint: { description: string }
  medicalHistory: { conditions: ConditionItemDraft[]; surgeries: SurgeryItemDraft[]; lastPhysicalExam: string }
  medications:    { items: MedicationItemDraft[] }
  allergies:      { items: AllergyItemDraft[] }
}

/**
 * 201 response from `POST /api/intake/manual`.
 * `warnings` is present when at least one advisory applies (e.g. brand-only medication).
 */
export interface ManualIntakeResponse {
  id:        number
  warnings?: string[]
}

// ── API wrappers ──────────────────────────────────────────────────────────────────────────────────

/**
 * `GET /api/intake/draft` — returns the patient's saved draft for form pre-population (AC-005).
 * Returns `null` on HTTP 404 (no draft exists) so the caller can render an empty form.
 *
 * @throws {Error} on HTTP 401, 403, or unexpected 5xx.
 */
export async function getDraft(accessToken: string): Promise<ManualIntakeDraft | null> {
  const res = await fetch(`${API_BASE}/intake/draft`, {
    method:  'GET',
    headers: { Authorization: `Bearer ${accessToken}` },
  })

  if (res.status === 404) return null
  if (res.status === 401) throw new Error('Authentication required.')
  if (res.status === 403) throw new Error('Access denied.')

  if (!res.ok) {
    let message = `Unexpected error (${res.status})`
    try {
      const err = (await res.json()) as { error?: string }
      message = err.error ?? message
    } catch { /* ignore */ }
    throw new Error(message)
  }

  return res.json() as Promise<ManualIntakeDraft>
}

/**
 * `POST /api/intake/draft` — fire-and-forget upsert of partial intake data triggered on
 * navigation-away (AC-004).
 * Errors are swallowed by the caller; PHI data is in the request body only (OWASP A02).
 *
 * An optional `cacheVersion` token received from a prior `POST /api/intake/mode-switch`
 * response is forwarded as the `X-Cache-Version` request header. The backend uses this to
 * discard stale auto-save writes that arrive after a mode switch
 * (Edge: concurrent auto-save; OWASP A04; us_018).
 *
 * @throws {Error} on HTTP 401 or unexpected 5xx.
 */
export async function postDraft(
  accessToken:  string,
  data:         Partial<ManualIntakeData>,
  cacheVersion?: string,
): Promise<void> {
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    Authorization:  `Bearer ${accessToken}`,
  }
  if (cacheVersion) {
    headers['X-Cache-Version'] = cacheVersion
  }

  const res = await fetch(`${API_BASE}/intake/draft`, {
    method:  'POST',
    headers,
    body: JSON.stringify(data),
  })

  if (res.status === 401) throw new Error('Authentication required.')
  if (!res.ok) throw new Error(`Draft save failed (${res.status})`)
}

/**
 * `POST /api/intake/manual` — submits the complete validated intake form (AC-002).
 * Returns `ManualIntakeResponse` with the persisted record ID and optional advisory warnings.
 *
 * @throws {Error} on HTTP 401, 403, 422 (validation — should not reach here after client-side
 *   validation), or unexpected 5xx.
 */
export async function postManualIntake(
  accessToken: string,
  data:        ManualIntakeData,
): Promise<ManualIntakeResponse> {
  const res = await fetch(`${API_BASE}/intake/manual`, {
    method:  'POST',
    headers: {
      'Content-Type':  'application/json',
      Authorization:   `Bearer ${accessToken}`,
    },
    body: JSON.stringify(data),
  })

  if (res.status === 401) throw new Error('Authentication required.')
  if (res.status === 403) throw new Error('Access denied.')
  if (res.status === 422) {
    let message = 'Validation failed. Please review your submission.'
    try {
      const err = (await res.json()) as { title?: string }
      message = err.title ?? message
    } catch { /* ignore */ }
    throw new Error(message)
  }

  if (!res.ok) {
    let message = `Unexpected error (${res.status})`
    try {
      const err = (await res.json()) as { error?: string }
      message = err.error ?? message
    } catch { /* ignore */ }
    throw new Error(message)
  }

  return res.json() as Promise<ManualIntakeResponse>
}
