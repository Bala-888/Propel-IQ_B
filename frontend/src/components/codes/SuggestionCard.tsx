/**
 * SuggestionCard — SCR-015 individual AI code suggestion card (us_043/task_002; us_044/task_002).
 *
 * Accessibility:
 *   role="listitem" on the card root — parent MedicalCodePage renders role="list" (WCAG SC 1.3.1).
 *   aria-label includes code + description + status so screen readers summarise the card (WCAG SC 1.3.1).
 *   role="meter" + aria-valuenow/min/max/label on the confidence progress bar (WCAG SC 4.1.2).
 *   aria-expanded + aria-controls on the "View Evidence" toggle (WCAG SC 4.1.2; Disclosure pattern).
 *   aria-expanded on the "Correct" button signals inline edit panel state (WCAG SC 4.1.2).
 *   role="region" + aria-label on the evidence expand panel (WCAG SC 1.3.1).
 *   hidden attribute on the region when collapsed — removes element from accessibility tree (checklist 3).
 *   role="alert" on the inline error div is always in the DOM (populated/empty, never unmounted)
 *     — removing and re-adding resets the aria-live region; screen reader announcements may be missed
 *     (WCAG SC 4.1.3; checklist item 2).
 *   ⚠ Low confidence badge: Unicode U+26A0 + text "Low confidence" — neither aria-hidden;
 *     badge conveys meaning via text+icon, not colour alone (UXR-105; WCAG SC 1.4.1).
 *   Accept/Reject/Correct buttons: icon character + text label in same element (UXR-105; AC-001).
 *   OWASP A02: chunk text in the evidence panel is PHI — never sent to any logger.
 */

import { useState, useRef }      from 'react'
import styles                    from './SuggestionCard.module.css'
import type { CodeSuggestionDto } from '../../api/codeSuggestionsApi'
import type { ReviewStatus }      from '../../types/codes'

interface Props {
  suggestion:    CodeSuggestionDto
  reviewStatus:  ReviewStatus
  loading:       boolean
  inlineError:   string | null
  onAccept:      () => void
  onRejectClick: () => void   // lifts to MedicalCodePage to open the reject confirm dialog
  onCorrect:     (correctedCode: string) => void
}

// ── Client-side code format patterns (same patterns as the backend; AC-005) ──────────────────────
// Re-instantiated once per module load — not per render.
const ICD10_PATTERN = /^[A-Z][0-9]{2}(\.[0-9A-Z]{1,4})?$/
const CPT_PATTERN   = /^\d{5}$/

