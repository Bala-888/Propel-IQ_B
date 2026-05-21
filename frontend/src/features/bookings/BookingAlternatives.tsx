import type { SlotDto } from '../../api/slotsApi'
import { SlotCell } from '../slots/SlotCell'

// ── Icons ─────────────────────────────────────────────────────────────────────────────────────────

function WarningIcon() {
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

// ── Props ─────────────────────────────────────────────────────────────────────────────────────────

interface BookingAlternativesProps {
  /**
   * The up to 3 alternative available slots returned in the 409 `SLOT_UNAVAILABLE` body.
   * Rendering is skipped when the array is empty.
   */
  alternatives: SlotDto[]
  /**
   * Called when the patient clicks an alternative slot cell — should update the `selectedSlotId`
   * in the parent `SlotCalendar` and clear the booking error so the calendar returns to its
   * normal selection state (AC-002; UXR-602).
   */
  onSelect: (slotId: number) => void
}

// ── Component ─────────────────────────────────────────────────────────────────────────────────────

/**
 * Renders exactly up to 3 alternative available slot cells below a `role="alert"` conflict
 * message after a 409 `SLOT_UNAVAILABLE` response (us_020; AC-002; UXR-602; UXR-105).
 *
 * Accessibility:
 * - The outer `<section aria-label="Alternative slots">` wraps the entire block so assistive
 *   technologies can navigate to it as a landmark.
 * - `<div role="alert">` announces the conflict message immediately on mount (WCAG 4.1.3).
 * - Icon + text — status never communicated by colour alone (UXR-105; WCAG 1.4.1).
 */
export function BookingAlternatives({ alternatives, onSelect }: BookingAlternativesProps) {
  if (alternatives.length === 0) return null

  return (
    <section
      aria-label="Alternative slots"
      style={{ marginTop: 'var(--space-5)', marginBottom: 'var(--space-6)' }}
    >
      {/* Conflict alert — role="alert" announces immediately on mount (WCAG 4.1.3) */}
      <div
        role="alert"
        style={{
          display:      'flex',
          alignItems:   'flex-start',
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
        {/* Icon — status not communicated by colour alone (UXR-105; WCAG 1.4.1) */}
        <WarningIcon />
        <span>
          This slot is no longer available. Here are some alternatives:
        </span>
      </div>

      {/* Alternative SlotCell rows — up to 3 (UXR-602) */}
      <div
        style={{
          display:  'flex',
          flexWrap: 'wrap',
          gap:      'var(--space-3)',
        }}
      >
        {alternatives.slice(0, 3).map(slot => (
          <SlotCell
            key={slot.id}
            slot={slot}
            isSelected={false}
            onSelect={onSelect}
          />
        ))}
      </div>
    </section>
  )
}
