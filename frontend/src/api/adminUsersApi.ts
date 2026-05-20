/**
 * API client for the /admin/users endpoints.
 * All functions accept the JWT accessToken as a parameter — no global state (OWASP A01).
 * Caller is responsible for providing a valid token; a 401 response propagates as an
 * Error so the session-expired flow can handle it at the call site.
 */

export interface AdminUser {
  id: number
  name: string
  email: string
  role: 'Patient' | 'Staff' | 'Admin'
  isActive: boolean
  createdAt: string
}

export interface CreateUserBody {
  name: string
  email: string
  role: string
}

export interface PatchUserBody {
  role?: string
  isActive?: boolean
}

/** Thrown by createUser when the server responds 409 (duplicate email). */
export class DuplicateEmailError extends Error {
  constructor() {
    super('A user with this email already exists')
    this.name = 'DuplicateEmailError'
  }
}

const BASE = '/api/admin/users'

function authHeaders(accessToken: string): HeadersInit {
  return {
    'Content-Type': 'application/json',
    Authorization: `Bearer ${accessToken}`,
  }
}

export async function getUsers(accessToken: string): Promise<AdminUser[]> {
  const res = await fetch(BASE, { headers: authHeaders(accessToken) })
  if (!res.ok) throw new Error(`GET /admin/users failed: ${res.status}`)
  return res.json() as Promise<AdminUser[]>
}

export async function createUser(
  accessToken: string,
  body: CreateUserBody,
): Promise<{ userId: number }> {
  const res = await fetch(BASE, {
    method: 'POST',
    headers: authHeaders(accessToken),
    body: JSON.stringify(body),
  })
  if (res.status === 409) throw new DuplicateEmailError()
  if (!res.ok) throw new Error(`POST /admin/users failed: ${res.status}`)
  return res.json() as Promise<{ userId: number }>
}

export async function patchUser(
  accessToken: string,
  id: number,
  body: PatchUserBody,
): Promise<void> {
  const res = await fetch(`${BASE}/${id}`, {
    method: 'PATCH',
    headers: authHeaders(accessToken),
    body: JSON.stringify(body),
  })
  if (!res.ok) throw new Error(`PATCH /admin/users/${id} failed: ${res.status}`)
}
