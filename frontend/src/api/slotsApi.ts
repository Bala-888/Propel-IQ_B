const API_BASE = '/api'

// ── Types ─────────────────────────────────────────────────────────────────────────────────────────

/**
 * Individual appointment slot — schedule data only; no patient identifiers
 * (us_019; AC-001; OWASP A02; HIPAA minimum-necessary).
 * `id` is the slot's integer primary key serialised as a number by ASP.NET Core JSON.
 */
export interface SlotDto {
  id:              number
  date:            string  // ISO date string "yyyy-MM-dd" derived from SlotStart
  startTime:       string  // "HH:mm:ss" time string derived from SlotStart
  durationMinutes: number
  status:          string  // "Available" | "Booked"
  providerName:    string | null
}

/** Pagination metadata returned alongside every GET /slots response (AC-002). */
export interface PaginationMeta {
  total:      number
  page:       number
  pageSize:   number
  totalPages: number
}

/**
 * Top-level response envelope from `GET /api/slots` (AC-001; AC-002).
 * `slots` is always an array — empty when no rows match (never null).
 */
export interface SlotsResponse {
  slots:      SlotDto[]
  pagination: PaginationMeta
}

// ── API wrapper ───────────────────────────────────────────────────────────────────────────────────

/**
 * `GET /api/slots?available=true&page=N&pageSize=N` — fetches a paginated page of available
 * appointment slots for SCR-006 (us_019; AC-001; AC-002).
 *
 * `available=true` is always included so Booked and Blocked slots are never fetched
 * by the calendar component (AC-001; minimum data fetch).
 *
 * PHI: no patient identifiers are present in the response.
 *
 * @throws {Error} on HTTP 400 (invalid pagination params), 401 (unauthenticated), or unexpected 5xx.
 */
export async function getSlots(
  accessToken: string,
  { page, pageSize, date }: { page: number; pageSize: number; date?: string },
): Promise<SlotsResponse> {
  let url = `${API_BASE}/slots?available=true&page=${encodeURIComponent(page)}&pageSize=${encodeURIComponent(pageSize)}`
  if (date) url += `&date=${encodeURIComponent(date)}`

  const res = await fetch(url, {
    method:  'GET',
    headers: { Authorization: `Bearer ${accessToken}` },
  })

  if (res.status === 401) throw new Error('Authentication required.')
  if (res.status === 400) {
    let message = 'Invalid pagination parameters.'
    try {
      const err = (await res.json()) as { title?: string }
      message = err.title ?? message
    } catch { /* ignore */ }
    throw new Error(message)
  }

  if (!res.ok) throw new Error(`Failed to load slots (${res.status})`)

  return res.json() as Promise<SlotsResponse>
}
