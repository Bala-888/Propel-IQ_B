const API_BASE = '/api'

// ── Shared types ──────────────────────────────────────────────────────────────────────────────────

export interface IntakeSummary {
  demographics: string | null
  medicalHistory: string | null
  medications: string | null
  allergies: string | null
  chiefComplaint: string | null
}

/**
 * A single message in the chat UI. Stored exclusively in React component state — never
 * written to localStorage, sessionStorage, IndexedDB, or any browser-persistent storage
 * (AIR guardrails; OWASP A02; HIPAA minimum-necessary; checklist).
 */
export interface ChatMessage {
  id: string
  role: 'ai' | 'patient'
  content: string
}

export interface StartSessionResponse {
  sessionId: string
  message: string
}

export interface SendMessageResponse {
  message: string
  allFieldsCollected: boolean
  summary?: IntakeSummary
}

// ── Typed errors ──────────────────────────────────────────────────────────────────────────────────

/** Thrown when the backend returns HTTP 503 (Ollama model not loaded). */
export class AiUnavailableError extends Error {
  constructor() {
    super('AI intake is temporarily unavailable. You can use the manual form instead.')
    this.name = 'AiUnavailableError'
    Object.setPrototypeOf(this, new.target.prototype)
  }
}

/** Thrown when the backend returns HTTP 504 (Ollama inference timeout). */
export class AiTimeoutError extends Error {
  constructor() {
    super('The AI took too long to respond. Please try your message again.')
    this.name = 'AiTimeoutError'
    Object.setPrototypeOf(this, new.target.prototype)
  }
}

/**
 * Thrown when `PATCH /intake/ai/field` returns HTTP 400 — a required field was submitted
 * as empty (Edge: empty required field; us_016-II/AC-002).
 * `message` contains the API error text (e.g. "Chief complaint cannot be empty").
 */
export class RequiredFieldError extends Error {
  constructor(message: string) {
    super(message)
    this.name = 'RequiredFieldError'
    Object.setPrototypeOf(this, new.target.prototype)
  }
}

/**
 * Thrown when `POST /intake/ai/confirm` returns HTTP 410 — the session TTL elapsed before
 * the patient confirmed (Edge: session expired; us_016-II/AC-003).
 */
export class SessionExpiredError extends Error {
  constructor() {
    super('Session expired. Your draft has been saved.')
    this.name = 'SessionExpiredError'
    Object.setPrototypeOf(this, new.target.prototype)
  }
}

// ── API wrappers ──────────────────────────────────────────────────────────────────────────────────

/**
 * POST /intake/ai/start
 * Creates a new intake session and returns the model's opening question.
 *
 * @throws {AiUnavailableError} HTTP 503 — Ollama model not loaded.
 * @throws {AiTimeoutError}     HTTP 504 — inference timed out.
 * @throws {Error}              HTTP 401 / 4xx / 5xx.
 */
export async function startSession(accessToken: string): Promise<StartSessionResponse> {
  const res = await fetch(`${API_BASE}/intake/ai/start`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${accessToken}`,
    },
  })

  if (res.status === 503) throw new AiUnavailableError()
  if (res.status === 504) throw new AiTimeoutError()
  if (res.status === 401) throw new Error('Authentication required.')

  if (!res.ok) {
    let message = `Unexpected error (${res.status})`
    try {
      const err = (await res.json()) as { error?: string }
      message = err.error ?? message
    } catch { /* ignore JSON parse failure */ }
    throw new Error(message)
  }

  return res.json() as Promise<StartSessionResponse>
}

/**
 * POST /intake/ai/message
 * Sends a patient message within an existing session and returns the AI reply.
 *
 * @throws {AiUnavailableError} HTTP 503 — Ollama model not loaded; session is preserved.
 * @throws {AiTimeoutError}     HTTP 504 — inference timed out; session is preserved; patient can retry.
 * @throws {Error}              HTTP 401 / 404 / 4xx / 5xx.
 */
export async function sendMessage(
  accessToken: string,
  sessionId: string,
  text: string,
): Promise<SendMessageResponse> {
  const res = await fetch(`${API_BASE}/intake/ai/message`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${accessToken}`,
    },
    body: JSON.stringify({ sessionId, message: text }),
  })

  if (res.status === 503) throw new AiUnavailableError()
  if (res.status === 504) throw new AiTimeoutError()
  if (res.status === 401) throw new Error('Authentication required.')
  if (res.status === 404) throw new Error('Session expired or not found. Please start a new intake.')

  if (!res.ok) {
    let message = `Unexpected error (${res.status})`
    try {
      const err = (await res.json()) as { error?: string }
      message = err.error ?? message
    } catch { /* ignore JSON parse failure */ }
    throw new Error(message)
  }

  return res.json() as Promise<SendMessageResponse>
}

