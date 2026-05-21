import { Link, useLocation } from 'react-router-dom'
import { Header } from '../components/layout/Header'
import { CalendarSyncSection } from '../components/calendar/CalendarSyncSection'
import { useAuth } from '../context/AuthContext'
import type { SlotDto } from '../api/slotsApi'

// ── Icons ─────────────────────────────────────────────────────────────────────────────────────────

function CheckCircleIcon() {
  return (
    <svg aria-hidden="true" width="48" height="48" viewBox="0 0 24 24" fill="none"
         stroke="var(--color-status-success, #16a34a)" strokeWidth="1.5"
         strokeLinecap="round" strokeLinejoin="round">
      <path d="M22 11.08V12a10 10 0 1 1-5.93-9.14" />
      <polyline points="22 4 12 14.01 9 11.01" />
    </svg>
  )
}

function CalendarIcon() {
  return (
    <svg aria-hidden="true" width="14" height="14" viewBox="0 0 24 24" fill="none"
         stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <rect x="3" y="4" width="18" height="18" rx="2" ry="2" />
      <line x1="16" y1="2" x2="16" y2="6" />
      <line x1="8" y1="2" x2="8" y2="6" />
      <line x1="3" y1="10" x2="21" y2="10" />
    </svg>
  )
}

// ── Helpers ───────────────────────────────────────────────────────────────────────────────────────

function formatDate(iso: string): string {
  const d = new Date(`${iso}T00:00:00`)
  if (isNaN(d.getTime())) return iso
  return d.toLocaleDateString('en-US', { weekday: 'long', year: 'numeric', month: 'long', day: 'numeric' })
}

function formatTime(hms: string): string {
  const [hStr, mStr] = hms.split(':')
  const h      = parseInt(hStr ?? '0', 10)
  const m      = mStr ?? '00'
  const suffix = h >= 12 ? 'PM' : 'AM'
  const h12    = h % 12 === 0 ? 12 : h % 12
  return `${h12}:${m} ${suffix}`
}

// ── Route state shape ─────────────────────────────────────────────────────────────────────────────

interface BookingConfirmationState {
  bookingId: number
  slot:      SlotDto
}

// ── Component ─────────────────────────────────────────────────────────────────────────────────────

/**
 * Booking confirmation screen — shown after `POST /api/bookings` returns 201 (us_020; AC-001).
 * Route state carries `{bookingId, slot}` set by `SlotCalendar` on success.
 */
export function BookingConfirmationPage() {
  const location = useLocation()
  const state    = location.state as BookingConfirmationState | null
  const { accessToken } = useAuth()

  return (
    <>
      <Header />
      <main
        style={{
          display:        'flex',
          flexDirection:  'column',
          alignItems:     'center',
          justifyContent: 'center',
          minHeight:      'calc(100vh - 64px)',
          padding:        'var(--space-8)',
          textAlign:      'center',
        }}
      >
        <div
          style={{
            background:   'var(--color-bg-surface)',
            border:       '1px solid var(--color-border)',
            borderRadius: 'var(--radius-md)',
            boxShadow:    'var(--shadow-2)',
            padding:      'var(--space-10) var(--space-8)',
            maxWidth:     480,
            width:        '100%',
          }}
        >
          {/* ── Success icon ────────────────────────────────────────────────── */}
          <div
            style={{
              display:        'flex',
              justifyContent: 'center',
              marginBottom:   'var(--space-5)',
            }}
          >
            <CheckCircleIcon />
          </div>

          {/* ── Heading ─────────────────────────────────────────────────────── */}
          <h1
            style={{
              fontSize:     '20px',
              fontWeight:   700,
              color:        'var(--color-text-primary)',
              marginBottom: 'var(--space-2)',
            }}
          >
            Appointment Booked!
          </h1>
          <p
            style={{
              fontSize:     '14px',
              color:        'var(--color-text-secondary)',
              marginBottom: 'var(--space-6)',
            }}
          >
            Your appointment has been confirmed.
          </p>

          {/* ── Booking details card ─────────────────────────────────────────── */}
          {state ? (
            <div
              style={{
                background:   'var(--color-primary-subtle)',
                borderRadius: 'var(--radius-sm)',
                padding:      'var(--space-4) var(--space-5)',
                textAlign:    'left',
                marginBottom: 'var(--space-6)',
              }}
            >
              <div
                style={{
                  fontSize:     '12px',
                  fontWeight:   600,
                  color:        'var(--color-text-secondary)',
                  marginBottom: 'var(--space-3)',
                  textTransform: 'uppercase',
                  letterSpacing: '0.05em',
                }}
              >
                Booking #{state.bookingId}
              </div>

              <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-2)', marginBottom: 'var(--space-2)' }}>
                <CalendarIcon />
                <span style={{ fontSize: '15px', fontWeight: 600, color: 'var(--color-text-primary)' }}>
                  {formatDate(state.slot.date)}
                </span>
              </div>

              <div style={{ fontSize: '14px', color: 'var(--color-text-primary)', paddingLeft: '18px' }}>
                {formatTime(state.slot.startTime)} &nbsp;·&nbsp; {state.slot.durationMinutes} min
              </div>

              {state.slot.providerName && (
                <div style={{ fontSize: '13px', color: 'var(--color-text-secondary)', paddingLeft: '18px', marginTop: 'var(--space-1)' }}>
                  Provider: <strong>{state.slot.providerName}</strong>
                </div>
              )}
            </div>
          ) : null}

          {/* ── Navigation links ─────────────────────────────────────────────── */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-3)' }}>
            <Link
              to="/slots"
              style={{
                display:      'inline-block',
                minHeight:    44,
                lineHeight:   '44px',
                padding:      '0 var(--space-6)',
                background:   'var(--color-primary)',
                color:        'var(--color-text-inverse)',
                borderRadius: 'var(--radius-sm)',
                textDecoration: 'none',
                fontWeight:   600,
                fontSize:     '14px',
              }}
            >
              Book another appointment
            </Link>
          </div>

          {/* ── Calendar sync section ────────────────────────────────────────────
               Rendered as a sibling to the booking details — never blocks or delays
               the visibility of the confirmation content (Edge: non-blocking; AC-005).
               AC-001, AC-002: independent per-provider buttons with inline status.
          ─────────────────────────────────────────────────────────────────────── */}
          {state && (
            <CalendarSyncSection
              bookingId={state.bookingId}
              accessToken={accessToken}
            />
          )}
        </div>
      </main>
    </>
  )
}
