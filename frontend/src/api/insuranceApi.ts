const API_BASE = '/api'

// ── Types ─────────────────────────────────────────────────────────────────────────────────────────

/**
 * The three possible outcomes of `GET /api/insurance/pre-check` (us_023; AC-001).
 * These exact string literals are required by the backend contract —
 * the `InsuranceAlertBanner` component depends on them (AC-002; Edge: Incomplete vs Missing).
 */
export type InsuranceStatus = 'Complete' | 'Incomplete' | 'Missing'

// ── API wrapper ───────────────────────────────────────────────────────────────────────────────────

/**
 * `GET /api/insurance/pre-check` — returns insurance completeness for the authenticated patient.
 *
 * The backend sources `patientId` from the JWT sub claim; no `patientId` query parameter is
 * accepted by the endpoint (OWASP A01; A07 — cross-patient enumeration prevention).
 *
 * Returns `null` on any non-ok HTTP response or network error — the caller treats `null` as
 * the silent skip path: the dialog opens without an insurance alert (Edge: API unavailable; AC-002).
 *
 * @param accessToken JWT Bearer token from AuthContext.
 */
export async function getInsurancePreCheck(accessToken: string): Promise<InsuranceStatus | null> {
  try {
    const res = await fetch(`${API_BASE}/insurance/pre-check`, {
      method:  'GET',
      headers: {
        'Authorization': `Bearer ${accessToken}`,
      },
    })

    if (!res.ok) return null

    const body = await res.json() as { status?: string }

    // Narrow at the API boundary — only accept the three known literals (type safety; AC-001)
    const status = body.status
    if (status === 'Complete' || status === 'Incomplete' || status === 'Missing') {
      return status
    }
    return null
  } catch {
    // Network error or JSON parse failure — treat as silent skip (Edge: API unavailable)
    return null
  }
}
