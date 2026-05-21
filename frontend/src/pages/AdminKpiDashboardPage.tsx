import { useEffect, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useAuth } from '../context/AuthContext'
import { getAdminMetrics, AdminApiError, type AdminMetrics } from '../api/adminApi'

// ── Icon components (aria-hidden — text labels are the primary signal; UXR-105) ─────────────────

function WarningIcon() {
  return (
    <svg aria-hidden="true" width="16" height="16" viewBox="0 0 20 20" fill="none">
      <path d="M9.02 3.27L1.76 15.5A1.14 1.14 0 002.74 17h14.52a1.14 1.14 0 00.98-1.5L10.98 3.27a1.14 1.14 0 00-1.96 0z" stroke="currentColor" strokeWidth="1.5" strokeLinejoin="round" />
      <path d="M10 8v4M10 14.5v.5" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
    </svg>
  )
}

function CautionIcon() {
  return (
    <svg aria-hidden="true" width="16" height="16" viewBox="0 0 20 20" fill="none">
      <circle cx="10" cy="10" r="8" stroke="currentColor" strokeWidth="1.5" />
      <path d="M10 6.5v4M10 13v.5" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
    </svg>
  )
}

function CheckIcon() {
  return (
    <svg aria-hidden="true" width="16" height="16" viewBox="0 0 20 20" fill="none">
      <circle cx="10" cy="10" r="8" stroke="currentColor" strokeWidth="1.5" />
      <polyline points="6.5,10 9,12.5 13.5,7.5" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  )
}

// ── Styles (SCR-016 wireframe tokens) ─────────────────────────────────────────────────────────────

const styles = {
  page:        { fontFamily: "'IBM Plex Sans', system-ui, -apple-system, sans-serif", background: '#F8FAFC', minHeight: '100vh', padding: '40px 40px 40px 40px' },
  heading:     { fontSize: '28px', fontWeight: 700, color: '#0F172A', marginBottom: '4px' },
  subheading:  { fontSize: '14px', color: '#475569', marginBottom: '32px' },
  headerRow:   { display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '32px' },
  usersLink:   { display: 'inline-flex', alignItems: 'center', gap: '6px', fontSize: '14px', fontWeight: 600, padding: '9px 16px', borderRadius: '6px', background: '#1A56DB', color: '#FFFFFF', textDecoration: 'none', minHeight: '40px' },
  grid:        { display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '20px', marginBottom: '40px' },
  card:        { background: '#FFFFFF', border: '1px solid #E2E8F0', borderRadius: '8px', padding: '24px', boxShadow: '0 1px 3px rgba(15,23,42,0.08)' },
  cardLabel:   { fontSize: '13px', fontWeight: 500, color: '#475569', marginBottom: '12px', textTransform: 'uppercase' as const, letterSpacing: '0.3px' },
  cardValue:   { fontSize: '36px', fontWeight: 700, lineHeight: 1, fontFamily: "'IBM Plex Mono', 'Courier New', monospace" },
  riskSection: { background: '#FFFFFF', border: '1px solid #E2E8F0', borderRadius: '8px', padding: '24px', boxShadow: '0 1px 3px rgba(15,23,42,0.08)' },
  riskTitle:   { fontSize: '16px', fontWeight: 600, color: '#0F172A', marginBottom: '16px' },
  riskRow:     { display: 'flex', gap: '24px', flexWrap: 'wrap' as const },
  riskBadge:   { display: 'inline-flex', alignItems: 'center', gap: '6px', fontSize: '14px', fontWeight: 500 },
  unavailable: { padding: '12px 16px', background: '#FFFBEB', border: '1px solid #FDE68A', borderRadius: '6px', fontSize: '14px', color: '#92400E', marginBottom: '24px' },
  loadingText: { color: '#475569', fontSize: '14px' },
} as const

// ── MetricCard ────────────────────────────────────────────────────────────────────────────────────

interface MetricCardProps {
  label: string
  value: string
  valueColor?: string
}

function MetricCard({ label, value, valueColor }: MetricCardProps) {
  return (
    <div style={styles.card} role="listitem" aria-label={`${label}: ${value}`}>
      <div style={styles.cardLabel}>{label}</div>
      <div style={{ ...styles.cardValue, color: valueColor ?? '#0F172A' }}>{value}</div>
    </div>
  )
}

// ── Component ─────────────────────────────────────────────────────────────────────────────────────

/**
 * AdminKpiDashboardPage (SCR-016) — Admin-only KPI metrics dashboard (us_034/AC-001–003).
 *
 * Fetches `GET /api/admin/metrics?date=today` on mount and every 60 seconds (AC-002).
 * Renders six metric cards plus a no-show risk distribution section.
 * On 503 response shows an unavailability banner without crashing; auto-refresh continues (Edge).
 */