export function SuggestionCard({
  suggestion,
  reviewStatus,
  loading,
  inlineError,
  onAccept,
  onRejectClick,
  onCorrect,
}: Props) {
  const [evidenceOpen,    setEvidenceOpen]    = useState(false)
  const [isEditing,       setIsEditing]       = useState(false)
  const [editValue,       setEditValue]       = useState(suggestion.code)
  const [correctionError, setCorrectionError] = useState('')

  // Ref for the Correct button — focus is restored here when the edit panel is cancelled
  const correctBtnRef = useRef<HTMLButtonElement>(null)

  const pct            = Math.round(suggestion.confidence * 100)
  const codeTypeLabel  = suggestion.codeType === 'ICD10' ? 'ICD-10-CM' : 'CPT'
  const sourceFilename = suggestion.supportingChunks[0]?.sourceFilename ?? 'unknown'
  const evidenceId     = `evidence-${suggestion.id}`
  const correctHintId  = `correct-hint-${suggestion.id}`
  const statusLabel    = reviewStatus.toLowerCase()   // for aria-label
  const cardAriaLabel  = `${suggestion.code} ${suggestion.description} — ${statusLabel}`

  // Card modifier class based on review status
  const cardClass = [
    styles.card,
    reviewStatus === 'Accepted' || reviewStatus === 'Corrected' ? styles.cardAccepted : '',
    reviewStatus === 'Rejected' ? styles.cardRejected : '',
  ].filter(Boolean).join(' ')

  // Status badge modifier
  const badgeClass = [
    styles.statusBadge,
    reviewStatus === 'Accepted' || reviewStatus === 'Corrected' ? styles.statusBadgeAccepted : '',
    reviewStatus === 'Rejected' ? styles.statusBadgeRejected : '',
  ].filter(Boolean).join(' ')

  // Badge text with icon + text (UXR-105 — never colour alone; WCAG SC 1.4.1)
  const badgeContent = {
    Pending:   'Pending',
    Accepted:  '✓ Accepted',
    Rejected:  '✗ Rejected',
    Corrected: '✎ Corrected',
  }[reviewStatus] ?? reviewStatus

  function handleCancelEdit() {
    setIsEditing(false)
    setEditValue(suggestion.code)   // reset to original AI value
    setCorrectionError('')
    correctBtnRef.current?.focus()
  }

  function handleSubmitCorrection() {
    const trimmed = editValue.trim().toUpperCase()
    const pattern = suggestion.codeType === 'ICD10' ? ICD10_PATTERN : CPT_PATTERN
    if (!pattern.test(trimmed)) {
      // AC-005: do NOT clear editValue on validation failure — clinician can fix the typo
      setCorrectionError(
        suggestion.codeType === 'ICD10'
          ? 'Invalid code format. ICD-10 codes must match [A-Z][0-9]{2}(.[0-9A-Z]{1,4})? (e.g. D50.0).'
          : 'Invalid code format. CPT codes must be exactly 5 digits (e.g. 99213).',
      )
      return
    }
    setCorrectionError('')
    setIsEditing(false)
    onCorrect(trimmed)
  }

  return (
    <div
      className={cardClass}
      role="listitem"
      aria-label={cardAriaLabel}
    >

      {/* ── Card header ───────────────────────────────────────────────────────────────────── */}
      <div className={styles.cardHeader}>
        <div>
          <span className={styles.cardCode}>{suggestion.code}</span>
          <div className={styles.cardDescription}>{suggestion.description}</div>
          <div className={styles.cardMeta}>{codeTypeLabel} · Source: {sourceFilename}</div>
        </div>

        <div className={styles.cardBadges}>
          {/* UXR-402 — AI badge indicates AI provenance; never aria-hidden (WCAG SC 1.3.1) */}
          <span className={styles.aiLabel}>✦ AI</span>
          <span className={badgeClass}>{badgeContent}</span>
        </div>
      </div>

      {/* ── Confidence row ────────────────────────────────────────────────────────────────── */}
      <div className={styles.confidenceRow}>
        <span className={styles.confidenceLabel}>Confidence:</span>

        <div
          role="meter"
          aria-valuenow={pct}
          aria-valuemin={0}
          aria-valuemax={100}
          aria-label={`AI confidence ${pct}%`}
          className={styles.confidenceBar}
        >
          <div
            className={`${styles.confidenceFill} ${suggestion.confidence >= 0.7 ? styles.confHigh : styles.confMed}`}
            style={{ width: `${pct}%` }}
          />
        </div>

        <span className={styles.confidenceValue}>{suggestion.confidence.toFixed(2)}</span>

        {suggestion.lowConfidence && (
          <span className={styles.lowConfBadge}>⚠ Low confidence</span>
        )}
      </div>

      {/* ── Action buttons — rendered only when Pending (AC-001) ──────────────────────────── */}
      <div className={styles.cardActions}>

        {reviewStatus === 'Pending' && (
          <>
            {/* Accept — icon + text label (AC-001; UXR-105; min-height 44px) */}
            <button
              type="button"
              className={styles.btnAccept}
              aria-label={`Accept ${suggestion.code}`}
              disabled={loading}
              onClick={onAccept}
            >
              ✓ Accept
            </button>

            {/* Reject — icon + text label; click lifts to MedicalCodePage to open dialog (UXR-404) */}
            <button
              type="button"
              className={styles.btnReject}
              aria-label={`Reject ${suggestion.code}`}
              disabled={loading}
              onClick={onRejectClick}
            >
              ✗ Reject
            </button>

            {/* Correct — aria-expanded signals inline edit panel state (WCAG SC 4.1.2) */}
            <button
              ref={correctBtnRef}
              type="button"
              className={styles.btnSecondary}
              aria-label={`Correct ${suggestion.code}`}
              aria-expanded={isEditing}
              disabled={loading}
              onClick={() => setIsEditing(true)}
            >
              ✎ Correct
            </button>
          </>
        )}

        {/* Evidence toggle — always visible regardless of reviewStatus */}
        <button
          type="button"
          className={styles.btnSecondary}
          aria-expanded={evidenceOpen}
          aria-controls={evidenceId}
          onClick={() => setEvidenceOpen(open => !open)}
        >
          {evidenceOpen
            ? 'Hide Evidence'
            : `View Evidence (${suggestion.supportingChunks.length})`}
        </button>
      </div>

      {/* ── Reviewed state footer (accepted/corrected/rejected confirmation line) ─────────── */}
      {reviewStatus === 'Accepted' && (
        <div className={`${styles.reviewedFooter} ${styles.reviewedFooterAccepted}`}>
          ✓ Accepted
        </div>
      )}
      {reviewStatus === 'Corrected' && (
        <div className={`${styles.reviewedFooter} ${styles.reviewedFooterCorrected}`}>
          ✎ Corrected
        </div>
      )}
      {reviewStatus === 'Rejected' && (
        <div className={`${styles.reviewedFooter} ${styles.reviewedFooterRejected}`}>
          ✗ Rejected
        </div>
      )}

      {/* ── Inline correct field (always in DOM; shown/hidden via class; AC-004) ───────────── */}
      <div
        className={`${styles.correctField} ${isEditing ? styles.correctFieldOpen : ''}`}
        aria-label={`Code correction for ${suggestion.code}`}
      >
        <div className={styles.correctFieldLabel}>
          Enter corrected {codeTypeLabel} code:
        </div>
        <div className={styles.correctRow}>
          <div>
            <input
              type="text"
              className={styles.correctInput}
              value={editValue}
              onChange={e => setEditValue(e.target.value)}
              aria-label={`Corrected ${suggestion.codeType} code`}
              aria-describedby={correctHintId}
              aria-invalid={correctionError ? 'true' : undefined}
            />
            <div id={correctHintId} className={styles.correctHint}>
              {suggestion.codeType === 'ICD10'
                ? 'Format: [A-Z][0-9]{2}.[0-9A-Z]{0,4}'
                : 'Format: 5 digits'}
            </div>
            {/* Correction validation error — role="alert" for immediate SR announcement */}
            {correctionError && (
              <span role="alert" className={styles.inlineAlert}>{correctionError}</span>
            )}
          </div>
          <button type="button" className={styles.btnPrimary} onClick={handleSubmitCorrection}>
            Submit
          </button>
          <button
            type="button"
            className={styles.btnSecondary}
            aria-label="Cancel correction"
            onClick={handleCancelEdit}
          >
            Cancel
          </button>
        </div>
      </div>

      {/* ── Inline 409/400 error from API (role="alert"; always in DOM; WCAG SC 4.1.3) ─────
          The div is always rendered — never conditionally mounted — so the aria-live region
          is established before any error arrives; toggling content triggers announcement
          without the element being removed and re-added (checklist item 2). */}
      <div role="alert" className={styles.inlineAlert}>
        {inlineError ?? ''}
      </div>

      {/* ── Evidence region ───────────────────────────────────────────────────────────────── */}
      <div
        id={evidenceId}
        role="region"
        aria-label={`Supporting evidence for ${suggestion.code}`}
        className={styles.evidenceRegion}
        hidden={!evidenceOpen}
      >
        {suggestion.supportingChunks.length === 0 ? (
          <p className={styles.evidenceEmpty}>No supporting chunks available.</p>
        ) : (
          suggestion.supportingChunks.map(chunk => (
            <div key={chunk.chunkId} className={styles.evidenceChunk}>
              <div className={styles.chunkSource}>{chunk.sourceFilename}</div>
              <p className={styles.chunkText}>
                {chunk.chunkText.length > 300
                  ? `${chunk.chunkText.slice(0, 300)}…`
                  : chunk.chunkText}
              </p>
            </div>
          ))
        )}
      </div>

    </div>
  )
}
