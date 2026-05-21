const API_BASE = '/api'

// ── Types ─────────────────────────────────────────────────────────────────────────────────────────

/**
 * Aggregate booking metrics for a single calendar day — mirrors `AdminMetricsDto` (us_034/AC-001).
 * No PHI fields are present — all values are aggregate integers or a nullable number (OWASP A02).
 */
export interface AdminMetrics {
  totalBookings:      number
  confirmed:          number
  cancelled:          number
  walkIns:            number
  /** Minutes from slot start to check-in arrival; null when no patients have checked in. */
  averageWaitMinutes: number | null
  highRisk:           number
  mediumRisk:         number
  lowRisk:            number
}

/** Thrown by {@link getAdminMetrics} on any non-2xx response. */
export class AdminApiError extends Error {
  constructor(public readonly status: number, message: string) {
    super(message)
    this.name = 'AdminApiError'
    Object.setPrototypeOf(this, new.target.prototype)
  }
}

// ── API wrapper ───────────────────────────────────────────────────────────────────────────────────

/**
 * `GET /api/admin/metrics?date=today` — returns aggregate booking KPIs for the current day.
 *
 * @throws {AdminApiError} status 403 when caller is not Admin role.
 * @throws {AdminApiError} status 503 when the metrics aggregation query exceeded the 5-second timeout.
 * @throws {AdminApiError} on any other non-2xx response.
 */
export async function getAdminMetrics(accessToken: string): Promise<AdminMetrics> {
  const res = await fetch(`${API_BASE}/admin/metrics?date=today`, {
    headers: { Authorization: `Bearer ${accessToken}` },
  })

  if (!res.ok) {
    let message = `Unexpected error (${res.status})`
    try {
      const err = (await res.json()) as { error?: string; title?: string }
      message = err.error ?? err.title ?? message
    } catch {
      // ignore JSON parse failure — keep default message
    }
    throw new AdminApiError(res.status, message)
  }

  return res.json() as Promise<AdminMetrics>
}