export function AdminKpiDashboardPage() {
  const { accessToken, role } = useAuth()
  const navigate = useNavigate()

  // Role guard: non-Admin users are redirected before any API call is attempted (AC-004; OWASP A01)
  useEffect(() => {
    if (role && role !== 'Admin') navigate('/login', { replace: true })
  }, [role, navigate])

  const [metrics,     setMetrics]     = useState<AdminMetrics | null>(null)
  const [loading,     setLoading]     = useState(true)
  const [unavailable, setUnavailable] = useState(false)

  // fetchMetrics is called on mount and every 60 seconds (AC-002; OWASP A01 — token from context)
  async function fetchMetrics() {
    if (!accessToken) return
    try {
      const data = await getAdminMetrics(accessToken)
      setMetrics(data)
      setUnavailable(false)
    } catch (err) {
      if (err instanceof AdminApiError && err.status === 503) {
        // Edge: query timeout — show banner; keep last known metrics; interval continues
        setUnavailable(true)
      }
      // Other errors (403, network) — leave metrics as-is; do not crash
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    // Guard: only fetch when authenticated as Admin (AC-004; OWASP A01 — prevents 403 for Staff/Patient)
    if (!accessToken || role !== 'Admin') return
    void fetchMetrics()
    const id = setInterval(() => { void fetchMetrics() }, 60_000)
    return () => clearInterval(id)
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [accessToken, role])

  // Non-admin users — prevent flash of content while redirect is pending (OWASP A01)
  if (role && role !== 'Admin') return null

  const fmt = (n: number) => n.toString()
  const fmtWait = (v: number | null | undefined) =>
    v != null ? `${Math.round(v)} min` : '—'

  return (
    <div style={styles.page}>
      {/* Header row */}
      <div style={styles.headerRow}>
        <div>
          <h1 style={styles.heading}>Platform metrics</h1>
          <p style={styles.subheading}>
            {new Date().toLocaleDateString('en-US', { day: 'numeric', month: 'long', year: 'numeric' })}
            {' · '}Auto-refreshes every 60 seconds
          </p>
        </div>
        {/* AC-003: "User Management" link to SCR-017 — same tab; React Router Link (no target) */}
        <Link to="/admin/users" style={styles.usersLink} aria-label="Go to User Management">
          User Management
        </Link>
      </div>

      {/* 503 unavailability banner — does not crash the component; auto-refresh continues (Edge) */}
      {unavailable && (
        <p role="alert" aria-live="assertive" style={styles.unavailable}>
          Data temporarily unavailable. Refreshing...
        </p>
      )}

      {loading && !metrics && (
        <p style={styles.loadingText}>Loading metrics…</p>
      )}

      {/* KPI grid — 6 metric cards (AC-001) */}
      {metrics && (
        <>
          <div style={styles.grid} role="list" aria-label="Key performance indicators" aria-live="polite">
            <MetricCard label="Total Bookings Today"  value={fmt(metrics.totalBookings)} valueColor="#1A56DB" />
            <MetricCard label="Confirmed"             value={fmt(metrics.confirmed)}     valueColor="#1A56DB" />
            <MetricCard label="Cancelled"             value={fmt(metrics.cancelled)}     valueColor={metrics.cancelled > 0 ? '#D97706' : '#0F172A'} />
            <MetricCard label="Walk-ins"              value={fmt(metrics.walkIns)}       valueColor="#1A56DB" />
            <MetricCard label="Avg Wait Time"         value={fmtWait(metrics.averageWaitMinutes)} />
            <MetricCard label="No-show Risk (Total)"  value={fmt(metrics.highRisk + metrics.mediumRisk + metrics.lowRisk)} valueColor={metrics.highRisk > 0 ? '#DC2626' : '#0F172A'} />
          </div>

          {/* No-show risk distribution — icon + text per tier (UXR-105; WCAG 2.1 SC 1.4.1) */}
          <div style={styles.riskSection}>
            <div style={styles.riskTitle}>No-show Risk Distribution</div>
            <div style={styles.riskRow} role="list" aria-label="No-show risk tier counts">
              <span role="listitem" style={{ ...styles.riskBadge, color: '#DC2626' }}>
                <WarningIcon /> High Risk: {metrics.highRisk}
              </span>
              <span role="listitem" style={{ ...styles.riskBadge, color: '#D97706' }}>
                <CautionIcon /> Medium Risk: {metrics.mediumRisk}
              </span>
              <span role="listitem" style={{ ...styles.riskBadge, color: '#16A34A' }}>
                <CheckIcon /> Low Risk: {metrics.lowRisk}
              </span>
            </div>
          </div>
        </>
      )}

      {/* Zero state: metrics loaded but all zeros — show cards with "0" (Edge: no bookings today) */}
      {!loading && metrics && metrics.totalBookings === 0 && (
        <p style={{ marginTop: '16px', fontSize: '14px', color: '#475569' }}>
          No bookings recorded for today yet.
        </p>
      )}
    </div>
  )
}
