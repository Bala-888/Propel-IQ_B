import { useState } from 'react'
import {
  triggerCalendarSync,
  getCalendarSyncStatus,
  CalendarSyncError,
  type CalendarProvider,
} from '../../api/calendarSyncApi'

// ── Per-provider sync states ─────────────────────────────────────────────────────────────────────

type SyncState = 'Idle' | 'Loading' | 'Synced' | 'Failed' | 'TokenExpired'

// ── Inline SVG icons (UXR-105 — status communicated by icon + text, never colour alone) ──────────

function CheckIcon() {
  return (
    <svg aria-hidden="true" width="15" height="15" viewBox="0 0 24 24" fill="none"
         stroke="var(--color-status-success, #16a34a)" strokeWidth="2.5"
         strokeLinecap="round" strokeLinejoin="round">
      <path d="M20 6L9 17l-5-5" />
    </svg>
  )
}

function WarningIcon() {
  return (
    <svg aria-hidden="true" width="15" height="15" viewBox="0 0 24 24" fill="none"
         stroke="var(--color-status-error, #dc2626)" strokeWidth="2"
         strokeLinecap="round" strokeLinejoin="round">
      <path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z" />
      <line x1="12" y1="9" x2="12" y2="13" />
      <line x1="12" y1="17" x2="12.01" y2="17" />
    </svg>
  )
}

function CalendarIcon() {
  return (
    <svg aria-hidden="true" width="15" height="15" viewBox="0 0 24 24" fill="none"
         stroke="currentColor" strokeWidth="1.5"
         strokeLinecap="round" strokeLinejoin="round">
      <rect x="3" y="4" width="18" height="18" rx="2" />
      <path d="M16 2v4M8 2v4M3 10h18" />
    </svg>
  )
}

function SpinnerIcon() {
  return (
    <span
      aria-hidden="true"
      style={{
        display:       'inline-block',
        width:         '14px',
        height:        '14px',
        border:        '2px solid rgba(0,0,0,0.15)',
        borderTopColor: '#333',
        borderRadius:  '50%',
        animation:     'calendarSpin 0.6s linear infinite',
        flexShrink:    0,
      }}
    />
  )
}

// ── Shared button base styles ─────────────────────────────────────────────────────────────────────

const btnBase: React.CSSProperties = {
  fontFamily:     'var(--font-sans, system-ui)',
  fontSize:       '14px',
  fontWeight:     600,
  padding:        'var(--space-3, 12px) var(--space-5, 20px)',
  borderRadius:   'var(--radius-sm, 4px)',
  border:         '1px solid var(--color-border, #e2e8f0)',
  background:     'var(--color-bg-surface, #fff)',
  color:          'var(--color-text-primary, #0f172a)',
  cursor:         'pointer',
  display:        'inline-flex',
  alignItems:     'center',
  gap:            '6px',
  transition:     'background 0.15s',
  minHeight:      '44px',
  minWidth:       '44px',
}

// ── Props ────────────────────────────────────────────────────────────────────────────────────────

interface CalendarSyncSectionProps {
  /** Numeric PK of the confirmed booking. */
  bookingId:   number
  /** JWT access token from AuthContext. Null means unauthenticated (buttons disabled). */
  accessToken: string | null
}

// ── Component ─────────────────────────────────────────────────────────────────────────────────────

/**
 * Async calendar sync section rendered below the booking confirmation details on SCR-007.
 *
 * Maintains independent state per provider so clicking one button does not affect the other
 * (us_028; AC-001, AC-002).
 *
 * Non-blocking: this section is rendered as a sibling to the booking details, not a dependency,
 * so it never delays the visibility of the main confirmation content (Edge: SCR-007; AC-005).
 *
 * UXR-105: all sync status indicators use both an icon (aria-hidden) and visible text.
 * WCAG 2.1 A: success uses role="status"; token-expired uses role="alert".
 */
