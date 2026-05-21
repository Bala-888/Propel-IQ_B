import { useState, useEffect, useCallback } from 'react'
import { useNavigate, Link } from 'react-router-dom'
import { useAuth } from '../../context/AuthContext'
import { getSlots, type SlotDto, type PaginationMeta } from '../../api/slotsApi'
import { createBooking, type BookingConflictError } from '../../api/bookingsApi'
import { getInsurancePreCheck, type InsuranceStatus } from '../../api/insuranceApi'
import { SlotCell } from './SlotCell'
import { SlotPagination } from './SlotPagination'
import { BookingConfirmDialog } from '../bookings/BookingConfirmDialog'
import { BookingAlternatives } from '../bookings/BookingAlternatives'

const PAGE_SIZE = 10

// ── Icons ─────────────────────────────────────────────────────────────────────────────────────────

function ErrorIcon() {
  return (
    <svg aria-hidden="true" width="16" height="16" viewBox="0 0 24 24" fill="none"
         stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"
         style={{ flexShrink: 0 }}>
      <circle cx="12" cy="12" r="10" />
      <line x1="12" y1="8" x2="12" y2="12" />
      <circle cx="12" cy="16" r="0.5" fill="currentColor" />
    </svg>
  )
}

function InfoIcon() {
  return (
    <svg aria-hidden="true" width="15" height="15" viewBox="0 0 24 24" fill="none"
         stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"
         style={{ flexShrink: 0 }}>
      <circle cx="12" cy="12" r="10" />
      <line x1="12" y1="16" x2="12" y2="12" />
      <line x1="12" y1="8" x2="12.01" y2="8" />
    </svg>
  )
}

function CalendarIcon() {
  return (
    <svg aria-hidden="true" width="16" height="16" viewBox="0 0 24 24" fill="none"
         stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"
         style={{ flexShrink: 0 }}>
      <rect x="3" y="4" width="18" height="18" rx="2" ry="2" />
      <line x1="16" y1="2" x2="16" y2="6" />
      <line x1="8" y1="2" x2="8" y2="6" />
      <line x1="3" y1="10" x2="21" y2="10" />
    </svg>
  )
}

function AlertIcon() {
  return (
    <svg aria-hidden="true" width="16" height="16" viewBox="0 0 24 24" fill="none"
         stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"
         style={{ flexShrink: 0 }}>
      <path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z" />
      <line x1="12" y1="9" x2="12" y2="13" />
      <circle cx="12" cy="17" r="0.5" fill="currentColor" />
    </svg>
  )
}

// ── Helpers ───────────────────────────────────────────────────────────────────────────────────────

/** Groups an array of SlotDto by their `date` string into a sorted Record. */
function groupByDate(slots: SlotDto[]): [string, SlotDto[]][] {
  const map = slots.reduce<Record<string, SlotDto[]>>((acc, slot) => {
    if (!acc[slot.date]) acc[slot.date] = []
    acc[slot.date].push(slot)
    return acc
  }, {})
  return Object.entries(map).sort(([a], [b]) => a.localeCompare(b))
}

/** Formats an ISO date string "yyyy-MM-dd" → human-readable, e.g. "Wednesday, 21 May 2026". */
function formatDate(iso: string): string {
  const d = new Date(`${iso}T00:00:00`)
  if (isNaN(d.getTime())) return iso
  return d.toLocaleDateString('en-US', { weekday: 'long', year: 'numeric', month: 'long', day: 'numeric' })
}

// ── Loading skeleton ──────────────────────────────────────────────────────────────────────────────

function LoadingSkeleton() {
  return (
    <div aria-busy="true" aria-label="Loading available slots" style={{ paddingTop: 'var(--space-4)' }}>
      {Array.from({ length: 3 }).map((_, gi) => (
        <div key={gi} style={{ marginBottom: 'var(--space-8)' }}>
          <div style={{
            width: 180, height: 18, borderRadius: 'var(--radius-sm)',
            background: 'var(--color-bg-subtle)', marginBottom: 'var(--space-3)',
          }} />
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: 'var(--space-3)' }}>
            {Array.from({ length: 4 }).map((_, ci) => (
              <div key={ci} style={{
                width: 110, height: 80, borderRadius: 'var(--radius-sm)',
                background: 'var(--color-bg-subtle)',
              }} />
            ))}
          </div>
        </div>
      ))}
    </div>
  )
}

