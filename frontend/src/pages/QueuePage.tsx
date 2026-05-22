import { useEffect, useMemo, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../context/AuthContext'
import { getQueue, markArrived, type QueueEntry } from '../api/queueApi'
import { RiskBadge } from '../components/queue/RiskBadge'
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'

// ── Icon components (aria-hidden — text labels are the primary signal; UXR-105) ─────────────────────────

function ClockIcon() {
  return (
    <svg aria-hidden="true" width="13" height="13" viewBox="0 0 20 20" fill="none">
      <circle cx="10" cy="10" r="8" stroke="currentColor" strokeWidth="1.5" />
      <path d="M10 6v4.5l3 2" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  )
}

function CheckedIcon() {
  return (
    <svg aria-hidden="true" width="13" height="13" viewBox="0 0 20 20" fill="none">
      <circle cx="10" cy="10" r="8" stroke="currentColor" strokeWidth="1.5" />
      <polyline points="6.5,10 9,12.5 13.5,7.5" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  )
}

function SpinnerIcon() {
  return (
    <svg aria-hidden="true" width="13" height="13" viewBox="0 0 20 20" fill="none" style={{ animation: 'spin 0.8s linear infinite' }}>
      <circle cx="10" cy="10" r="7" stroke="currentColor" strokeWidth="2" strokeDasharray="22" strokeDashoffset="8" strokeLinecap="round" />
    </svg>
  )
}

// ── Sort types ────────────────────────────────────────────────────────────────────────────────────

type SortColumn = 'position' | 'waitTime' | 'riskTier'

interface SortState {
  column:    SortColumn
  direction: 'asc' | 'desc'
}

// ── Constants ─────────────────────────────────────────────────────────────────────────────────────

/** Numeric ordering for Risk Tier descending sort: High (3) → Medium (2) → Low (1) → Unknown (0). */
const RISK_ORDER: Record<string, number> = { High: 3, Medium: 2, Low: 1, Unknown: 0 }

// ── Helpers ───────────────────────────────────────────────────────────────────────────────────────

function todayIso(): string {
  return new Date().toISOString().slice(0, 10)   // "yyyy-MM-dd"
}

function formatTime(iso: string | null): string {
  if (!iso) return '—'
  return new Date(iso).toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit' })
}

function patientInitials(name: string): string {
  return name.split(' ').map(w => w[0] ?? '').join('').slice(0, 2).toUpperCase()
}

// ── Styles (exact wireframe tokens — SCR-011) ─────────────────────────────────────────────────────

const styles = {
  page:          { fontFamily: "'IBM Plex Sans', system-ui, -apple-system, sans-serif", background: '#F8FAFC', minHeight: '100vh', padding: '32px' },
  heading:       { fontSize: '22px', fontWeight: 700, color: '#0F172A', marginBottom: '4px' },
  subheading:    { fontSize: '14px', color: '#475569', marginBottom: '24px' },
  summaryBar:    { marginBottom: '20px', padding: '12px 16px', background: '#FFFFFF', border: '1px solid #E2E8F0', borderRadius: '8px', fontSize: '14px', color: '#475569' },
  tableWrapper:  { background: '#FFFFFF', border: '1px solid #E2E8F0', borderRadius: '8px', overflow: 'hidden', boxShadow: '0 1px 3px rgba(15,23,42,0.08)' },
  table:         { width: '100%', borderCollapse: 'collapse' as const },
  thead:         { background: '#F1F5F9' },
  th:            { padding: '12px 16px', fontSize: '12px', fontWeight: 600, color: '#475569', textAlign: 'left' as const, letterSpacing: '0.3px', textTransform: 'uppercase' as const, borderBottom: '1px solid #E2E8F0' },
  thSortable:    { cursor: 'pointer', userSelect: 'none' as const },
  td:            { padding: '16px', fontSize: '14px', borderBottom: '1px solid #E2E8F0', verticalAlign: 'middle' as const, color: '#0F172A' },
  patientCell:   { display: 'flex', alignItems: 'center', gap: '12px' },
  initials:      { width: '32px', height: '32px', borderRadius: '50%', background: '#EBF3FE', color: '#1A56DB', fontSize: '12px', fontWeight: 700, display: 'flex', alignItems: 'center', justifyContent: 'center', flexShrink: 0 },
  badgeDefault:  { display: 'inline-flex', alignItems: 'center', fontSize: '12px', fontWeight: 500, padding: '2px 10px', borderRadius: '9999px', background: '#EBF3FE', color: '#1A56DB' },
  badgeArrived:  { display: 'inline-flex', alignItems: 'center', fontSize: '12px', fontWeight: 500, padding: '2px 10px', borderRadius: '9999px', background: '#DCFCE7', color: '#16A34A' },
  emptyCell:     { padding: '24px 16px', textAlign: 'center' as const, color: '#64748B', fontSize: '14px' },
  errorBox:      { padding: '16px', color: '#DC2626', fontSize: '14px', background: '#FEF2F2', borderRadius: '8px', marginBottom: '16px' },
  loadingText:   { color: '#475569', fontSize: '14px' },
  statusCell:    { display: 'inline-flex', alignItems: 'center', gap: '5px', fontSize: '13px', fontWeight: 500 },
  markBtn:       { display: 'inline-flex', alignItems: 'center', gap: '5px', fontSize: '12px', fontWeight: 600, padding: '5px 10px', borderRadius: '4px', border: '1px solid #E2E8F0', background: '#FFFFFF', color: '#0F172A', cursor: 'pointer', minHeight: '32px', minWidth: '44px' },
  markBtnDisabled: { opacity: 0.6, cursor: 'not-allowed' as const },
  toast:         { position: 'fixed' as const, bottom: '24px', left: '50%', transform: 'translateX(-50%)', background: '#0F172A', color: '#FFFFFF', fontSize: '14px', padding: '12px 20px', borderRadius: '8px', zIndex: 1000, boxShadow: '0 4px 12px rgba(0,0,0,0.15)' },
} as const

// ── Component ─────────────────────────────────────────────────────────────────────────────────────

/**
 * QueuePage (SCR-011) — same-day patient queue dashboard for Staff and Admin (us_031; AC-001–005).
 *
 * Role guard: Patient-role users are immediately redirected to /intake (OWASP A01; AC-001).
 * Queue data is fetched from GET /api/queue on mount. A 30-second setInterval increments a tick
 * counter which triggers re-renders to update the live Wait Time column values without re-fetching
 * (AC-003). Table columns are sorted client-side via useMemo (AC-004).
 */
export function QueuePage() {
  const { accessToken, role } = useAuth()
  const navigate = useNavigate()

  // ── Role guard (OWASP A01; AC-001) ───────────────────────────────────────────────────────────
  useEffect(() => {
    if (role === 'Patient') navigate('/intake', { replace: true })
  }, [role, navigate])

  // ── Queue data ────────────────────────────────────────────────────────────────────────────────
  const [queue,   setQueue]   = useState<QueueEntry[]>([])
  const [loading, setLoading] = useState(true)
  const [error,   setError]   = useState<string | null>(null)

  useEffect(() => {
    if (!accessToken) return
    let cancelled = false
    async function fetchQueue() {
      setLoading(true)
      setError(null)
      try {
        const data = await getQueue(accessToken!, todayIso())
        if (!cancelled) setQueue(data)
      } catch (err) {
        if (!cancelled) setError((err as Error).message ?? 'Failed to load queue')
      } finally {
        if (!cancelled) setLoading(false)
      }
    }
    void fetchQueue()
    return () => { cancelled = true }
  }, [accessToken])

  // ── Live wait time tick — 30-second interval increments counter; re-render refreshes Date.now()
  //    in each row's Wait Time cell without re-fetching queue data (AC-003) ─────────────────────
  const [, setTick] = useState(0)
  useEffect(() => {
    const id = setInterval(() => setTick(t => t + 1), 30_000)
    return () => clearInterval(id)
  }, [])
  // ── Per-row loading state + toast (us_032; AC-001) ───────────────────────────────────────────
  const [loadingRows, setLoadingRows] = useState<ReadonlySet<number>>(new Set<number>())
  const [toastMsg,    setToastMsg]    = useState<string | null>(null)

  // ── SignalR — real-time queue push (us_033/AC-001–004) ────────────────────────────────────────
  // `newRowId`: the id of the most recently pushed entry; drives the .queue-row--new CSS class (AC-003; UXR-502).
  // `lastEventTimestamp`: updated on every received event; used as `since` cursor on reconnection (AC-004).
  const [newRowId, setNewRowId] = useState<number | null>(null)
  const lastEventTimestamp = useRef<string>(new Date().toISOString())

  useEffect(() => {
    if (!accessToken) return

    const connection = new HubConnectionBuilder()
      .withUrl('/hubs/queue', {
        // Token from in-memory context — never from localStorage (OWASP A01)
        accessTokenFactory: () => accessToken,
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build()

    // AC-001: new entry pushed by server → prepend to queue + trigger highlight animation
    connection.on('QueueEntryAdded', (entry: QueueEntry) => {
      setQueue(q => [entry, ...q])
      setNewRowId(entry.id)
      lastEventTimestamp.current = new Date().toISOString()
      // Clear highlight class after transition completes (AC-003; UXR-502 — 1.5s transition + 0.1s buffer)
      setTimeout(() => setNewRowId(null), 1_600)
    })

    // AC-002: status change pushed by server → update matching row in-place; preserve position (F014)
    connection.on('QueueEntryUpdated', (entry: QueueEntry) => {
      setQueue(q => q.map(r =>
        r.id === entry.id
          ? { ...r, status: entry.status, arrivalTime: entry.arrivalTime }
          : r
      ))
      lastEventTimestamp.current = new Date().toISOString()
    })

    // AC-004: on reconnection, fetch missed events using the last-seen timestamp as cursor
    connection.onreconnected(async () => {
      try {
        const missed = await getQueue(accessToken, todayIso(), { since: lastEventTimestamp.current })
        setQueue(existing => {
          // Reconcile: update matching rows in-place, append genuinely new ones — no duplicates.
          // Map keyed by id gives O(1) lookup per incoming entry (AC-004).
          const idMap = new Map(existing.map(e => [e.id, e]))
          for (const m of missed) {
            idMap.set(m.id, idMap.has(m.id) ? { ...idMap.get(m.id)!, ...m } : m)
          }
          return Array.from(idMap.values())
        })
      } catch {
        // Reconnection fallback failure — queue may be slightly stale; next interval fetch will catch up
      }
    })

    void connection.start().catch(() => {
      // SignalR connection failure is non-fatal — queue still loads via the REST poll on mount
    })

    return () => {
      // connection.off() removes all handlers before stop() to prevent duplicate
      // registrations in React strict mode (AC-001 — no memory leak).
      connection.off('QueueEntryAdded')
      connection.off('QueueEntryUpdated')
      void connection.stop()
    }
  }, [accessToken])

  async function handleMarkArrived(entry: QueueEntry) {
    setLoadingRows(prev => new Set([...prev, entry.id]))
    try {
      const response = await markArrived(accessToken!, entry.id)
      // Immutable update — only the affected row reference changes (AC-002; checklist)
      setQueue(q => q.map(r => r.id === entry.id
        ? { ...r, status: response.status, arrivalTime: response.arrivedAt }
        : r
      ))
    } catch {
      // queue state NOT mutated on failure — row stays "Confirmed", button reappears (Edge: network failure)
      setToastMsg('Could not mark as arrived. Please try again.')
      setTimeout(() => setToastMsg(null), 4_000)
    } finally {
      setLoadingRows(prev => { const s = new Set([...prev]); s.delete(entry.id); return s })
    }
  }
  // ── Sort state ────────────────────────────────────────────────────────────────────────────────
  const [sortState, setSortState] = useState<SortState>({ column: 'position', direction: 'asc' })

  function handleSort(column: SortColumn) {
    setSortState(prev =>
      prev.column === column
        ? { column, direction: prev.direction === 'asc' ? 'desc' : 'asc' }
        : { column, direction: 'asc' }
    )
  }

  function sortArrow(col: SortColumn): string {
    if (sortState.column !== col) return ''
    return sortState.direction === 'asc' ? ' ▲' : ' ▼'
  }

  function ariaSortAttr(col: SortColumn): 'ascending' | 'descending' | undefined {
    if (sortState.column !== col) return undefined
    return sortState.direction === 'asc' ? 'ascending' : 'descending'
  }

  // ── Summary bar — derived from unsorted queue; unaffected by sort order (AC-005) ─────────────
  const summary = useMemo(() => ({
    total:       queue.length,
    highCount:   queue.filter(e => e.noShowRiskTier === 'High').length,
    mediumCount: queue.filter(e => e.noShowRiskTier === 'Medium').length,
    lowCount:    queue.filter(e => e.noShowRiskTier === 'Low').length,
  }), [queue])

  // ── Sorted queue — Risk Tier uses numeric map for deterministic ordering (AC-004) ─────────────
  const sortedQueue = useMemo(() => {
    const now = Date.now()
    return [...queue].sort((a, b) => {
      let cmp: number
      if (sortState.column === 'position') {
        cmp = a.position - b.position
      } else if (sortState.column === 'waitTime') {
        const wa = a.arrivalTime ? now - new Date(a.arrivalTime).getTime() : -Infinity
        const wb = b.arrivalTime ? now - new Date(b.arrivalTime).getTime() : -Infinity
        cmp = wa - wb
      } else {
        // riskTier — numeric map: High=3, Medium=2, Low=1, Unknown=0
        cmp = (RISK_ORDER[a.noShowRiskTier] ?? 0) - (RISK_ORDER[b.noShowRiskTier] ?? 0)
      }
      return sortState.direction === 'asc' ? cmp : -cmp
    })
  }, [queue, sortState])

  // ── Wait time display — Date.now() is called at render time; setTick drives re-renders (AC-003)
  function waitTimeDisplay(arrivalTime: string | null): string {
    if (!arrivalTime) return '—'
    const mins = Math.floor((Date.now() - new Date(arrivalTime).getTime()) / 60_000)
    return `${mins} min`
  }

  // Block render until role redirect completes — prevent flash of staff content for Patient role
  if (role === 'Patient') return null

  return (
    <div style={styles.page}>
      <h1 style={styles.heading}>Today's queue</h1>
      <p style={styles.subheading}>
        {new Date().toLocaleDateString('en-US', { day: 'numeric', month: 'long', year: 'numeric' })}
      </p>

      {/* AC-005 — Summary bar; role="status" so screen readers announce count changes on update */}
      <div role="status" aria-live="polite" style={styles.summaryBar}>
        {`${summary.total} Waiting | ${summary.highCount} High Risk | ${summary.mediumCount} Medium Risk | ${summary.lowCount} Low Risk`}
      </div>

      {error && (
        <div role="alert" style={styles.errorBox}>{error}</div>
      )}

      {loading ? (
        <p style={styles.loadingText}>Loading queue…</p>
      ) : (
        <div style={styles.tableWrapper} role="region" aria-label="Patient queue table">
          <table style={styles.table}>
            <thead style={styles.thead}>
              <tr>
                <th
                  scope="col"
                  style={{ ...styles.th, ...styles.thSortable }}
                  onClick={() => handleSort('position')}
                  aria-sort={ariaSortAttr('position')}
                >
                  {'Position' + sortArrow('position')}
                </th>
                <th scope="col" style={styles.th}>Patient Name</th>
                <th scope="col" style={styles.th}>Arrival Time</th>
                <th scope="col" style={styles.th}>Appointment Time</th>
                <th
                  scope="col"
                  style={{ ...styles.th, ...styles.thSortable }}
                  onClick={() => handleSort('waitTime')}
                  aria-sort={ariaSortAttr('waitTime')}
                >
                  {'Wait Time' + sortArrow('waitTime')}
                </th>
                <th
                  scope="col"
                  style={{ ...styles.th, ...styles.thSortable }}
                  onClick={() => handleSort('riskTier')}
                  aria-sort={ariaSortAttr('riskTier')}
                >
                  {'Risk Tier' + sortArrow('riskTier')}
                </th>
                <th scope="col" style={styles.th}>Status</th>
                <th scope="col" style={styles.th}>Actions</th>
              </tr>
            </thead>
            <tbody>
              {sortedQueue.length === 0 ? (
                // Edge: empty queue — single row with colSpan; <table>/<thead> still rendered for AT context
                <tr>
                  <td colSpan={8} style={styles.emptyCell}>No patients in queue for today.</td>
                </tr>
              ) : (
                sortedQueue.map(entry => (
                  <tr
                    key={entry.position}
                    className={entry.id === newRowId ? 'queue-row--new' : undefined}
                  >
                    <td style={styles.td}>{entry.position}</td>
                    <td style={styles.td}>
                      <div style={styles.patientCell}>
                        <div style={styles.initials} aria-hidden="true">
                          {patientInitials(entry.patientName)}
                        </div>
                        <span>{entry.patientName}</span>
                      </div>
                    </td>
                    <td style={styles.td}>{formatTime(entry.arrivalTime)}</td>
                    <td style={styles.td}>{formatTime(entry.appointmentTime)}</td>
                    {/* Wait time uses Date.now() — refreshed on every re-render triggered by setTick */}
                    <td style={styles.td}>{waitTimeDisplay(entry.arrivalTime)}</td>
                    <td style={styles.td}>
                      {/* RiskBadge renders icon + text for all tiers inc. Unknown (AC-002; UXR-403; UXR-105) */}
                      <RiskBadge tier={entry.noShowRiskTier} />
                    </td>
                    {/* Status cell: icon + text for both states — colour is supplementary (UXR-105; WCAG 2.1 SC 1.4.1) */}
                    <td style={styles.td}>
                      {entry.status === 'CheckedIn' ? (
                        <span style={{ ...styles.statusCell, color: '#16A34A' }}>
                          <CheckedIcon />
                          <span>Checked In</span>
                        </span>
                      ) : (
                        <span style={{ ...styles.statusCell, color: '#64748B' }}>
                          <ClockIcon />
                          <span>{entry.status}</span>
                        </span>
                      )}
                    </td>
                    {/* Actions column — Mark Arrived button absent from DOM when status !== "Confirmed" (AC-001; UXR-106 ≤2 clicks) */}
                    <td style={styles.td}>
                      {entry.status === 'Confirmed' && (
                        <button
                          style={{
                            ...styles.markBtn,
                            ...(loadingRows.has(entry.id) ? styles.markBtnDisabled : {}),
                          }}
                          onClick={() => void handleMarkArrived(entry)}
                          disabled={loadingRows.has(entry.id)}
                          aria-label="Mark arrived"
                          aria-busy={loadingRows.has(entry.id)}
                        >
                          {loadingRows.has(entry.id) ? <SpinnerIcon /> : null}
                          Mark arrived
                        </button>
                      )}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      )}

      {/* Toast notification for PATCH failure (Edge: network failure; AC-001 — button reappears naturally) */}
      {toastMsg && (
        <div role="alert" aria-live="assertive" style={styles.toast}>
          {toastMsg}
        </div>
      )}
    </div>
  )
}
