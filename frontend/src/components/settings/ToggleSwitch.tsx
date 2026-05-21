/**
 * Reusable accessible toggle switch (us_029; UXR-105; WCAG 2.1).
 *
 * Renders a `<button role="switch" aria-checked>` so the toggled state is announced to
 * screen readers. A visible `<span aria-hidden="true">On / Off</span>` text label ensures
 * the state is communicated by text — never by colour alone (UXR-105; WCAG 1.4.1 Use of Color).
 * The button is `disabled` while `isLoading` is true so only the in-flight toggle is blocked;
 * sibling toggles remain fully interactive (AC-002 — per-field loading isolation).
 */

import type { CSSProperties } from 'react'

// ── Types ────────────────────────────────────────────────────────────────────────────────────────

export interface ToggleSwitchProps {
  /** Visible label text shown to the left of the toggle. */
  label:     string
  /** Optional secondary description below the label. */
  description?: string
  /** Controlled checked state. */
  checked:   boolean
  /** Called with the new boolean value when the user clicks the toggle. */
  onChange:  (value: boolean) => void
  /** When true the button is disabled (in-flight PATCH for this specific field). */
  isLoading: boolean
  /** Unique id used for `aria-labelledby` linkage. */
  id:        string
}

// ── Styles ───────────────────────────────────────────────────────────────────────────────────────

const rowStyle: CSSProperties = {
  display:        'flex',
  alignItems:     'center',
  justifyContent: 'space-between',
  padding:        'var(--space-4) 0',
  borderBottom:   '1px solid var(--color-border)',
}

const infoStyle: CSSProperties = { flex: 1 }

const labelTextStyle: CSSProperties = {
  fontSize:   '14px',
  fontWeight: 500,
  marginBottom: '2px',
}

const descStyle: CSSProperties = {
  fontSize: '13px',
  color:    'var(--color-text-secondary)',
}

const controlStyle: CSSProperties = {
  display:    'flex',
  alignItems: 'center',
  gap:        'var(--space-2)',
  marginLeft: 'var(--space-4)',
  flexShrink: 0,
}

const statusTextStyle = (checked: boolean): CSSProperties => ({
  fontSize:   '13px',
  fontWeight: 500,
  minWidth:   '24px',
  color:      checked ? 'var(--color-primary)' : 'var(--color-text-secondary)',
})

const trackStyle = (checked: boolean, disabled: boolean): CSSProperties => ({
  position:     'relative',
  width:        '44px',
  height:       '24px',
  borderRadius: '9999px',
  background:   checked ? 'var(--color-primary)' : 'var(--color-border-strong)',
  border:       'none',
  cursor:       disabled ? 'not-allowed' : 'pointer',
  opacity:      disabled ? 0.6 : 1,
  transition:   'background 0.2s',
  flexShrink:   0,
  padding:      0,
  // Focus ring
  outline:      'none',
})

const thumbStyle = (checked: boolean): CSSProperties => ({
  position:     'absolute',
  width:        '18px',
  height:       '18px',
  top:          '3px',
  left:         checked ? '23px' : '3px',
  background:   '#FFFFFF',
  borderRadius: '50%',
  transition:   'left 0.2s',
  pointerEvents: 'none',
})

// ── Component ────────────────────────────────────────────────────────────────────────────────────

export function ToggleSwitch({
  label,
  description,
  checked,
  onChange,
  isLoading,
  id,
}: ToggleSwitchProps) {
  return (
    <div style={rowStyle}>
      {/* Label + optional description */}
      <div style={infoStyle} id={`${id}-label`}>
        <div style={labelTextStyle}>{label}</div>
        {description && <div style={descStyle}>{description}</div>}
      </div>

      {/* Toggle control: status text + button */}
      <div style={controlStyle}>
        {/* UXR-105: visible "On"/"Off" text — state is not communicated by colour alone.
            aria-hidden so screen readers rely on aria-checked, not duplicated text. */}
        <span aria-hidden="true" style={statusTextStyle(checked)}>
          {checked ? 'On' : 'Off'}
        </span>

        {/* WCAG 2.1 / ARIA APG Switch pattern:
            role="switch" + aria-checked conveys toggle semantics to assistive tech.
            aria-labelledby links to the visible label text above.
            disabled while isLoading to block double-clicks during the in-flight PATCH. */}
        <button
          role="switch"
          id={id}
          aria-checked={checked}
          aria-labelledby={`${id}-label`}
          aria-label={label}
          disabled={isLoading}
          onClick={() => onChange(!checked)}
          style={trackStyle(checked, isLoading)}
        >
          <span style={thumbStyle(checked)} />
        </button>

        {/* Loading indicator — visible spinner replaces the "On"/"Off" text while saving */}
        {isLoading && (
          <span
            role="status"
            aria-label="Saving…"
            style={{ fontSize: '12px', color: 'var(--color-text-secondary)' }}
          >
            …
          </span>
        )}
      </div>
    </div>
  )
}
