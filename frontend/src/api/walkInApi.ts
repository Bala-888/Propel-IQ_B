const API_BASE = '/api'

export interface CreateWalkInRequest {
  patientName: string
  dateOfBirth: string         // ISO-8601 "yyyy-MM-dd"
  createAccount: boolean
  email?: string
  linkExistingAccountId?: number
}

export interface CreateWalkInResponse {
  bookingId: number
  userId?: number
  credentialsEmailFailed: boolean
}

/** Thrown by {@link createWalkIn} when the supplied email already belongs to an existing account. */
export class DuplicateEmailError extends Error {
  constructor(public readonly existingUserId: number) {
    super('An account with this email already exists. Would you like to link this walk-in to that account?')
    this.name = 'DuplicateEmailError'
    Object.setPrototypeOf(this, new.target.prototype)
  }
}

/**
 * POST /walkins
 *
 * @throws {DuplicateEmailError}  HTTP 409 — email already registered; `existingUserId` is populated.
 * @throws {Error}                HTTP 400 / 404 / 5xx — message from the response body.
 */
export async function createWalkIn(
  accessToken: string,
  body: CreateWalkInRequest,
): Promise<CreateWalkInResponse> {
  const res = await fetch(`${API_BASE}/walkins`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${accessToken}`,
    },
    body: JSON.stringify(body),
  })

  if (res.status === 409) {
    const payload = (await res.json()) as { error?: string; existingUserId?: number }
    throw new DuplicateEmailError(payload.existingUserId ?? 0)
  }

  if (!res.ok) {
    let message = `Unexpected error (${res.status})`
    try {
      const err = (await res.json()) as { error?: string; title?: string }
      message = err.error ?? err.title ?? message
    } catch {
      // ignore JSON parse failure — keep default message
    }
    throw new Error(message)
  }

  return res.json() as Promise<CreateWalkInResponse>
}

// ══════════════════════════════════════════════════════════════════════════════════════════════════
// us_030 — new walk-in booking flow (patient search + slot lock + queue position)
// ══════════════════════════════════════════════════════════════════════════════════════════════════

// ── Types ─────────────────────────────────────────────────────────────────────────────────────────

export interface PatientSearchResult {
  patientId:   number
  firstName:   string
  lastName:    string
  dateOfBirth: string | null  // ISO-8601 "yyyy-MM-dd" or null
}

export interface WalkinBookingRequest {
  patientId:          number
  slotId:             number
  reasonForVisit?:    string
  priority?:          string
  overrideDuplicate?: boolean
  phone?:             string
  insuranceProvider?: string
}

export interface WalkinBookingResponse {
  bookingId:     number
  status:        string   // "Confirmed"
  queuePosition: number
}

export interface WalkinPatientRequest {
  firstName:    string
  lastName:     string
  dateOfBirth:  string   // ISO-8601 "yyyy-MM-dd"
  phoneNumber?: string
}

export interface WalkinPatientResponse {
  patientId: number
  firstName: string
  lastName:  string
}

// ── Typed errors ──────────────────────────────────────────────────────────────────────────────────

/** Thrown when the patient already has a Confirmed booking today (edge: duplicate walk-in). */
export class DuplicateTodayError extends Error {
  constructor(public readonly existingBookingId: number) {
    super('This patient already has a confirmed booking today.')
    this.name = 'DuplicateTodayError'
    Object.setPrototypeOf(this, new.target.prototype)
  }
}

/** Thrown when the requested slot is no longer available. */
export class SlotUnavailableError extends Error {
  constructor() {
    super('The selected slot is no longer available.')
    this.name = 'SlotUnavailableError'
    Object.setPrototypeOf(this, new.target.prototype)
  }
}

/** Thrown on 503 lock timeout. */
export class BookingLockTimeoutError extends Error {
  constructor() {
    super('The booking system is temporarily busy. Please try again.')
    this.name = 'BookingLockTimeoutError'
    Object.setPrototypeOf(this, new.target.prototype)
  }
}

// ── API wrappers ──────────────────────────────────────────────────────────────────────────────────

/**
 * `GET /api/patients/search?q=<term>` — typeahead by patient name.
 * Caller must enforce 3-char minimum before calling (OWASP A03; AC-002).
 */
export async function searchPatients(
  accessToken: string,
  q: string,
): Promise<PatientSearchResult[]> {
  const res = await fetch(`${API_BASE}/patients/search?q=${encodeURIComponent(q)}`, {
    headers: { Authorization: `Bearer ${accessToken}` },
  })
  if (!res.ok) {
    const err = await res.json().catch(() => ({ error: `HTTP ${res.status}` })) as { error?: string }
    throw new Error(err.error ?? `HTTP ${res.status}`)
  }
  return res.json() as Promise<PatientSearchResult[]>
}

/**
 * `POST /api/bookings/walkin` — create a confirmed walk-in booking.
 *
 * @throws {DuplicateTodayError}     409 DuplicateBookingToday
 * @throws {SlotUnavailableError}    409 SlotNoLongerAvailable
 * @throws {BookingLockTimeoutError} 503
 */
export async function createWalkinBooking(
  accessToken: string,
  body: WalkinBookingRequest,
): Promise<WalkinBookingResponse> {
  const res = await fetch(`${API_BASE}/bookings/walkin`, {
    method:  'POST',
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${accessToken}` },
    body:    JSON.stringify(body),
  })

  if (res.status === 201) return res.json() as Promise<WalkinBookingResponse>

  if (res.status === 409) {
    const payload = await res.json() as { error?: string; existingBookingId?: number }
    if (payload.error === 'DuplicateBookingToday') throw new DuplicateTodayError(payload.existingBookingId ?? 0)
    throw new SlotUnavailableError()
  }

  if (res.status === 503) throw new BookingLockTimeoutError()

  const err = await res.json().catch(() => ({ error: `HTTP ${res.status}` })) as { error?: string }
  throw new Error(err.error ?? `HTTP ${res.status}`)
}

/**
 * `POST /api/patients/walkin-create` — create a minimal patient record for form pre-fill (AC-004).
 */
export async function createWalkinPatient(
  accessToken: string,
  body: WalkinPatientRequest,
): Promise<WalkinPatientResponse> {
  const res = await fetch(`${API_BASE}/patients/walkin-create`, {
    method:  'POST',
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${accessToken}` },
    body:    JSON.stringify(body),
  })

  if (res.status === 201) return res.json() as Promise<WalkinPatientResponse>

  const err = await res.json().catch(() => ({ error: `HTTP ${res.status}` })) as { error?: string }
  throw new Error(err.error ?? `HTTP ${res.status}`)
}
