import type { ConflictDto } from '../types/patient'
import { SeverityBadge }    from './SeverityBadge'
import styles                from './ConflictBanner.module.css'

// ── Conflict icon (wireframe pattern — info-circle with stroke #DC2626) ───────────────────────────

function ConflictIcon() {
  return (
    <svg aria-hidden="true" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="var(--color-status-error)" strokeWidth="2.5">
      <circle cx="12" cy="12" r="10" />
      <line x1="12" y1="8" x2="12" y2="12" />
      <line x1="12" y1="16" x2="12.01" y2="16" />
    </svg>
  )
}

// ── formatConflictType ────────────────────────────────────────────────────────────────────────────
// Maps backend enum values to human-readable labels (WCAG SC 3.1.1 — comprehensible labels).
// Unknown values fall through to the raw string so future types remain displayable. (checklist)

function formatConflictType(conflictType: string): string {
  switch (conflictType) {
    case 'DrugInteraction':    return 'Drug interaction detected'
    case 'DrugAllergyConflict': return 'Drug-allergy conflict detected'
    case 'DuplicateDiagnosis': return 'Duplicate diagnosis detected'
    default:                   return conflictType
  }
}

// ── Component ─────────────────────────────────────────────────────────────────────────────────────

interface ConflictBannerProps {
  conflict:   ConflictDto
  patientId:  string
  /** Called when the user clicks "Resolve conflict →". Opens MOD-005 drawer in the parent. */
  onResolve:  (conflict: ConflictDto) => void
}

/**
 * Clinical conflict banner for the SCR-014 Extracted data panel (us_041/AC-003; UXR-105).
 *
 * Accessibility:
 * - `role="alert"` + `aria-live="assertive"` — announced immediately on mount (WCAG SC 4.1.3)
 * - `aria-label` includes conflict type for screen-reader context
 * - "Resolve conflict →" button has a descriptive `aria-label` to distinguish multiple banners
 *   (WCAG 2.1 AA SC 2.4.6; checklist)
 */
export function ConflictBanner({ conflict, patientId: _patientId, onResolve }: ConflictBannerProps) {
  const title = formatConflictType(conflict.conflictType)

  return (
    <div
      role="alert"
      aria-live="assertive"
      aria-label={`Clinical conflict detected: ${title}`}
      className={styles.banner}
    >
      {/* Icon circle — aria-hidden; colour not the only indicator (UXR-105) */}
      <div className={styles.iconCircle} aria-hidden="true">
        <ConflictIcon />
      </div>

      <div className={styles.content}>
        <div className={styles.title}>{title}</div>
        <p className={styles.desc}>{conflict.description}</p>
        <SeverityBadge severity={conflict.severity} />

        <div className={styles.actions}>
          {/* Stub button — MOD-005 resolve drawer not yet implemented (checklist) */}
          <button
            aria-label={`Resolve conflict: ${title}`}
            className={styles.resolveBtn}
            onClick={() => onResolve(conflict)}
          >
            Resolve conflict →
          </button>
        </div>
      </div>
    </div>
  )
}
