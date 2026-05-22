const API_BASE = '/api'

export interface ResolveConflictPayload {
  resolution: 'Resolved' | 'Dismissed'
  note:       string | null
}

/**
 * `PATCH /api/clinical-conflicts/{id}/resolve` — resolves or dismisses an open clinical conflict.
 *
 * Resolves with `void` on HTTP 200.
 * Throws an `Error` with `.status` set to the HTTP status code on non-2xx responses.
 * The error `.message` contains the server-returned `{ error }` string (for 400/409 inline display).
 *
 * @param accessToken JWT Bearer token from AuthContext.
 * @param conflictId  UUID of the clinical_conflicts row.
 * @param payload     Resolution action + optional clinical note.
 * @param signal      AbortController signal — cancels the in-flight request if the drawer closes.
 */
export async function resolveConflict(
  accessToken: string,
  conflictId:  string,
  payload:     ResolveConflictPayload,
  signal?:     AbortSignal,
): Promise<void> {
  const res = await fetch(`${API_BASE}/clinical-conflicts/${conflictId}/resolve`, {
    method:  'PATCH',
    headers: {
      'Content-Type':  'application/json',
      'Authorization': `Bearer ${accessToken}`,
    },
    body:   JSON.stringify({ resolution: payload.resolution, note: payload.note }),
    signal,
  })

  if (res.ok) return

  // Extract structured error message — fall back to HTTP status string (OWASP A02 — no PHI in body)
  let message: string
  try {
    const body = await res.json() as { error?: string }
    message = body.error ?? `Request failed (HTTP ${res.status})`
  } catch {
    message = `Request failed (HTTP ${res.status})`
  }

  throw Object.assign(new Error(message), { status: res.status })
}
