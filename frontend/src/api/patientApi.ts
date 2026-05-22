/**
 * API wrapper for patient search and patient summary endpoints.
 * us_040 — task_002 (SCR-013, SCR-014)
 *
 * Both functions accept an optional `signal` for AbortController-based cancellation;
 * this allows the debounced search to abort in-flight requests on each new keystroke (AC-001).
 * No global mutable state — accessToken is passed per call (OWASP A01).
 */

import type { PatientSearchResultDto, PatientSummaryDto } from '../types/patient'

const API_BASE = '/api'

/**
 * `GET /api/patients/search?q=<term>` — search patients by name/code.
 * Callers must enforce the 3-character minimum before calling (AC-001; OWASP A03).
 */
export async function searchPatients(
  accessToken: string,
  q: string,
  signal?: AbortSignal,
): Promise<PatientSearchResultDto[]> {
  const res = await fetch(`${API_BASE}/patients/search?q=${encodeURIComponent(q)}`, {
    headers: { Authorization: `Bearer ${accessToken}` },
    signal,
  })

  if (!res.ok) {
    const err = await res.json().catch(() => ({ error: `HTTP ${res.status}` })) as { error?: string }
    throw new Error(err.error ?? `HTTP ${res.status}`)
  }

  return res.json() as Promise<PatientSearchResultDto[]>
}

/**
 * `GET /api/patients/{id}/summary` — full 360° patient summary.
 * `page` / `pageSize` control document pagination (Edge: 100+ documents; AC-002).
 */
export async function getPatientSummary(
  accessToken: string,
  id: string,
  page = 1,
  pageSize = 20,
): Promise<PatientSummaryDto> {
  const url = `${API_BASE}/patients/${encodeURIComponent(id)}/summary?page=${page}&pageSize=${pageSize}`

  const res = await fetch(url, {
    headers: { Authorization: `Bearer ${accessToken}` },
  })

  if (!res.ok) {
    const err = await res.json().catch(() => ({ error: `HTTP ${res.status}` })) as { error?: string }
    throw new Error(err.error ?? `HTTP ${res.status}`)
  }

  return res.json() as Promise<PatientSummaryDto>
}
