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
