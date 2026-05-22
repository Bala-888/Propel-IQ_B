import type { SeverityLevel } from '../types/patient'
import styles from './ConflictBanner.module.css'

// ── Severity icon SVGs ────────────────────────────────────────────────────────────────────────────
// All icons aria-hidden="true" — text label is always visible (UXR-105; WCAG SC 1.4.1)

function HighIcon() {
  return (
    <svg aria-hidden="true" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5">
      <circle cx="12" cy="12" r="10" />
      <line x1="12" y1="8" x2="12" y2="12" />
      <line x1="12" y1="16" x2="12.01" y2="16" />
    </svg>
  )
}

function MediumIcon() {
  return (
    <svg aria-hidden="true" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5">
      <path d="M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z" />
      <line x1="12" y1="9" x2="12" y2="13" />
      <line x1="12" y1="17" x2="12.01" y2="17" />
    </svg>
  )
}

function LowIcon() {
  return (
    <svg aria-hidden="true" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5">
      <circle cx="12" cy="12" r="10" />
      <line x1="12" y1="16" x2="12" y2="12" />
      <line x1="12" y1="8" x2="12.01" y2="8" />
    </svg>
  )
}

// ── Severity text labels ──────────────────────────────────────────────────────────────────────────

const severityText: Record<SeverityLevel, string> = {
  High:   'High severity',
  Medium: 'Medium severity',
  Low:    'Low severity',
}

const severityClass: Record<SeverityLevel, string> = {
  High:   styles.severityHigh,
  Medium: styles.severityMedium,
  Low:    styles.severityLow,
}

// ── Component ─────────────────────────────────────────────────────────────────────────────────────

interface SeverityBadgeProps {
  severity: SeverityLevel
}

/**
 * Text + icon severity badge (UXR-105; WCAG SC 1.4.1).
 * The icon is `aria-hidden` because the text label is always visible in the DOM —
 * severity information is never conveyed by colour alone.
 */
export function SeverityBadge({ severity }: SeverityBadgeProps) {
  return (
    <span className={`${styles.severityBadge} ${severityClass[severity]}`}>
      {severity === 'High'   && <HighIcon />}
      {severity === 'Medium' && <MediumIcon />}
      {severity === 'Low'    && <LowIcon />}
      {severityText[severity]}
    </span>
  )
}
