/**
 * codeSuggestionsApi — `GET /patients/{id}/code-suggestions` (us_043; AC-001, AC-005).
 *
 * Raw fetch wrapper following the project's API module pattern (bookingsApi.ts, conflictsApi.ts).
 * OWASP A02: no PHI (chunk texts, entity values) is logged anywhere in this module.
 */

const API_BASE = '/api'

// ── Types ──────────────────────────────────────────────────────────────────────────────────────────

export interface SupportingChunkDto {
  chunkId:       string
  chunkText:     string
  sourceFilename: string
}

export interface CodeSuggestionDto {
  id:               string   // UUID from code_suggestions row (us_044; AC-001)
  reviewStatus:     string   // 'Pending' | 'Accepted' | 'Rejected' | 'Corrected' (us_044; AC-001)
  codeType:         string   // "ICD10" | "CPT"
  code:             string
  description:      string
  confidence:       number
  lowConfidence:    boolean
  supportingChunks: SupportingChunkDto[]
}

export interface CodeSuggestionsResponseDto {
  suggestions: CodeSuggestionDto[]
  message:     string | null
}

// ── API wrapper ────────────────────────────────────────────────────────────────────────────────────

/**
 * `GET /api/patients/{id}/code-suggestions` — fetches AI-generated ICD-10/CPT code suggestions
 * for the specified patient via the synchronous RAG pipeline (us_043; AIR-006).
 *
 * Resolves with {@link CodeSuggestionsResponseDto} on HTTP 200 (including the empty/message case).
 * Throws an `Error` with `.status` set to the HTTP status code on non-2xx:
 *   - 403 — caller is not Clinician or Admin; frontend redirects to /403 (AC-005).
 *   - 5xx / network error — caller renders the error state.
 *
 * @param accessToken JWT Bearer token from AuthContext.
 * @param patientId   Patient integer primary key (as string or number).
 * @param signal      AbortController signal — cancels the in-flight request on unmount.
 *                    The 10-second Ollama RAG pipeline means stale updates are common on
 *                    rapid navigation (checklist item 1; AC-001 10-second SLA path).
 */
export async function getCodeSuggestions(
  accessToken: string,
  patientId:   string | number,
  signal?:     AbortSignal,
): Promise<CodeSuggestionsResponseDto> {
  const res = await fetch(`${API_BASE}/patients/${patientId}/code-suggestions`, {
    headers: { 'Authorization': `Bearer ${accessToken}` },
    signal,
  })

  if (res.ok) {
    return res.json() as Promise<CodeSuggestionsResponseDto>
  }

  // Non-2xx: attach status for caller-side branching (403 → redirect, 5xx → error state)
  throw Object.assign(new Error(`Request failed (HTTP ${res.status})`), { status: res.status })
}
