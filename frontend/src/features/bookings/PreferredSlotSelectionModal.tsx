import { useEffect, useRef, useState } from 'react'
import { getSlots, type SlotDto } from '../../api/slotsApi'
import { setPreferredSlot, ApiError } from '../../api/preferredSlotApi'

// ── Icons ─────────────────────────────────────────────────────────────────────────────────────────

function CheckIcon() {
  return (
    <svg aria-hidden="true" width="15" height="15" viewBox="0 0 24 24" fill="none"
         stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"
         style={{ flexShrink: 0 }}>
      <polyline points="20 6 9 17 4 12" />
    </svg>
  )
}

function WarningIcon() {
  return (
    <svg aria-hidden="true" width="15" height="15" viewBox="0 0 24 24" fill="none"
         stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"
         style={{ flexShrink: 0 }}>
      <path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z" />
      <line x1="12" y1="9" x2="12" y2="13" />
      <circle cx="12" cy="17" r="0.5" fill="currentColor" />
    </svg>
  )
}

function SpinnerIcon() {
  return (
    <svg aria-hidden="true" width="14" height="14" viewBox="0 0 24 24" fill="none"
         stroke="currentColor" strokeWidth="2.5" strokeLinecap="round"
         style={{ animation: 'spin 0.8s linear infinite', flexShrink: 0 }}>
      <path d="M12 2a10 10 0 0 1 10 10" />
    </svg>
  )
}

// ── Helpers ───────────────────────────────────────────────────────────────────────────────────────

function formatDate(iso: string): string {
  const d = new Date(`${iso}T00:00:00`)
  if (isNaN(d.getTime())) return iso
  return d.toLocaleDateString('en-US', { weekday: 'short', month: 'long', day: 'numeric' })
}

function formatTime(hms: string): string {
  const [hStr, mStr] = hms.split(':')
  const h      = parseInt(hStr ?? '0', 10)
  const m      = mStr ?? '00'
  const suffix = h >= 12 ? 'PM' : 'AM'
  const h12    = h % 12 === 0 ? 12 : h % 12
  return `${h12}:${m} ${suffix}`
}

// ── Props ─────────────────────────────────────────────────────────────────────────────────────────

interface PreferredSlotSelectionModalProps {
  /** Confirmed booking ID — sent in the POST body. */
  bookingId:     number
  /** ID of the currently booked slot — excluded from the selectable list (AC-003). */
  currentSlotId: number
  /** Whether the modal is open; drives the useEffect re-fetch (Edge: re-open refresh). */
  isOpen:        boolean
  /** Called when the patient closes or dismisses the modal. */
  onClose:       () => void
  /** JWT Bearer token for authenticated API calls. */
  accessToken:   string
}

// ── Component ─────────────────────────────────────────────────────────────────────────────────────

/**
 * MOD-003 — Preferred Slot Selection Modal (us_024; AC-001; AC-003).
 *
 * Fetches available slots on every open (`useEffect` on `isOpen`); filters out the patient's
 * currently booked slot; lets the patient designate one as preferred.
 *
 * All status feedback uses icon + text — never colour alone (UXR-105; WCAG 1.4.1).
 * Success uses `role="status"`; errors use `role="alert"` (WCAG 4.1.3).
 */
