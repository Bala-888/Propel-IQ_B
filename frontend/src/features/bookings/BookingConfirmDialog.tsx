import { useEffect, useRef, useState } from 'react'
import type { SlotDto } from '../../api/slotsApi'
import type { InsuranceStatus } from '../../api/insuranceApi'
import { InsuranceAlertBanner } from './InsuranceAlertBanner'

// ── Icons ─────────────────────────────────────────────────────────────────────────────────────────

function CalendarIcon() {
  return (
    <svg aria-hidden="true" width="16" height="16" viewBox="0 0 24 24" fill="none"
         stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <rect x="3" y="4" width="18" height="18" rx="2" ry="2" />
      <line x1="16" y1="2" x2="16" y2="6" />
      <line x1="8" y1="2" x2="8" y2="6" />
      <line x1="3" y1="10" x2="21" y2="10" />
    </svg>
  )
}

function ClockIcon() {
  return (
    <svg aria-hidden="true" width="15" height="15" viewBox="0 0 24 24" fill="none"
         stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="12" cy="12" r="10" />
      <polyline points="12 6 12 12 16 14" />
    </svg>
  )
}

function SpinnerIcon() {
  return (
    <svg aria-hidden="true" width="16" height="16" viewBox="0 0 24 24" fill="none"
         stroke="currentColor" strokeWidth="2.5" strokeLinecap="round"
         style={{ animation: 'spin 0.8s linear infinite' }}>
      <path d="M12 2a10 10 0 0 1 10 10" />
    </svg>
  )
}