/**
 * GET /intake/ai/summary?sessionId=<id>
 * Retrieves the structured field summary for an active session (AC-001; us_016-II).
 *
 * @throws {Error} HTTP 401 / 403 / 404 / 5xx.
 */
export async function getSummary(
  accessToken: string,
  sessionId: string,
): Promise<IntakeSummary> {
  const url = `${API_BASE}/intake/ai/summary?sessionId=${encodeURIComponent(sessionId)}`
  const res = await fetch(url, {
    method: 'GET',
    headers: { Authorization: `Bearer ${accessToken}` },
  })

  if (res.status === 401) throw new Error('Authentication required.')
  if (res.status === 403) throw new Error('Access denied.')
  if (res.status === 404) throw new Error('Session not found or expired.')

  if (!res.ok) {
    let message = `Unexpected error (${res.status})`
    try {
      const err = (await res.json()) as { error?: string }
      message = err.error ?? message
    } catch { /* ignore */ }
    throw new Error(message)
  }

  return res.json() as Promise<IntakeSummary>
}

/**
 * PATCH /intake/ai/field
 * Applies a single field correction to the session state (AC-002; us_016-II).
 * Returns the updated full summary.
 *
 * @throws {RequiredFieldError} HTTP 400 — required field submitted as empty (Edge).
 * @throws {Error}              HTTP 401 / 403 / 404 / 5xx.
 */
export async function patchField(
  accessToken: string,
  sessionId: string,
  fieldPath: string,
  value: string,
): Promise<IntakeSummary> {
  const res = await fetch(`${API_BASE}/intake/ai/field`, {
    method: 'PATCH',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${accessToken}`,
    },
    body: JSON.stringify({ sessionId, fieldPath, value }),
  })

  if (res.status === 400) {
    let message = 'This field cannot be empty.'
    try {
      const err = (await res.json()) as { error?: string }
      message = err.error ?? message
    } catch { /* ignore */ }
    throw new RequiredFieldError(message)
  }

  if (res.status === 401) throw new Error('Authentication required.')
  if (res.status === 403) throw new Error('Access denied.')
  if (res.status === 404) throw new Error('Session not found or expired.')

  if (!res.ok) {
    let message = `Unexpected error (${res.status})`
    try {
      const err = (await res.json()) as { error?: string }
      message = err.error ?? message
    } catch { /* ignore */ }
    throw new Error(message)
  }

  return res.json() as Promise<IntakeSummary>
}

/**
 * POST /intake/ai/confirm
 * Promotes the intake record to Complete and evicts the session (AC-003; AC-004; us_016-II).
 *
 * @throws {SessionExpiredError} HTTP 410 — session TTL elapsed before confirmation (Edge).
 * @throws {Error}               HTTP 401 / 403 / 5xx.
 */
export async function confirmIntake(
  accessToken: string,
  sessionId: string,
): Promise<void> {
  const res = await fetch(`${API_BASE}/intake/ai/confirm`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${accessToken}`,
    },
    body: JSON.stringify({ sessionId }),
  })

  if (res.status === 410) throw new SessionExpiredError()
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
}
