import { useState, useEffect, useCallback } from 'react'
import { useNavigate, Link } from 'react-router-dom'
import {
  getSummary,
  confirmIntake,
  SessionExpiredError,
  type IntakeSummary,
} from '../../api/intakeAiApi'
import { IntakeFieldRow } from './IntakeFieldRow'

export interface IntakeSummaryPanelProps {
  sessionId: string
  accessToken: string
  /**
   * Pre-fetched summary already in memory from the last `sendMessage` response.
   * Displayed immediately; refreshed from the API on mount for accuracy (AC-001).
   */
  initialSummary?: IntakeSummary
}

// ── Inline SVG icons ──────────────────────────────────────────────────────────

/** Checkmark icon for the confirm button (UXR-105). */
function CheckIcon() {
  return (
    <svg
      aria-hidden="true"
      width="16"
      height="16"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2.5"
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      <polyline points="20 6 9 17 4 12" />
    </svg>
  )
}

/** Warning icon for the session-expired banner (UXR-105 — icon + text, not colour-only). */
function WarningIcon() {
  return (
    <svg
      aria-hidden="true"
      width="16"
      height="16"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      style={{ flexShrink: 0 }}
    >
      <path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z" />
      <line x1="12" y1="9" x2="12" y2="13" />
      <circle cx="12" cy="17" r="0.5" fill="currentColor" />
    </svg>
  )
}

/** Five skeleton bars shown while the summary API call is in-flight. */
function LoadingSkeleton() {
  const pulse: React.CSSProperties = {
    background: 'linear-gradient(90deg, #E2E8F0 25%, #CBD5E1 50%, #E2E8F0 75%)',
    backgroundSize: '200% 100%',
    animation: 'skeleton-pulse 1.4s ease infinite',
    borderRadius: 'var(--radius-sm)',
    height: 14,
    marginBottom: 'var(--space-3)',
  }
  return (
    <>
      {/* Inject keyframes once via a style tag */}
      <style>{`@keyframes skeleton-pulse { 0%,100%{background-position:200% 0} 50%{background-position:-200% 0} }`}</style>
      {[70, 100, 85, 60, 90].map((w, i) => (
        <div key={i} style={{ marginBottom: 'var(--space-5)' }}>
          <div style={{ ...pulse, width: 80, marginBottom: 'var(--space-2)' }} />
          <div style={{ ...pulse, width: `${w}%` }} />
        </div>
      ))}
    </>
  )
}

/** Ordered metadata about every editable intake field (AC-001; us_016-II). */
const FIELD_DEFINITIONS: Array<{
  path: keyof IntakeSummary
  label: string
}> = [
  { path: 'chiefComplaint',  label: 'Chief Complaint' },
  { path: 'demographics',   label: 'Demographics'    },
  { path: 'medicalHistory', label: 'Medical History'  },
  { path: 'medications',    label: 'Medications'      },
  { path: 'allergies',      label: 'Allergies'        },
]

/**
 * SCR-004 — Intake summary / review panel (us_016-II).
 *
 * Lifecycle:
 * 1. Renders `initialSummary` (if provided) immediately, then fetches fresh data via
 *    `GET /intake/ai/summary` to guarantee accuracy (AC-001).
 * 2. Each of the 5 field groups is rendered as an `<IntakeFieldRow>`, which exposes an
 *    inline edit → `PATCH /intake/ai/field` flow with required-field validation (AC-002).
 * 3. "Confirm & Submit" calls `POST /intake/ai/confirm` → on success navigates to `/intake`
 *    with a confirmation toast state (AC-003; AC-004).
 * 4. If the session TTL elapsed before confirmation, a `role="alert"` session-expired banner
 *    is shown with a "Resume from draft" link; the patient is NOT auto-navigated away (Edge;
 *    UXR-105; WCAG 4.1.3).
 *
 * AIR guardrails: all PHI values live only in `useState` — never written to localStorage or
 * sessionStorage (checklist; HIPAA minimum-necessary; OWASP A02).
 */
