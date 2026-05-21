import type { ReactNode } from 'react'

// ── Shared SVG icons ──────────────────────────────────────────────────────────────────────────────
// UXR-105: all error/advisory states require a named icon alongside text — colour is supplementary.

/** Error circle icon for validation error messages (UXR-105; WCAG 1.4.1). */
export function ErrorIcon() {
  return (
    <svg
      aria-hidden="true"
      width="12"
      height="12"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      style={{ flexShrink: 0 }}
    >
      <circle cx="12" cy="12" r="10" />
      <line x1="12" y1="8" x2="12" y2="12" />
      <circle cx="12" cy="16" r="0.5" fill="currentColor" />
    </svg>
  )
}

/** Warning triangle icon for non-blocking advisory messages (UXR-105; WCAG 1.4.1). */
export function WarningIcon() {
  return (
    <svg
      aria-hidden="true"
      width="12"
      height="12"
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

/** Plus icon for "Add" buttons in dynamic list fields. */
export function PlusIcon() {
  return (
    <svg aria-hidden="true" width="13" height="13" viewBox="0 0 24 24"
      fill="none" stroke="currentColor" strokeWidth="2.5"
      strokeLinecap="round" strokeLinejoin="round">
      <line x1="12" y1="5" x2="12" y2="19" />
      <line x1="5" y1="12" x2="19" y2="12" />
    </svg>
  )
}

/** Trash icon for "Remove" buttons in dynamic list fields. */
export function TrashIcon() {
  return (
    <svg aria-hidden="true" width="13" height="13" viewBox="0 0 24 24"
      fill="none" stroke="currentColor" strokeWidth="2"
      strokeLinecap="round" strokeLinejoin="round">
      <polyline points="3 6 5 6 21 6" />
      <path d="M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6" />
      <path d="M10 11v6M14 11v6" />
      <path d="M9 6V4h6v2" />
    </svg>
  )
}

// ── Shared form primitives ────────────────────────────────────────────────────────────────────────

interface FieldRowProps {
  label:    string
  required?: boolean
  hint?:    string
  children: ReactNode
}

/**
 * Labeled field container — renders the label above the input/select/textarea child.
 * Consistent padding and typography across all 5 section components.
 */
export function FieldRow({ label, required, hint, children }: FieldRowProps) {
  return (
    <div>
      <label
        style={{
          display:      'block',
          fontSize:     '12px',
          fontWeight:   600,
          color:        'var(--color-text-secondary)',
          marginBottom: 'var(--space-1)',
          textTransform: 'uppercase',
          letterSpacing: '0.4px',
        }}
      >
        {label}
        {required && (
          <span aria-hidden="true" style={{ color: 'var(--color-status-error)', marginLeft: 2 }}>*</span>
        )}
        {hint && (
          <span
            style={{
              fontSize:      '10px',
              fontWeight:    400,
              color:         'var(--color-text-disabled)',
              marginLeft:    'var(--space-2)',
              textTransform: 'none',
              letterSpacing: 0,
            }}
          >
            ({hint})
          </span>
        )}
      </label>
      {children}
    </div>
  )
}

/**
 * Validation error message rendered with icon + text (UXR-105; WCAG 1.4.1; WCAG 4.1.3).
 * Uses `role="alert"` so screen readers announce the error immediately (WCAG 4.1.3).
 */
export function ErrorMessage({ message }: { message: string }) {
  return (
    <span
      role="alert"
      style={{
        display:       'flex',
        alignItems:    'center',
        gap:           '4px',
        fontSize:      '12px',
        color:         'var(--color-status-error)',
        marginTop:     'var(--space-1)',
        fontWeight:    500,
      }}
    >
      <ErrorIcon />
      {message}
    </span>
  )
}

/**
 * Non-blocking advisory rendered with icon + text (UXR-105; WCAG 1.4.1).
 * Uses `role="status"` — advisory does not block form submission (Edge: brand-only medication).
 */
export function AdvisoryMessage({ message }: { message: string }) {
  return (
    <span
      role="status"
      style={{
        display:    'flex',
        alignItems: 'center',
        gap:        '4px',
        fontSize:   '12px',
        color:      'var(--color-status-warning)',
        marginTop:  'var(--space-1)',
        fontWeight: 500,
      }}
    >
      <WarningIcon />
      {message}
    </span>
  )
}

/** Shared style for text inputs and selects across all section components. */
export function inputStyle(hasError: boolean): React.CSSProperties {
  return {
    width:       '100%',
    fontFamily:  'var(--font-sans)',
    fontSize:    '14px',
    padding:     'var(--space-2) var(--space-3)',
    border:      `1px solid ${hasError ? 'var(--color-status-error)' : 'var(--color-border)'}`,
    borderRadius: 'var(--radius-sm)',
    outline:     'none',
    background:  'var(--color-bg-surface)',
    color:       'var(--color-text-primary)',
    boxSizing:   'border-box' as const,
  }
}

/** Small "Add …" button for dynamic list sections. */
export function AddButton({ onClick, label }: { onClick: () => void; label: string }) {
  return (
    <button
      type="button"
      onClick={onClick}
      style={{
        display:     'inline-flex',
        alignItems:  'center',
        gap:         'var(--space-1)',
        background:  'none',
        border:      '1px dashed var(--color-border-strong)',
        borderRadius: 'var(--radius-sm)',
        padding:     '5px 12px',
        cursor:      'pointer',
        fontSize:    '13px',
        fontWeight:  500,
        color:       'var(--color-text-secondary)',
        marginTop:   'var(--space-3)',
      }}
    >
      <PlusIcon />
      {label}
    </button>
  )
}

/** Small "Remove" icon button for dynamic list items. */
export function RemoveButton({ onClick }: { onClick: () => void }) {
  return (
    <button
      type="button"
      onClick={onClick}
      aria-label="Remove"
      style={{
        background:   'none',
        border:       '1px solid var(--color-border)',
        borderRadius: 'var(--radius-sm)',
        padding:      '4px 6px',
        cursor:       'pointer',
        color:        'var(--color-status-error)',
        display:      'flex',
        alignItems:   'center',
      }}
    >
      <TrashIcon />
    </button>
  )
}
