// ── IntakeReviewBuffer (us_018; AC-003; UXR-105; WCAG 4.1.3) ─────────────────────────────────────

/**
 * Renders a notification card listing AI free-text content that could not be automatically
 * mapped to a named intake field during a mode switch (us_018; AC-003).
 *
 * Accessibility:
 * - `<div role="alert">` triggers an immediate screen-reader announcement when the component
 *   mounts with non-empty `reviewItems` (WCAG 4.1.3; WCAG 4.1.2).
 * - The notification message uses icon + text — colour is supplementary (UXR-105; WCAG 1.4.1).
 * - The wrapping `<section>` has `aria-label="Review items"` so screen-reader users can
 *   navigate to it directly (WCAG 2.4.1).
 * - Renders nothing when `reviewItems` is empty (`length === 0`) — no empty placeholder
 *   DOM is injected (AC-003 edge: no-op when list is empty).
 */

// ── Icon ─────────────────────────────────────────────────────────────────────────────────────────

/** Warning triangle — used alongside the notification text (UXR-105; WCAG 1.4.1). */
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
      style={{ flexShrink: 0, marginTop: 1 }}
    >
      <path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z" />
      <line x1="12" y1="9" x2="12" y2="13" />
      <line x1="12" y1="17" x2="12.01" y2="17" />
    </svg>
  )
}

// ── Component ─────────────────────────────────────────────────────────────────────────────────────

export interface IntakeReviewBufferProps {
  /** Items from the mode-switch response that could not be automatically mapped (AC-003). */
  reviewItems: string[]
}

/**
 * Notification card for unmapped mode-switch content.
 * Renders nothing when `reviewItems` is empty.
 * Must be placed above the tab bar in SCR-005 and above the chat window in SCR-004 (AC-003).
 */
export function IntakeReviewBuffer({ reviewItems }: IntakeReviewBufferProps) {
  if (reviewItems.length === 0) return null

  return (
    <section
      aria-label="Review items"
      style={{
        margin:       'var(--space-4) 0',
        borderRadius: 'var(--radius-sm)',
        border:       '1px solid var(--color-status-warning)',
        background:   '#FFFBEB',
        overflow:     'hidden',
      }}
    >
      {/* role="alert" announces the unmapped-content notification immediately on mount
          (WCAG 4.1.3; UXR-105) */}
      <div
        role="alert"
        style={{
          display:    'flex',
          alignItems: 'flex-start',
          gap:        'var(--space-2)',
          padding:    'var(--space-3) var(--space-4)',
          color:      'var(--color-status-warning)',
          fontSize:   '13px',
          fontWeight: 500,
          borderBottom: reviewItems.length > 0 ? '1px solid #FDE68A' : undefined,
        }}
      >
        {/* Icon + text — state is not communicated by colour alone (UXR-105; WCAG 1.4.1) */}
        <WarningIcon />
        <span>Some items could not be auto-mapped. Please review them.</span>
      </div>

      <ul
        style={{
          margin:     0,
          padding:    'var(--space-2) var(--space-4) var(--space-3) var(--space-10)',
          listStyle:  'disc',
          fontSize:   '13px',
          color:      'var(--color-text-primary)',
          lineHeight: 1.6,
        }}
      >
        {reviewItems.map((item, i) => (
          // eslint-disable-next-line react/no-array-index-key
          <li key={i}>{item}</li>
        ))}
      </ul>
    </section>
  )
}