export function IntakeSummaryPanel({ sessionId, accessToken, initialSummary }: IntakeSummaryPanelProps) {
  const navigate = useNavigate()

  // PHI lives only in React state — never written to browser storage (AIR guardrails)
  const [summary, setSummary]             = useState<IntakeSummary | null>(initialSummary ?? null)
  const [isLoading, setIsLoading]         = useState(!initialSummary)
  const [isConfirming, setIsConfirming]   = useState(false)
  const [sessionExpired, setSessionExpired] = useState(false)
  const [confirmError, setConfirmError]   = useState<string | null>(null)
  // Tracks which field paths are currently in edit mode — "Confirm" is disabled while any are open
  const [editingFields, setEditingFields] = useState<Set<string>>(new Set())

  // Fetch fresh summary from server on mount (AC-001) — even if initialSummary is provided
  useEffect(() => {
    let cancelled = false
    getSummary(accessToken, sessionId)
      .then(data => { if (!cancelled) { setSummary(data); setIsLoading(false) } })
      .catch(() => { if (!cancelled) setIsLoading(false) })
    return () => { cancelled = true }
  }, [sessionId, accessToken])

  const handleFieldUpdated = useCallback((fieldPath: keyof IntakeSummary, newValue: string) => {
    setSummary(prev => prev ? { ...prev, [fieldPath]: newValue } : prev)
  }, [])

  const handleEditingChange = useCallback((fieldPath: keyof IntakeSummary, isEditing: boolean) => {
    setEditingFields(prev => {
      const next = new Set(prev)
      isEditing ? next.add(fieldPath) : next.delete(fieldPath)
      return next
    })
  }, [])

  async function handleConfirm() {
    setConfirmError(null)
    setIsConfirming(true)
    try {
      await confirmIntake(accessToken, sessionId)
      navigate('/intake', { state: { message: 'Your intake has been saved.' } })
    } catch (err) {
      if (err instanceof SessionExpiredError) {
        // Edge: session TTL elapsed — show alert in-place; do NOT auto-navigate (UXR-105; WCAG 4.1.3)
        setSessionExpired(true)
      } else {
        setConfirmError(err instanceof Error ? err.message : 'An unexpected error occurred.')
      }
    } finally {
      setIsConfirming(false)
    }
  }

  const confirmDisabled = editingFields.size > 0 || isConfirming

  return (
    <section
      role="region"
      aria-label="Intake summary"
      style={{
        background: 'var(--color-bg-surface)',
        border: '1px solid var(--color-border)',
        borderRadius: 'var(--radius-md)',
        boxShadow: 'var(--shadow-2)',
        padding: 'var(--space-6)',
        marginTop: 'var(--space-6)',
      }}
    >
      {/* ── Panel header ─────────────────────────────────────────────────── */}
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          marginBottom: 'var(--space-5)',
          paddingBottom: 'var(--space-4)',
          borderBottom: '1px solid var(--color-border)',
        }}
      >
        <h2 style={{ fontSize: '15px', fontWeight: 600, color: 'var(--color-text-primary)' }}>
          Review your intake
        </h2>
        <span
          style={{
            display: 'inline-flex',
            alignItems: 'center',
            gap: '4px',
            background: 'var(--color-success-surface)',
            color: 'var(--color-status-success)',
            fontSize: '11px',
            fontWeight: 600,
            padding: '2px 8px',
            borderRadius: 'var(--radius-full)',
          }}
          aria-label="All fields collected"
        >
          <CheckIcon />
          <span>Complete</span>
        </span>
      </div>

      {/* ── Loading skeleton while GET /intake/ai/summary is in-flight ──── */}
      {isLoading && <LoadingSkeleton />}

      {/* ── Editable field rows ──────────────────────────────────────────── */}
      {!isLoading && summary && FIELD_DEFINITIONS.map(({ path, label }) => (
        <IntakeFieldRow
          key={path}
          sessionId={sessionId}
          accessToken={accessToken}
          fieldPath={path}
          label={label}
          value={summary[path]}
          onFieldUpdated={handleFieldUpdated}
          onEditingChange={handleEditingChange}
        />
      ))}

      {/* ── Session-expired banner (Edge; AC-003; UXR-105; WCAG 4.1.3) ─── */}
      {sessionExpired && (
        <div
          role="alert"
          style={{
            display: 'flex',
            flexDirection: 'column',
            gap: 'var(--space-3)',
            background: 'var(--color-error-surface)',
            border: '1px solid var(--color-error-border)',
            borderRadius: 'var(--radius-sm)',
            padding: 'var(--space-4)',
            marginBottom: 'var(--space-4)',
          }}
        >
          <div
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 'var(--space-2)',
              color: 'var(--color-status-error)',
              fontSize: '13px',
              fontWeight: 600,
            }}
          >
            {/* Icon+text required — colour is supplementary, not sole indicator (UXR-105) */}
            <WarningIcon />
            <span>Session expired. Your draft has been saved.</span>
          </div>
          <Link
            to="/intake"
            style={{
              fontSize: '13px',
              fontWeight: 600,
              color: 'var(--color-primary)',
              textDecoration: 'underline',
              alignSelf: 'flex-start',
            }}
          >
            Resume from draft
          </Link>
        </div>
      )}

      {/* ── Confirm submit error ─────────────────────────────────────────── */}
      {confirmError && !sessionExpired && (
        <div
          role="alert"
          style={{
            fontSize: '13px',
            color: 'var(--color-status-error)',
            marginBottom: 'var(--space-3)',
          }}
        >
          {confirmError}
        </div>
      )}

      {/* ── Confirm & Submit button ──────────────────────────────────────── */}
      {!sessionExpired && (
        <button
          type="button"
          onClick={handleConfirm}
          disabled={confirmDisabled}
          aria-disabled={confirmDisabled}
          title={editingFields.size > 0 ? 'Save or cancel open edits before submitting' : undefined}
          style={{
            width: '100%',
            marginTop: 'var(--space-4)',
            background: confirmDisabled ? 'var(--color-bg-subtle)' : 'var(--color-primary)',
            color: confirmDisabled ? 'var(--color-text-disabled)' : 'var(--color-text-inverse)',
            fontFamily: 'var(--font-sans)',
            fontSize: '15px',
            fontWeight: 600,
            padding: 'var(--space-3) var(--space-5)',
            borderRadius: 'var(--radius-sm)',
            border: confirmDisabled ? '1px solid var(--color-border)' : 'none',
            cursor: confirmDisabled ? 'not-allowed' : 'pointer',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            gap: 'var(--space-2)',
            minHeight: '44px',
            transition: 'background 0.15s',
          }}
        >
          <CheckIcon />
          {isConfirming ? 'Submitting…' : 'Confirm & Submit'}
        </button>
      )}
    </section>
  )
}
