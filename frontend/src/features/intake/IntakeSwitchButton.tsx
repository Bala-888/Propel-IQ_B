// ── IntakeSwitchButton (us_018; AC-004; UXR-105) ─────────────────────────────────────────────────

/**
 * Shared "Switch to [other mode]" button for the AI (SCR-004) and Manual (SCR-005) intake
 * toolbars (us_018; AC-004; UXR-105; WCAG 2.5.5; WCAG 4.1.2).
 *
 * Accessibility:
 * - Renders as a real `<button>` element (not `<div>`), ensuring native keyboard focusability
 *   and activation via Space/Enter without extra ARIA (WCAG 4.1.2).
 * - `aria-label` explicitly describes the target mode so screen-reader users understand the
 *   action — the visible text label is also present for sighted users (UXR-105; WCAG 2.4.6).
 * - `aria-busy` is set during the in-flight API call so assistive technology announces
 *   the loading state.
 * - Minimum 44×44 px touch target applied via inline `minHeight`/`minWidth`
 *   (WCAG 2.5.5; AC-004).
 * - Disabled state: the switch icon is replaced with a spinner icon AND the `disabled` attribute
 *   is set — the state is never communicated by colour change alone
 *   (UXR-105; WCAG 1.4.1; AC-004).
 */

// ── Icons ─────────────────────────────────────────────────────────────────────────────────────────

/** Two-arrow switch icon used in the enabled state. */
function SwitchIcon() {
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
      <path d="M7 16V4m0 0L3 8m4-4l4 4" />
      <path d="M17 8v12m0 0l4-4m-4 4l-4-4" />
    </svg>
  )
}

/** Animated spinner — shown in place of SwitchIcon while the mode-switch API call is in flight. */
function SpinnerIcon() {
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
      style={{
        flexShrink: 0,
        animation:  'intake-spin 0.9s linear infinite',
      }}
    >
      <path d="M21 12a9 9 0 1 1-6.219-8.56" />
      <style>{`@keyframes intake-spin { from { transform: rotate(0deg); } to { transform: rotate(360deg); } }`}</style>
    </svg>
  )
}

// ── Component ─────────────────────────────────────────────────────────────────────────────────────

export interface IntakeSwitchButtonProps {
  /** Target intake mode — determines the `aria-label` and visible text. */
  targetMode: 'AI' | 'Manual'
  /** Called when the button is clicked. Should initiate the mode-switch API call. */
  onClick: () => void
  /** When `true`, disables the button and shows a spinner icon (in-flight API call). */
  isLoading?: boolean
}

/**
 * "Switch to AI Chat" or "Switch to Manual Form" button used in both intake page toolbars.
 * Satisfies 44×44 px minimum touch target, icon+text label, and keyboard focusability.
 */
export function IntakeSwitchButton({ targetMode, onClick, isLoading = false }: IntakeSwitchButtonProps) {
  const label = targetMode === 'AI' ? 'Switch to AI Chat' : 'Switch to Manual Form'

  return (
    <button
      type="button"
      onClick={onClick}
      disabled={isLoading}
      aria-label={label}
      aria-busy={isLoading}
      style={{
        display:        'inline-flex',
        alignItems:     'center',
        gap:            'var(--space-2)',
        minHeight:      44,
        minWidth:       44,
        padding:        '0 var(--space-4)',
        fontFamily:     'var(--font-sans)',
        fontSize:       '13px',
        fontWeight:     600,
        color:          isLoading ? 'var(--color-text-disabled)' : 'var(--color-text-primary)',
        background:     'var(--color-bg-surface)',
        border:         '1px solid var(--color-border)',
        borderRadius:   'var(--radius-sm)',
        cursor:         isLoading ? 'default' : 'pointer',
        opacity:        isLoading ? 0.65 : 1,
        userSelect:     'none',
        flexShrink:     0,
      }}
    >
      {/* In-flight: spinner replaces switch icon — state is not communicated by colour alone
          (UXR-105; WCAG 1.4.1; AC-004) */}
      {isLoading ? <SpinnerIcon /> : <SwitchIcon />}
      <span>{isLoading ? 'Switching…' : label}</span>
    </button>
  )
}