function CheckCircleIcon() {
  return (
    <svg aria-hidden="true" width="28" height="28" viewBox="0 0 24 24" fill="none"
         stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="12" cy="12" r="10" />
      <polyline points="9 12 11 14 15 10" />
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
  // hms is "HH:mm:ss" — strip seconds and convert to 12-hour format
  const [hStr, mStr] = hms.split(':')
  const h = parseInt(hStr ?? '0', 10)
  const m = mStr ?? '00'
  const suffix = h >= 12 ? 'PM' : 'AM'
  const h12   = h % 12 === 0 ? 12 : h % 12
  return `${h12}:${m} ${suffix}`
}

// ── Props ─────────────────────────────────────────────────────────────────────────────────────────

interface BookingConfirmDialogProps {
  /** The slot selected by the patient — drives the summary display. */
  slot:      SlotDto
  /** Called when the patient clicks "Confirm Booking". Implementer calls the booking API. */
  onConfirm: () => Promise<void>
  /** Called on "Cancel" click, native Escape key, or "Done" after a confirmed booking. */
  onClose:   () => void
  /**
   * Result of the insurance pre-check (us_023; AC-002; AC-003).
   * - `"Missing"` or `"Incomplete"` → soft alert banner shown above slot details.
   * - `"Complete"` or `null` (pre-check failed silently) → no banner rendered.
   * The "Confirm Booking" button is never disabled by this value (UXR-604).
   */
  insuranceStatus?: InsuranceStatus | null
  /**
   * Booking ID set by the parent after `onConfirm()` succeeds (us_024).
   * When non-null the dialog transitions to the post-booking success view,
   * which offers "Choose a Preferred Slot" and "Done" (navigate to confirmation page).
   */
  confirmedBookingId?: number | null
  /**
   * Called when the patient clicks "Choose a Preferred Slot" in the success view.
   * The parent is responsible for opening MOD-003 (us_024).
   */
  onPreferredSlot?: () => void
}

// ── Component ─────────────────────────────────────────────────────────────────────────────────────

/**
 * MOD-002 — Booking Confirmation Dialog (us_020; AC-001).
 *
 * Renders as a native `<dialog>` element opened via `showModal()` so the browser provides a
 * default backdrop, focus trap, and Escape-key dismissal — no custom JS required (WAI-ARIA
 * modal pattern; WCAG 2.5.5; WCAG 1.3.1).
 *
 * The "Confirm Booking" button is guarded by an `isSubmitting` flag to prevent double-submit;
 * during submission it shows a spinner icon — a visual indicator beyond colour alone (UXR-105;
 * WCAG 1.4.1).
 */
export function BookingConfirmDialog({ slot, onConfirm, onClose, insuranceStatus = null, confirmedBookingId = null, onPreferredSlot }: BookingConfirmDialogProps) {
  const dialogRef              = useRef<HTMLDialogElement>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  // Open the native dialog as a modal (provides backdrop + focus trap)
  useEffect(() => {
    dialogRef.current?.showModal()
  }, [])

  // Wire the native cancel event (Escape key) to onClose so parent state stays in sync
  useEffect(() => {
    const el = dialogRef.current
    if (!el) return
    const handler = () => onClose()
    el.addEventListener('cancel', handler)
    return () => el.removeEventListener('cancel', handler)
  }, [onClose])

  async function handleConfirm() {
    if (isSubmitting) return
    setIsSubmitting(true)
    try {
      await onConfirm()
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <>
      {/* Spin keyframe injected once alongside the dialog */}
      <style>{`@keyframes spin { to { transform: rotate(360deg); } }`}</style>

      <dialog
        ref={dialogRef}
        aria-modal="true"
        aria-labelledby="dialog-title"
        onClose={onClose}
        style={{
          border:       '1px solid var(--color-border)',
          borderRadius: 'var(--radius-md)',
          boxShadow:    'var(--shadow-3)',
          padding:      0,
          maxWidth:     480,
          width:        'calc(100vw - 2rem)',
          fontFamily:   'var(--font-sans)',
        }}
      >
        {/* ── Header ──────────────────────────────────────────────────────── */}
        <div
          style={{
            display:       'flex',
            alignItems:    'center',
            justifyContent: 'space-between',
            padding:       'var(--space-5) var(--space-6)',
            borderBottom:  '1px solid var(--color-border)',
            gap:           'var(--space-3)',
          }}
        >
          <h2
            id="dialog-title"
            style={{ fontSize: '16px', fontWeight: 700, margin: 0, color: 'var(--color-text-primary)' }}
          >
            {confirmedBookingId !== null ? 'Booking Confirmed' : 'Confirm Appointment Booking'}
          </h2>
          <button
            type="button"
            aria-label="Close dialog"
            onClick={onClose}
            style={{
              background: 'none',
              border:     'none',
              cursor:     'pointer',
              color:      'var(--color-text-secondary)',
              fontFamily: 'var(--font-sans)',
              fontSize:   '18px',
              lineHeight: 1,
              padding:    'var(--space-1)',
              minHeight:  32,
              minWidth:   32,
            }}
          >
            ×
          </button>
        </div>

        {/* ── Success view (post-booking) ──── rendered when confirmedBookingId is set */}
        {confirmedBookingId !== null ? (
          <div style={{ padding: 'var(--space-6)' }}>
            {/* icon + text — never colour alone (UXR-105; WCAG 1.4.1) */}
            <div
              role="status"
              style={{
                display: 'flex', flexDirection: 'column', alignItems: 'center',
                gap: 'var(--space-3)', textAlign: 'center', padding: 'var(--space-4) 0',
              }}
            >
              <span style={{ color: '#10B981' }}><CheckCircleIcon /></span>
              <p style={{ fontSize: '15px', fontWeight: 700, color: 'var(--color-text-primary)', margin: 0 }}>
                Your appointment has been booked!
              </p>
              <p style={{ fontSize: '13px', color: 'var(--color-text-secondary)', margin: 0 }}>
                Reference #{confirmedBookingId}
              </p>
            </div>

            {onPreferredSlot && (
              <div
                style={{
                  marginTop: 'var(--space-4)', padding: 'var(--space-3) var(--space-4)',
                  background: 'var(--color-primary-subtle)', borderRadius: 'var(--radius-sm)',
                  textAlign: 'center',
                }}
              >
                <p style={{ fontSize: '13px', color: 'var(--color-text-secondary)', margin: '0 0 var(--space-2) 0' }}>
                  Want a different slot if one opens up?
                </p>
                <button
                  type="button"
                  onClick={() => { onPreferredSlot(); onClose() }}
                  style={{
                    background: 'none', border: '1px solid var(--color-primary)',
                    borderRadius: 'var(--radius-sm)', color: 'var(--color-primary)',
                    cursor: 'pointer', fontFamily: 'var(--font-sans)', fontSize: '13px',
                    fontWeight: 600, padding: 'var(--space-2) var(--space-5)', minHeight: 36,
                  }}
                >
                  Choose a Preferred Slot
                </button>
              </div>
            )}

            <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: 'var(--space-5)' }}>
              <button
                type="button"
                onClick={onClose}
                style={{
                  minHeight: 44, padding: 'var(--space-2) var(--space-6)',
                  background: 'var(--color-primary)', color: 'var(--color-text-inverse)',
                  border: 'none', borderRadius: 'var(--radius-sm)', cursor: 'pointer',
                  fontFamily: 'var(--font-sans)', fontSize: '14px', fontWeight: 600,
                }}
              >
                View Booking Details
              </button>
            </div>
          </div>
        ) : (
        <>
        {/* ── Slot summary ─────────────────────────────────────────────────── */}
        <div style={{ padding: 'var(--space-6)' }}>
          {/* Insurance alert — soft, non-blocking; shown for Missing/Incomplete only (us_023; AC-002; UXR-604) */}
          {(insuranceStatus === 'Missing' || insuranceStatus === 'Incomplete') && (
            <InsuranceAlertBanner status={insuranceStatus} />
          )}

          <p style={{ fontSize: '14px', color: 'var(--color-text-secondary)', marginBottom: 'var(--space-4)' }}>
            You are about to book the following appointment slot:
          </p>

          <div
            style={{
              background:   'var(--color-primary-subtle)',
              borderRadius: 'var(--radius-sm)',
              padding:      'var(--space-4) var(--space-5)',
              display:      'flex',
              flexDirection: 'column',
              gap:          'var(--space-3)',
            }}
          >
            {/* Date row */}
            <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-2)' }}>
              <CalendarIcon />
              <span style={{ fontSize: '15px', fontWeight: 600, color: 'var(--color-text-primary)' }}>
                {formatDate(slot.date)}
              </span>
            </div>

            {/* Time + duration row */}
            <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-2)' }}>
              <ClockIcon />
              <span style={{ fontSize: '14px', color: 'var(--color-text-primary)' }}>
                {formatTime(slot.startTime)} &nbsp;·&nbsp; {slot.durationMinutes} min
              </span>
            </div>

            {/* Provider row */}
            {slot.providerName && (
              <div style={{ fontSize: '13px', color: 'var(--color-text-secondary)', paddingLeft: '20px' }}>
                Provider: <strong>{slot.providerName}</strong>
              </div>
            )}
          </div>
        </div>

        {/* ── Actions ──────────────────────────────────────────────────────── */}
        <div
          style={{
            display:       'flex',
            justifyContent: 'flex-end',
            gap:           'var(--space-3)',
            padding:       'var(--space-4) var(--space-6)',
            borderTop:     '1px solid var(--color-border)',
          }}
        >
          {/* Cancel — always enabled */}
          <button
            type="button"
            onClick={onClose}
            disabled={isSubmitting}
            style={{
              minHeight:    44,
              padding:      'var(--space-2) var(--space-5)',
              background:   'none',
              border:       '1px solid var(--color-border)',
              borderRadius: 'var(--radius-sm)',
              cursor:       isSubmitting ? 'default' : 'pointer',
              fontFamily:   'var(--font-sans)',
              fontSize:     '14px',
              color:        'var(--color-text-primary)',
              opacity:      isSubmitting ? 0.5 : 1,
            }}
          >
            Cancel
          </button>

          {/* Confirm — spinner + disabled while submitting (UXR-105; WCAG 1.4.1) */}
          <button
            type="button"
            onClick={() => { void handleConfirm() }}
            disabled={isSubmitting}
            aria-label={isSubmitting ? 'Booking in progress…' : 'Confirm Booking'}
            style={{
              display:      'inline-flex',
              alignItems:   'center',
              gap:          'var(--space-2)',
              minHeight:    44,
              padding:      'var(--space-2) var(--space-6)',
              background:   isSubmitting ? 'var(--color-primary-subtle)' : 'var(--color-primary)',
              color:        isSubmitting ? 'var(--color-primary)' : 'var(--color-text-inverse)',
              border:       'none',
              borderRadius: 'var(--radius-sm)',
              cursor:       isSubmitting ? 'default' : 'pointer',
              fontFamily:   'var(--font-sans)',
              fontSize:     '14px',
              fontWeight:   600,
              // Visual disabled indicator beyond colour (UXR-105; WCAG 1.4.1)
              outline:      isSubmitting ? '2px solid var(--color-primary)' : 'none',
              outlineOffset: '-2px',
            }}
          >
            {isSubmitting ? <SpinnerIcon /> : null}
            {isSubmitting ? 'Booking…' : 'Confirm Booking'}
          </button>
        </div>
        </>
        )}
      </dialog>
    </>
  )
}