export function CalendarSyncSection({ bookingId, accessToken }: CalendarSyncSectionProps) {
  const [googleStatus,  setGoogleStatus]  = useState<SyncState>('Idle')
  const [outlookStatus, setOutlookStatus] = useState<SyncState>('Idle')

  // Token-expired banners are per-provider and persist until dismissed
  const [googleAlertDismissed,  setGoogleAlertDismissed]  = useState(false)
  const [outlookAlertDismissed, setOutlookAlertDismissed] = useState(false)

  async function handleSync(provider: CalendarProvider) {
    if (!accessToken) return

    const setStatus = provider === 'Google' ? setGoogleStatus : setOutlookStatus

    setStatus('Loading')

    try {
      await triggerCalendarSync(bookingId, provider, accessToken)
      // 202 received — sync is running asynchronously in the backend worker
      setStatus('Synced')

      // Poll once after 3 s to catch TokenExpired (Edge: expired OAuth token)
      setTimeout(async () => {
        try {
          const polledStatus = await getCalendarSyncStatus(bookingId, provider, accessToken)
          if (polledStatus === 'TokenExpired') {
            setStatus('TokenExpired')
          }
          // "Synced", "Pending", "Failed" — keep current "Synced" display; worker may still be
          // processing and a "Pending" result at poll time does not indicate a failure.
        } catch {
          // Poll failure is non-critical — do not override the "Synced" state
        }
      }, 3000)
    } catch (err) {
      // Non-202 response from the trigger endpoint (AC-005 — failure never affects booking state)
      if (err instanceof CalendarSyncError) {
        setStatus('Failed')
      } else {
        setStatus('Failed')
      }
    }
  }

  const showGoogleAlert  = googleStatus  === 'TokenExpired' && !googleAlertDismissed
  const showOutlookAlert = outlookStatus === 'TokenExpired' && !outlookAlertDismissed

  return (
    <>
      {/* Keyframe animation for spinner — injected inline once (harmless duplicate) */}
      <style>{`@keyframes calendarSpin { to { transform: rotate(360deg); } }`}</style>

      {/* ── Section divider ──────────────────────────────────────────────────────────── */}
      <hr
        role="separator"
        style={{
          border:     'none',
          borderTop:  '1px solid var(--color-border, #e2e8f0)',
          margin:     'var(--space-6, 24px) 0',
        }}
      />

      {/* ── Section heading ──────────────────────────────────────────────────────────── */}
      <p
        style={{
          fontSize:     '15px',
          fontWeight:   600,
          color:        'var(--color-text-primary, #0f172a)',
          marginBottom: 'var(--space-4, 16px)',
        }}
      >
        Add to calendar <span style={{ fontWeight: 400, color: 'var(--color-text-secondary, #475569)' }}>(optional)</span>
      </p>

      {/* ── Button row ───────────────────────────────────────────────────────────────── */}
      <div
        style={{
          display:   'flex',
          gap:       'var(--space-3, 12px)',
          flexWrap:  'wrap',
          marginBottom: 'var(--space-4, 16px)',
        }}
      >
        <SyncButton
          provider="Google"
          status={googleStatus}
          disabled={!accessToken}
          onSync={() => handleSync('Google')}
        />
        <SyncButton
          provider="Outlook"
          status={outlookStatus}
          disabled={!accessToken}
          onSync={() => handleSync('Outlook')}
        />
      </div>

      {/* ── Per-provider success / error status (UXR-105; WCAG role="status") ─────── */}
      {googleStatus === 'Synced' && (
        <SyncStatusLabel provider="Google" type="success" />
      )}
      {googleStatus === 'Failed' && (
        <SyncStatusLabel provider="Google" type="failed" onRetry={() => handleSync('Google')} />
      )}
      {outlookStatus === 'Synced' && (
        <SyncStatusLabel provider="Outlook" type="success" />
      )}
      {outlookStatus === 'Failed' && (
        <SyncStatusLabel provider="Outlook" type="failed" onRetry={() => handleSync('Outlook')} />
      )}

      {/* ── Token-expired banners (WCAG role="alert"; persist until dismissed) ─────── */}
      {showGoogleAlert && (
        <TokenExpiredBanner onDismiss={() => setGoogleAlertDismissed(true)} />
      )}
      {showOutlookAlert && (
        <TokenExpiredBanner onDismiss={() => setOutlookAlertDismissed(true)} />
      )}
    </>
  )
}