// ── SlotCalendar (SCR-006) ────────────────────────────────────────────────────────────────────────

/**
 * SCR-006 — Appointment Slot Calendar (us_019; AC-001–AC-004; UXR-604).
 *
 * Fetches available slots from `GET /api/slots?available=true` and renders them grouped by
 * calendar date. Supports single-slot selection that enables the "Book this slot" button.
 * Pagination allows browsing beyond the first page (AC-002).
 *
 * PHI guardrails: no patient identifiers are present in slot data — `SlotDto` contains
 * only schedule data (OWASP A02; HIPAA minimum-necessary).
 */
export function SlotCalendar() {
  const { accessToken }  = useAuth()
  const navigate         = useNavigate()

  // ── Slots state ──────────────────────────────────────────────────────────────────────────
  const [slots,          setSlots]          = useState<SlotDto[]>([])
  const [pagination,     setPagination]     = useState<PaginationMeta>({ total: 0, page: 1, pageSize: PAGE_SIZE, totalPages: 0 })
  const [selectedSlotId, setSelectedSlotId] = useState<number | null>(null)
  const [isFetching,     setIsFetching]     = useState(false)
  const [fetchError,     setFetchError]     = useState<string | null>(null)

  // ── Dialog + booking error state ─────────────────────────────────────────────────────────
  const [isDialogOpen,      setIsDialogOpen]      = useState(false)
  const [bookingError,      setBookingError]      = useState<BookingConflictError | null>(null)

  // ── Insurance pre-check state (us_023; AC-002; AC-003) ────────────────────────
  // null = pre-check not yet run or failed silently (dialog opens without alert)
  const [insuranceStatus,   setInsuranceStatus]   = useState<InsuranceStatus | null>(null)
  const [isPreCheckLoading, setIsPreCheckLoading] = useState(false)

  // ── Insurance banner state (UXR-604) ─────────────────────────────────────────────────────
  // Shown by default because `insuranceId` is not yet available in AuthContext or a patient
  // profile hook — the banner is a soft-alert (role="status") that the patient can dismiss.
  const [insuranceBannerDismissed, setInsuranceBannerDismissed] = useState(false)

  // ── Fetch slots ───────────────────────────────────────────────────────────────────────────

  const fetchPage = useCallback(async (page: number) => {
    if (!accessToken) return
    setIsFetching(true)
    setFetchError(null)
    setSelectedSlotId(null) // reset selection on page change
    setBookingError(null)   // clear any residual booking error on navigation

    try {
      const res = await getSlots(accessToken, { page, pageSize: PAGE_SIZE })
      setSlots(res.slots)
      setPagination(res.pagination)
    } catch (err) {
      setFetchError(err instanceof Error ? err.message : 'An unexpected error occurred.')
    } finally {
      setIsFetching(false)
    }
  }, [accessToken])

  useEffect(() => {
    void fetchPage(1)
  }, [fetchPage])

  // ── Slot selection ────────────────────────────────────────────────────────────────────────

  function handleSelect(id: number) {
    setSelectedSlotId(prev => prev === id ? null : id) // toggle if same slot clicked twice
    setBookingError(null) // clear booking error when patient makes a new selection
  }

  // ── Book button — opens MOD-002 dialog with insurance pre-check (us_023; AC-001) ───────

  async function handleBook() {
    if (selectedSlotId === null || !accessToken) return
    setBookingError(null)
    setInsuranceStatus(null) // reset stale status from any previous click
    setIsPreCheckLoading(true)
    try {
      const status = await getInsurancePreCheck(accessToken)
      setInsuranceStatus(status)
    } catch {
      // Network error — treat as silent skip; dialog still opens (Edge: API unavailable)
      setInsuranceStatus(null)
    } finally {
      setIsPreCheckLoading(false)
    }
    setIsDialogOpen(true)
  }

  // ── Confirm booking — called by BookingConfirmDialog.onConfirm ────────────────────────────

  async function handleConfirmBooking() {
    if (selectedSlotId === null || !accessToken) return
    try {
      const res = await createBooking(accessToken, selectedSlotId)
      setIsDialogOpen(false)
      setSelectedSlotId(null) // clear so back-navigation doesn't re-trigger the flow (AC-001)
      navigate('/booking/confirmation', { state: { bookingId: res.bookingId, slot: res.slot } })
    } catch (err) {
      setIsDialogOpen(false)
      if (isBookingConflictError(err)) {
        setBookingError(err)
      }
    }
  }

  // ── Alternative slot selected from BookingAlternatives ────────────────────────────────────

  function handleAlternativeSelect(slotId: number) {
    setSelectedSlotId(slotId)
    setBookingError(null) // clear error so calendar returns to normal selection state (AC-002)
  }

  // Type guard for BookingConflictError
  function isBookingConflictError(err: unknown): err is BookingConflictError {
    return typeof err === 'object' && err !== null && 'errorCode' in err
  }

  // ── Derived: grouped slots ────────────────────────────────────────────────────────────────
  const groupedSlots = groupByDate(slots)

  // ── Render ────────────────────────────────────────────────────────────────────────────────
  return (
    <div
      style={{
        maxWidth: 800,
        margin:   '0 auto',
        padding:  'var(--space-6)',
      }}
    >
      {/* ── Insurance soft-alert banner (UXR-604; UXR-105; WCAG 4.1.3) ────── */}
      {!insuranceBannerDismissed && (
        <div
          role="status"
          aria-live="polite"
          style={{
            display:       'flex',
            alignItems:    'flex-start',
            gap:           'var(--space-3)',
            background:    '#FFFBEB',
            border:        '1px solid var(--color-status-warning)',
            borderRadius:  'var(--radius-sm)',
            padding:       'var(--space-3) var(--space-4)',
            marginBottom:  'var(--space-5)',
            fontSize:      '13px',
            color:         'var(--color-status-warning)',
          }}
        >
          {/* Icon + text — status not communicated by colour alone (UXR-105; WCAG 1.4.1) */}
          <InfoIcon />
          <span style={{ flex: 1 }}>
            Your insurance information may be missing. Consider adding it before booking.
          </span>
          <button
            type="button"
            aria-label="Dismiss insurance notice"
            onClick={() => setInsuranceBannerDismissed(true)}
            style={{
              background: 'none',
              border:     'none',
              cursor:     'pointer',
              color:      'var(--color-status-warning)',
              fontFamily: 'var(--font-sans)',
              fontSize:   '16px',
              lineHeight:  1,
              padding:    'var(--space-1)',
              minHeight:  32,
              minWidth:   32,
            }}
          >
            ×
          </button>
        </div>
      )}

      {/* ── Page heading ─────────────────────────────────────────────────────── */}
      <div
        style={{
          display:       'flex',
          alignItems:    'center',
          gap:           'var(--space-3)',
          marginBottom:  'var(--space-6)',
        }}
      >
        <CalendarIcon />
        <h1 style={{ fontSize: '20px', fontWeight: 700, color: 'var(--color-text-primary)', margin: 0 }}>
          Available Appointment Slots
        </h1>
      </div>

      {/* ── Fetch error (icon + text; role="alert"; WCAG 4.1.3; UXR-105) ────── */}
      {fetchError && (
        <div
          role="alert"
          style={{
            display:       'flex',
            alignItems:    'center',
            gap:           'var(--space-2)',
            background:    'var(--color-error-surface)',
            border:        '1px solid var(--color-error-border)',
            borderRadius:  'var(--radius-sm)',
            padding:       'var(--space-3) var(--space-4)',
            color:         'var(--color-status-error)',
            fontSize:      '14px',
          }}
        >
          <ErrorIcon />
          <span>{fetchError}</span>
        </div>
      )}

      {/* ── SLOT_UNAVAILABLE conflict: BookingAlternatives (AC-002; UXR-602) ── */}
      {bookingError?.errorCode === 'SLOT_UNAVAILABLE' && (
        <BookingAlternatives
          alternatives={bookingError.alternatives}
          onSelect={handleAlternativeSelect}
        />
      )}

      {/* ── SLOT_BLOCKED: inline alert, no alternatives (Edge; UXR-105; WCAG 4.1.3) ── */}
      {bookingError?.errorCode === 'SLOT_BLOCKED' && (
        <span
          role="alert"
          style={{
            display:      'flex',
            alignItems:   'center',
            gap:          'var(--space-2)',
            background:   'var(--color-error-surface, #FEF2F2)',
            border:       '1px solid var(--color-error-border, #FECACA)',
            borderRadius: 'var(--radius-sm)',
            padding:      'var(--space-3) var(--space-4)',
            color:        'var(--color-status-error)',
            fontSize:     '14px',
            marginBottom: 'var(--space-4)',
          }}
        >
          <AlertIcon />
          This slot has been blocked by the clinic.
        </span>
      )}

      {/* ── DUPLICATE_WINDOW: inline alert with "View my bookings" link (AC-004; WCAG 4.1.3) ── */}
      {bookingError?.errorCode === 'DUPLICATE_WINDOW' && (
        <span
          role="alert"
          style={{
            display:      'flex',
            alignItems:   'center',
            gap:          'var(--space-2)',
            background:   'var(--color-error-surface, #FEF2F2)',
            border:       '1px solid var(--color-error-border, #FECACA)',
            borderRadius: 'var(--radius-sm)',
            padding:      'var(--space-3) var(--space-4)',
            color:        'var(--color-status-error)',
            fontSize:     '14px',
            marginBottom: 'var(--space-4)',
            flexWrap:     'wrap',
          }}
        >
          <AlertIcon />
          <span>You already have an active booking for this time window.</span>
          <Link
            to="/bookings"
            style={{
              color:          'var(--color-primary)',
              textDecoration: 'underline',
              fontSize:       '14px',
              marginLeft:     'var(--space-2)',
              whiteSpace:     'nowrap',
            }}
          >
            View my bookings
          </Link>
        </span>
      )}

      {/* ── LOCK_TIMEOUT (503): retry alert with "Try again" button (Edge; WCAG 4.1.3) ── */}
      {bookingError?.errorCode === 'LOCK_TIMEOUT' && (
        <span
          role="alert"
          style={{
            display:      'flex',
            alignItems:   'center',
            gap:          'var(--space-2)',
            background:   'var(--color-error-surface, #FEF2F2)',
            border:       '1px solid var(--color-error-border, #FECACA)',
            borderRadius: 'var(--radius-sm)',
            padding:      'var(--space-3) var(--space-4)',
            color:        'var(--color-status-error)',
            fontSize:     '14px',
            marginBottom: 'var(--space-4)',
            flexWrap:     'wrap',
          }}
        >
          <AlertIcon />
          <span>Booking could not be processed. Please try again.</span>
          {/* "Try again" reopens MOD-002 with selectedSlotId still intact (Edge: lock timeout) */}
          <button
            type="button"
            onClick={() => {
              setBookingError(null)
              setIsDialogOpen(true)
            }}
            style={{
              background:   'none',
              border:       '1px solid var(--color-status-error)',
              borderRadius: 'var(--radius-sm)',
              cursor:       'pointer',
              color:        'var(--color-status-error)',
              fontFamily:   'var(--font-sans)',
              fontSize:     '13px',
              padding:      'var(--space-1) var(--space-3)',
              marginLeft:   'var(--space-2)',
              minHeight:    32,
            }}
          >
            Try again
          </button>
        </span>
      )}

      {/* ── Loading skeleton ──────────────────────────────────────────────────── */}
      {isFetching && <LoadingSkeleton />}

      {/* ── Empty state (Edge: no available slots) ───────────────────────────── */}
      {!isFetching && !fetchError && slots.length === 0 && (
        <p
          style={{
            fontSize:   '15px',
            color:      'var(--color-text-secondary)',
            textAlign:  'center',
            padding:    'var(--space-12) var(--space-8)',
            lineHeight: 1.6,
          }}
        >
          No available slots at this time. Please check back later or add yourself to a wait list.
        </p>
      )}

      {/* ── Slot grid — grouped by date (AC-001; AC-003) ────────────────────── */}
      {!isFetching && !fetchError && slots.length > 0 && (
        <>
          {groupedSlots.map(([date, dateSlots]) => (
            <section
              key={date}
              aria-label={`Slots for ${formatDate(date)}`}
              style={{ marginBottom: 'var(--space-8)' }}
            >
              <h3
                style={{
                  fontSize:     '14px',
                  fontWeight:   700,
                  color:        'var(--color-text-primary)',
                  marginBottom: 'var(--space-3)',
                  paddingBottom: 'var(--space-2)',
                  borderBottom: '1px solid var(--color-border)',
                }}
              >
                {formatDate(date)}
              </h3>
              <div
                style={{
                  display:   'flex',
                  flexWrap:  'wrap',
                  gap:       'var(--space-3)',
                }}
              >
                {dateSlots.map(slot => (
                  <SlotCell
                    key={slot.id}
                    slot={slot}
                    isSelected={slot.id === selectedSlotId}
                    onSelect={handleSelect}
                  />
                ))}
              </div>
            </section>
          ))}

          {/* ── Pagination (AC-002) ─────────────────────────────────────────── */}
          <SlotPagination
            pagination={pagination}
            onPageChange={fetchPage}
            isLoading={isFetching}
          />
        </>
      )}

      {/* ── "Book this slot" button (AC-004; UXR-105; WCAG 2.5.5; WCAG 4.1.2) ── */}
      {!isFetching && !fetchError && slots.length > 0 && (
        <div
          style={{
            display:        'flex',
            justifyContent: 'flex-end',
            marginTop:      'var(--space-8)',
            paddingTop:     'var(--space-4)',
            borderTop:      '1px solid var(--color-border)',
          }}
        >
          <button
            type="button"
            onClick={() => { void handleBook() }}
            disabled={selectedSlotId === null || isPreCheckLoading}
            aria-label={isPreCheckLoading ? 'Checking insurance…' : 'Book this slot'}
            aria-disabled={selectedSlotId === null || isPreCheckLoading}
            style={{
              display:      'inline-flex',
              alignItems:   'center',
              gap:          'var(--space-2)',
              minHeight:    44,
              minWidth:     44,
              padding:      'var(--space-3) var(--space-8)',
              background:   selectedSlotId !== null && !isPreCheckLoading ? 'var(--color-primary)' : 'var(--color-bg-subtle)',
              color:        selectedSlotId !== null && !isPreCheckLoading ? 'var(--color-text-inverse)' : 'var(--color-text-disabled)',
              fontFamily:   'var(--font-sans)',
              fontSize:     '15px',
              fontWeight:   600,
              borderRadius: 'var(--radius-sm)',
              border:       selectedSlotId !== null && !isPreCheckLoading ? 'none' : '1px solid var(--color-border)',
              cursor:       selectedSlotId !== null && !isPreCheckLoading ? 'pointer' : 'default',
              // Disabled state: reduced opacity + icon change — not colour alone (UXR-105; WCAG 1.4.1)
              opacity:      selectedSlotId !== null && !isPreCheckLoading ? 1 : 0.6,
            }}
          >
            {/* Visual text indicator in disabled state — not colour alone (UXR-105; WCAG 1.4.1) */}
            {selectedSlotId === null ? 'Select a slot to book' : isPreCheckLoading ? 'Checking…' : 'Book this slot →'}
          </button>
        </div>
      )}

      {/* ── MOD-002 Booking Confirmation Dialog (us_020; AC-001) ─────────────── */}
      {isDialogOpen && selectedSlotId !== null && (() => {
        const selectedSlot = slots.find(s => s.id === selectedSlotId)
        if (!selectedSlot) return null
        return (
          <BookingConfirmDialog
            slot={selectedSlot}
            insuranceStatus={insuranceStatus}
            onConfirm={handleConfirmBooking}
            onClose={() => setIsDialogOpen(false)}
          />
        )
      })()}
    </div>
  )
}
