/**
 * Typed fetch wrapper for `POST /api/documents/upload` (us_035; AC-001, AC-002, AC-004).
 *
 * Accepts `accessToken` as a parameter — no global mutable state (OWASP A01).
 * Error messages are read from the server's `error` field in the JSON response body —
 * strings are not hard-coded here so the API remains authoritative (AC-001, AC-002; checklist).
 * No additional auth headers are added beyond the standard `Authorization: Bearer` token (OWASP A01; A02).
 * `Content-Type` is NOT set manually — the browser assigns `multipart/form-data` with the correct
 * boundary when a `FormData` body is passed (MDN FormData convention).
 */

import type { DocumentStatusDto } from '../types/document'

const BASE = '/api/documents'

// ── Types ─────────────────────────────────────────────────────────────────────────────────────────

/** Success body returned by `POST /api/documents/upload` HTTP 201 (AC-004). */
export interface UploadResult {
  documentId: string   // UUID string from the server
  status:     'Uploaded'
}

// ── Error type ────────────────────────────────────────────────────────────────────────────────────

/**
 * Thrown when the upload endpoint returns a non-201 response.
 * `status` carries the HTTP status code so callers can differentiate 400 vs 413 (AC-001, AC-002).
 * `message` is the server's `error` field — never a hard-coded string (checklist).
 */
export class DocumentApiError extends Error {
  constructor(
    public readonly status:  number,
    message:                 string,
  ) {
    super(message)
    this.name = 'DocumentApiError'
    Object.setPrototypeOf(this, new.target.prototype)
  }
}

// ── API function ──────────────────────────────────────────────────────────────────────────────────

/**
 * `POST /api/documents/upload` — uploads an encrypted patient document (us_035/AC-004).
 *
 * Builds a `FormData` with the file appended as the `"file"` key and POSTs it.
 * Resolves with {@link UploadResult} on HTTP 201.
 * Rejects with {@link DocumentApiError} on 400 (unsupported type) or 413 (size exceeded) or other errors,
 * carrying the server's `error` message string (AC-001, AC-002).
 *
 * @param file        The `File` object selected by the patient.
 * @param accessToken JWT Bearer token from `AuthContext`.
 */
export async function uploadDocument(
  file:        File,
  accessToken: string,
): Promise<UploadResult> {
  const formData = new FormData()
  formData.append('file', file)

  const res = await fetch(`${BASE}/upload`, {
    method:  'POST',
    headers: {
      // Only the Authorization header is added — Content-Type is intentionally omitted so the
      // browser sets multipart/form-data with the correct boundary automatically (OWASP A01; checklist).
      Authorization: `Bearer ${accessToken}`,
    },
    body: formData,
  })

  if (res.status === 201) {
    return res.json() as Promise<UploadResult>
  }

  // Read the server's error message — do NOT hard-code fallback strings that override the API (checklist)
  let serverMessage: string
  try {
    const body = await res.json() as { error?: string }
    serverMessage = body.error ?? `Upload failed with HTTP ${res.status}`
  } catch {
    serverMessage = `Upload failed with HTTP ${res.status}`
  }

  throw new DocumentApiError(res.status, serverMessage)
}

// ── Document status API (us_039; AC-001, AC-004) ──────────────────────────────────────────────────

/**
 * `GET /api/documents` — fetch all patient documents ordered by upload time desc (SCR-010).
 * Ownership is enforced server-side; bearer token identifies the patient (OWASP A01).
 *
 * @param accessToken JWT Bearer token from `AuthContext`.
 * @param signal      Optional `AbortController.signal` for request cancellation (checklist).
 */
export async function getDocuments(
  accessToken: string,
  signal?:     AbortSignal,
): Promise<DocumentStatusDto[]> {
  const res = await fetch(`${BASE}`, {
    headers: { Authorization: `Bearer ${accessToken}` },
    signal,
  })
  if (res.ok) return res.json() as Promise<DocumentStatusDto[]>
  throw new DocumentApiError(res.status, `Failed to load documents (HTTP ${res.status})`)
}

/**
 * `GET /api/documents/{id}/status` — poll the processing status of a single document (AC-001).
 * Returns 404 for both non-existent and unauthorised documents (OWASP A01 — no status code leakage).
 *
 * @param id          Document UUID.
 * @param accessToken JWT Bearer token from `AuthContext`.
 * @param signal      `AbortController.signal` — must be passed to abort stale in-flight polls on
 *                    component unmount or poll teardown (checklist — stale closure protection).
 */
export async function getDocumentStatus(
  id:          string,
  accessToken: string,
  signal?:     AbortSignal,
): Promise<DocumentStatusDto> {
  const res = await fetch(`${BASE}/${encodeURIComponent(id)}/status`, {
    headers: { Authorization: `Bearer ${accessToken}` },
    signal,
  })
  if (res.ok) return res.json() as Promise<DocumentStatusDto>
  throw new DocumentApiError(res.status, `Status check failed (HTTP ${res.status})`)
}

/**
 * `POST /api/documents/{id}/retry` — reset a `TimedOut` or `ExtractionFailed` document
 * and restart the pipeline (AC-004, AC-005).
 *
 * Callers must handle:
 * - `DocumentApiError(409)` → "Document processing is already complete." (Edge)
 * - `DocumentApiError(404)` → document not found or not owned
 * - Any `Error` thrown → network / connectivity issue
 *
 * @param id          Document UUID.
 * @param accessToken JWT Bearer token from `AuthContext`.
 */
export async function retryDocument(
  id:          string,
  accessToken: string,
): Promise<void> {
  const res = await fetch(`${BASE}/${encodeURIComponent(id)}/retry`, {
    method:  'POST',
    headers: { Authorization: `Bearer ${accessToken}` },
  })
  if (res.ok) return

  let serverMessage: string
  try {
    const body = await res.json() as { error?: string; message?: string }
    serverMessage = body.error ?? body.message ?? `Retry failed with HTTP ${res.status}`
  } catch {
    serverMessage = `Retry failed with HTTP ${res.status}`
  }

  throw new DocumentApiError(res.status, serverMessage)
}