export function PreferredSlotSelectionModal({
  bookingId,
  currentSlotId,
  isOpen,
  onClose,
  accessToken,
}: PreferredSlotSelectionModalProps) {
  const dialogRef = useRef<HTMLDialogElement>(null)

  const [slots,              setSlots]              = useState<SlotDto[]>([])
  const [isFetching,         setIsFetching]         = useState(false)
  const [fetchError,         setFetchError]         = useState<string | null>(null)
  const [selectedSlotId,     setSelectedSlotId]     = useState<number | null>(null)
  const [confirmationSlotId, setConfirmationSlotId] = useState<number | null>(null)
  const [slotError,          setSlotError]          = useState<string | null>(null)
  const [isSubmitting,       setIsSubmitting]       = useState(false)

  // ── Open / close native dialog ────────────────────────────────────────────────────────────
  useEffect(() => {
    if (isOpen) {
      dialogRef.current?.showModal()
    } else {
      dialogRef.current?.close()
    }
  }, [isOpen])

  // Wire native cancel event (Escape key) to onClose
  useEffect(() => {
    const el = dialogRef.current
    if (!el) return
    const handler = () => onClose()
    el.addEventListener('cancel', handler)
    return () => el.removeEventListener('cancel', handler)
  }, [onClose])

  // ── Fetch available slots on every open (Edge: re-open refresh) ───────────────────────────
  useEffect(() => {
    if (!isOpen) return
    // Reset transient state on each open
    setSelectedSlotId(null)
    setSlotError(null)
    setConfirmationSlotId(null)
    void fetchSlots()
  }, [isOpen]) // eslint-disable-line react-hooks/exhaustive-deps

  async function fetchSlots() {
    setIsFetching(true)
    setFetchError(null)
    try {
      // Fetch first 50 available slots; the patient picks from this list
      const res = await getSlots(accessToken, { page: 1, pageSize: 50 })
      // AC-003: filter out the currently booked slot — never allow selecting it as preferred
      setSlots(res.slots.filter(s => s.id !== currentSlotId))
    } catch (err) {
      setFetchError(err instanceof Error ? err.message : 'Failed to load available slots.')
    } finally {
      setIsFetching(false)
    }
  }

  // ── Set preferred slot ────────────────────────────────────────────────────────────────────

  async function handleSetPreferred() {
    if (selectedSlotId === null || isSubmitting) return
    setIsSubmitting(true)
    setSlotError(null)

    try {
      await setPreferredSlot(accessToken, bookingId, selectedSlotId)
      setConfirmationSlotId(selectedSlotId)
    } catch (err) {
      if (err instanceof ApiError) {
        if (err.message.includes('no longer available')) {
          // Edge: slot became unavailable — show error and re-fetch (AC-001; Edge)
          setSlotError('This slot is no longer available. Please select another.')
          setSelectedSlotId(null)
          void fetchSlots()
        } else if (err.message.includes('active confirmed bookings')) {
          // AC-004: booking not confirmed — show error then close
          setSlotError('This booking is no longer active. The modal will close.')
          setTimeout(() => onClose(), 2000)
        } else {
          setSlotError(err.message)
        }
      } else {
        setSlotError('An unexpected error occurred. Please try again.')
      }
    } finally {
      setIsSubmitting(false)
    }
  }

  if (!isOpen) return null

  return (
    <>
      <style>{`@keyframes spin { to { transform: rotate(360deg); } }`}</style>

      <dialog
        ref={dialogRef}
        aria-modal="true"
        aria-labelledby="mod003-title"
        onClose={onClose}
        style={{
          border:       '1px solid var(--color-border)',
          borderRadius: 'var(--radius-md)',
          boxShadow:    'var(--shadow-3)',
          padding:      0,
          maxWidth:     520,
          width:        'calc(100vw - 2rem)',
          maxHeight:    '80vh',
          display:      'flex',
          flexDirection: 'column',
          fontFamily:   'var(--font-sans)',
          overflow:     'hidden',
        }}
      >
        {/* ── Header ──────────────────────────────────────────────────────── */}
        <div
          style={{
            display:        'flex',
            alignItems:     'center',
            justifyContent: 'space-between',
            padding:        'var(--space-5) var(--space-6)',
            borderBottom:   '1px solid var(--color-border)',
            gap:            'var(--space-3)',
            flexShrink:     0,
          }}
        >
          <h2
            id="mod003-title"
            style={{ fontSize: '16px', fontWeight: 700, margin: 0, color: 'var(--color-text-primary)' }}
          >
            Choose a Preferred Slot
          </h2>
          <button
            type="button"
            aria-label="Close"
            onClick={onClose}
            style={{
              background: 'none', border: 'none', cursor: 'pointer',
              color: 'var(--color-text-secondary)', fontSize: '18px', lineHeight: 1,
              padding: 'var(--space-1)', minHeight: 32, minWidth: 32, fontFamily: 'var(--font-sans)',
            }}
          >
            ×
          </button>
        </div>

        {/* ── Body ─────────────────────────────────────────────────────────── */}
        <div style={{ padding: 'var(--space-4) var(--space-6)', overflowY: 'auto', flex: 1 }}>
          <p style={{ fontSize: '13px', color: 'var(--color-text-secondary)', marginBottom: 'var(--space-4)', marginTop: 0 }}>
            Select an available slot to register as your preferred alternative. Your current booking is not affected.
          </p>

          {/* Fetch error */}
          {fetchError && (
            <div
              role="alert"
              style={{
                display: 'flex', alignItems: 'center', gap: 'var(--space-2)',
                background: '#FEF2F2', border: '1px solid #FECACA',
                borderRadius: 'var(--radius-sm)', padding: 'var(--space-3) var(--space-4)',
                color: 'var(--color-status-error)', fontSize: '13px', marginBottom: 'var(--space-3)',
              }}
            >
              <WarningIcon />
              <span>{fetchError}</span>
            </div>
          )}

          {/* Slot-level error (409 / unexpected) — icon + text (UXR-105; WCAG 4.1.3) */}
          {slotError && (
            <div
              role="alert"
              style={{
                display: 'flex', alignItems: 'flex-start', gap: 'var(--space-2)',
                background: '#FFFBEB', border: '1px solid #F59E0B',
                borderRadius: 'var(--radius-sm)', padding: 'var(--space-3) var(--space-4)',
                color: '#92400E', fontSize: '13px', marginBottom: 'var(--space-3)',
              }}
            >
              <WarningIcon />
              <span>{slotError}</span>
            </div>
          )}

          {/* Loading */}
          {isFetching && (
            <p style={{ fontSize: '13px', color: 'var(--color-text-secondary)', display: 'flex', alignItems: 'center', gap: 'var(--space-2)' }}>
              <SpinnerIcon /> Loading available slots…
            </p>
          )}

          {/* Empty state */}
          {!isFetching && !fetchError && slots.length === 0 && (
            <p style={{ fontSize: '13px', color: 'var(--color-text-secondary)', textAlign: 'center', padding: 'var(--space-6) 0' }}>
              No alternative slots are currently available.
            </p>
          )}

          {/* Slot list */}
          {!isFetching && slots.length > 0 && (
            <ul role="listbox" aria-label="Available slots" style={{ listStyle: 'none', margin: 0, padding: 0, display: 'flex', flexDirection: 'column', gap: 'var(--space-2)' }}>
              {slots.map(slot => {
                const isSelected  = selectedSlotId === slot.id
                const isConfirmed = confirmationSlotId === slot.id
                return (
                  <li
                    key={slot.id}
                    role="option"
                    aria-selected={isSelected}
                    style={{
                      display:      'flex',
                      alignItems:   'center',
                      justifyContent: 'space-between',
                      gap:          'var(--space-3)',
                      padding:      'var(--space-3) var(--space-4)',
                      borderRadius: 'var(--radius-sm)',
                      border:       `1px solid ${isConfirmed ? '#10B981' : isSelected ? 'var(--color-primary)' : 'var(--color-border)'}`,
                      background:   isConfirmed ? '#ECFDF5' : isSelected ? 'var(--color-primary-subtle)' : 'transparent',
                      cursor:       isConfirmed ? 'default' : 'pointer',
                    }}
                    onClick={() => { if (!isConfirmed && !isSubmitting) setSelectedSlotId(slot.id) }}
                  >
                    <div>
                      <div style={{ fontSize: '14px', fontWeight: 600, color: 'var(--color-text-primary)' }}>
                        {formatDate(slot.date)}
                      </div>
                      <div style={{ fontSize: '13px', color: 'var(--color-text-secondary)' }}>
                        {formatTime(slot.startTime)} · {slot.durationMinutes} min
                        {slot.providerName && ` · ${slot.providerName}`}
                      </div>
                    </div>

                    {/* Confirmed badge — icon + text (UXR-105; WCAG 1.4.1) */}
                    {isConfirmed ? (
                      <span
                        role="status"
                        style={{
                          display:    'inline-flex', alignItems: 'center', gap: 'var(--space-1)',
                          fontSize:   '12px', fontWeight: 600, color: '#065F46',
                          whiteSpace: 'nowrap', flexShrink: 0,
                        }}
                      >
                        <CheckIcon />
                        Preferred slot registered
                      </span>
                    ) : (
                      <button
                        type="button"
                        onClick={e => { e.stopPropagation(); setSelectedSlotId(slot.id); void handleSetPreferred() }}
                        disabled={isSubmitting || selectedSlotId !== slot.id && !isSelected}
                        aria-label={`Set ${formatDate(slot.date)} at ${formatTime(slot.startTime)} as preferred`}
                        style={{
                          minHeight:    36, padding: 'var(--space-1) var(--space-4)',
                          background:   isSelected ? 'var(--color-primary)' : 'none',
                          color:        isSelected ? 'var(--color-text-inverse)' : 'var(--color-primary)',
                          border:       `1px solid ${isSelected ? 'transparent' : 'var(--color-primary)'}`,
                          borderRadius: 'var(--radius-sm)', cursor: isSubmitting ? 'default' : 'pointer',
                          fontFamily:   'var(--font-sans)', fontSize: '13px', fontWeight: 600,
                          display:      'inline-flex', alignItems: 'center', gap: 'var(--space-1)',
                          flexShrink:   0, opacity: isSubmitting && selectedSlotId === slot.id ? 0.7 : 1,
                          whiteSpace:   'nowrap',
                        }}
                      >
                        {isSubmitting && selectedSlotId === slot.id ? <><SpinnerIcon /> Setting…</> : 'Set as Preferred'}
                      </button>
                    )}
                  </li>
                )
              })}
            </ul>
          )}
        </div>

        {/* ── Footer ───────────────────────────────────────────────────────── */}
        <div
          style={{
            display:        'flex',
            justifyContent: 'flex-end',
            padding:        'var(--space-4) var(--space-6)',
            borderTop:      '1px solid var(--color-border)',
            flexShrink:     0,
          }}
        >
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
            Done
          </button>
        </div>
      </dialog>
    </>
  )
}
