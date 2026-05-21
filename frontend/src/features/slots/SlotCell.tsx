import type { SlotDto } from '../../api/slotsApi'

// ── Icons ─────────────────────────────────────────────────────────────────────────────────────────

/** Calendar clock icon — used alongside "Available" text so status is never colour-only
    (UXR-105; WCAG 1.4.1; AC-003). */
function AvailableIcon() {
  return (
    <svg
      aria-hidden="true"
      width="13"
      height="13"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2.5"
      strokeLinecap="round"
      strokeLinejoin="round"
      style={{ flexShrink: 0 }}
    >
      <circle cx="12" cy="12" r="10" />
      <polyline points="12 6 12 12 16 14" />
    </svg>
  )
}

// ── SlotCell ──────────────────────────────────────────────────────────────────────────────────────

export interface SlotCellProps {
  slot:       SlotDto
  isSelected: boolean
  onSelect:   (id: number) => void
}

/**
 * Individual appointment slot cell (SCR-006; us_019; AC-003; AC-004).
 *
 * - Renders start time, duration in minutes, and an icon + text "Available" label so the
 *   availability status is never communicated by colour alone (UXR-105; WCAG 1.4.1).
 * - Applies `--color-primary-subtle` background when selected (AC-004).
 * - Minimum 44×44 px touch target (WCAG 2.5.5; AC-004).
 * - Keyboard focusable and activatable via Space/Enter (`role="button"` + `tabIndex`).
 */
export function SlotCell({ slot, isSelected, onSelect }: SlotCellProps) {
  // Format "HH:mm:ss" → "HH:mm" for display
  const displayTime = slot.startTime.length >= 5 ? slot.startTime.slice(0, 5) : slot.startTime

  function handleKeyDown(e: React.KeyboardEvent) {
    if (e.key === ' ' || e.key === 'Enter') {
      e.preventDefault()
      onSelect(slot.id)
    }
  }

  return (
    <div
      role="button"
      tabIndex={0}
      aria-pressed={isSelected}
      aria-label={`${slot.status} slot at ${displayTime}, ${slot.durationMinutes} minutes${slot.providerName ? `, provider: ${slot.providerName}` : ''}`}
      onClick={() => onSelect(slot.id)}
      onKeyDown={handleKeyDown}
      style={{
        minHeight:    44,
        minWidth:     44,
        padding:      'var(--space-3) var(--space-4)',
        borderRadius: 'var(--radius-sm)',
        border:       isSelected
          ? '2px solid var(--color-primary)'
          : '1px solid var(--color-border)',
        background: isSelected
          ? 'var(--color-primary-subtle)'
          : 'var(--color-bg-surface)',
        cursor:   'pointer',
        display:  'flex',
        flexDirection: 'column',
        gap:      'var(--space-1)',
        userSelect: 'none',
        transition: 'background 0.1s, border-color 0.1s',
        outline: 'none',
      }}
      onFocus={e => { e.currentTarget.style.boxShadow = '0 0 0 3px rgba(26,86,219,0.18)' }}
      onBlur={e => { e.currentTarget.style.boxShadow = 'none' }}
    >
      {/* Start time — primary label */}
      <span style={{ fontSize: '15px', fontWeight: 700, color: 'var(--color-text-primary)', lineHeight: 1.2 }}>
        {displayTime}
      </span>

      {/* Duration */}
      <span style={{ fontSize: '12px', color: 'var(--color-text-secondary)' }}>
        {slot.durationMinutes} min
      </span>

      {/* Availability indicator — icon + text, never colour-only (UXR-105; WCAG 1.4.1; AC-003) */}
      <span
        style={{
          display:    'inline-flex',
          alignItems: 'center',
          gap:        '3px',
          fontSize:   '11px',
          fontWeight: 600,
          color:      'var(--color-primary)',
          marginTop:  'var(--space-1)',
        }}
      >
        <AvailableIcon />
        {slot.status}
      </span>

      {/* Provider name — optional, not PHI */}
      {slot.providerName && (
        <span style={{ fontSize: '11px', color: 'var(--color-text-secondary)', fontStyle: 'italic' }}>
          {slot.providerName}
        </span>
      )}
    </div>
  )
}