// ── Sub-components ────────────────────────────────────────────────────────────────────────────────

interface SyncButtonProps {
  provider: CalendarProvider
  status:   SyncState
  disabled: boolean
  onSync:   () => void
}

function SyncButton({ provider, status, disabled, onSync }: SyncButtonProps) {
  const isLoading = status === 'Loading'
  const label     = provider === 'Google' ? 'Google Calendar' : 'Outlook Calendar'

  return (
    <button
      onClick={onSync}
      disabled={disabled || isLoading}
      aria-label={isLoading ? `Syncing to ${label}…` : `Add to ${label}`}
      style={{
        ...btnBase,
        opacity: (disabled || isLoading) ? 0.7 : 1,
        cursor:  (disabled || isLoading) ? 'not-allowed' : 'pointer',
      }}
    >
      {isLoading ? <SpinnerIcon /> : <CalendarIcon />}
      {isLoading ? 'Syncing…' : `Add to ${label}`}
    </button>
  )
}

interface SyncStatusLabelProps {
  provider: CalendarProvider
  type:     'success' | 'failed'
  onRetry?: () => void
}

function SyncStatusLabel({ provider, type, onRetry }: SyncStatusLabelProps) {
  const label = provider === 'Google' ? 'Google Calendar' : 'Outlook Calendar'

  if (type === 'success') {
    return (
      // role="status" — polite live region; announced without interruption (WCAG 2.1 A; UXR-105)
      <span
        role="status"
        style={{
          display:    'flex',
          alignItems: 'center',
          gap:        '6px',
          fontSize:   '13px',
          color:      'var(--color-status-success, #16a34a)',
          marginBottom: 'var(--space-2, 8px)',
        }}
      >
        <CheckIcon />
        Added to {label}
      </span>
    )
  }

  return (
    // role="status" — error inline feedback (not an alert; booking is unaffected — AC-005)
    <span
      role="status"
      style={{
        display:    'flex',
        alignItems: 'center',
        gap:        '6px',
        fontSize:   '13px',
        color:      'var(--color-status-error, #dc2626)',
        marginBottom: 'var(--space-2, 8px)',
      }}
    >
      <WarningIcon />
      Calendar sync failed. Please{' '}
      {onRetry ? (
        <button
          onClick={onRetry}
          style={{
            background:  'none',
            border:      'none',
            cursor:      'pointer',
            color:       'var(--color-primary, #1a56db)',
            fontWeight:  600,
            fontSize:    '13px',
            padding:     0,
            textDecoration: 'underline',
          }}
        >
          try again
        </button>
      ) : 'try again'}.
    </span>
  )
}

interface TokenExpiredBannerProps {
  onDismiss: () => void
}

function TokenExpiredBanner({ onDismiss }: TokenExpiredBannerProps) {
  return (
    // role="alert" — assertive live region; announced immediately (WCAG 2.1 A; UXR-105; Edge: token expired)
    <div
      role="alert"
      style={{
        display:      'flex',
        alignItems:   'flex-start',
        gap:          '8px',
        background:   '#fef9c3',
        border:       '1px solid #fde047',
        borderRadius: 'var(--radius-sm, 4px)',
        padding:      'var(--space-3, 12px) var(--space-4, 16px)',
        fontSize:     '13px',
        color:        'var(--color-text-primary, #0f172a)',
        marginTop:    'var(--space-3, 12px)',
      }}
    >
      <WarningIcon />
      <span style={{ flex: 1 }}>
        Calendar sync unavailable. Please reconnect your calendar in Settings.
      </span>
      <button
        onClick={onDismiss}
        aria-label="Dismiss calendar sync warning"
        style={{
          background: 'none',
          border:     'none',
          cursor:     'pointer',
          fontSize:   '18px',
          lineHeight: '1',
          color:      'var(--color-text-secondary, #475569)',
          padding:    '0 0 0 8px',
          minHeight:  '24px',
          minWidth:   '24px',
        }}
      >
        ×
      </button>
    </div>
  )
}
