import type { InsuranceStatus } from '../../api/insuranceApi'

// ── Warning icon ──────────────────────────────────────────────────────────────────────────────────

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

// ── Props ─────────────────────────────────────────────────────────────────────────────────────────

interface InsuranceAlertBannerProps {
  /** Insurance status — only "Missing" or "Incomplete" are rendered; "Complete" is never passed here. */
  status: Extract<InsuranceStatus, 'Missing' | 'Incomplete'>
}

// ── Message map ───────────────────────────────────────────────────────────────────────────────────

const MESSAGES: Record<InsuranceAlertBannerProps['status'], string> = {
  Missing:    'Insurance information is missing. You can still book, but please update it before your appointment.',
  Incomplete: 'Insurance information may be incomplete.',
}

// ── Component ─────────────────────────────────────────────────────────────────────────────────────

/**
 * Soft non-blocking alert banner for MOD-002 (us_023; AC-002; UXR-604).
 *
 * - Uses `role="alert"` so assistive technologies announce the message immediately (WCAG 4.1.3).
 * - Warning is conveyed via icon + text — not by colour alone (UXR-105; WCAG 1.4.1).
 * - Does NOT disable "Confirm Booking" — the alert is advisory only (UXR-604).
 */
export function InsuranceAlertBanner({ status }: InsuranceAlertBannerProps) {
  return (
    <div
      role="alert"
      style={{
        display:       'flex',
        alignItems:    'flex-start',
        gap:           'var(--space-2)',
        background:    '#FFFBEB',
        border:        '1px solid #F59E0B',
        borderRadius:  'var(--radius-sm)',
        padding:       'var(--space-3) var(--space-4)',
        marginBottom:  'var(--space-4)',
        fontSize:      '13px',
        color:         '#92400E',
      }}
    >
      {/* Icon ensures the alert is not conveyed by colour alone (UXR-105; WCAG 1.4.1) */}
      <WarningIcon />
      <span>{MESSAGES[status]}</span>
    </div>
  )
}
