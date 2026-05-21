import { useState } from 'react'
import { patchField, RequiredFieldError, type IntakeSummary } from '../../api/intakeAiApi'

export interface IntakeFieldRowProps {
  sessionId: string
  accessToken: string
  /** camelCase key matching the backend field path, e.g. "chiefComplaint" */
  fieldPath: keyof IntakeSummary
  /** Human-readable label shown above the field value */
  label: string
  /** Current field value (null = not yet collected) */
  value: string | null
  /** Callback fired on successful patch — parent updates its summary state */
  onFieldUpdated: (fieldPath: keyof IntakeSummary, newValue: string) => void
  /** Notifies parent when this row enters or exits edit mode (for confirm-button guard) */
  onEditingChange: (fieldPath: keyof IntakeSummary, isEditing: boolean) => void
}

// ── Inline SVG icons ──────────────────────────────────────────────────────────────────────────────

/** Sparkle icon for the AI confidence badge (UXR-101 — icon must accompany text label). */
function SparkleIcon() {
  return (
    <svg
      aria-hidden="true"
      width="10"
      height="10"
      viewBox="0 0 16 16"
      fill="currentColor"
    >
      <path d="M8 0l1.5 5.5L15 7l-5.5 1.5L8 16l-1.5-5.5L1 9l5.5-1.5L8 0z" />
    </svg>
  )
}

/** Pencil icon for the Edit button. */
function PencilIcon() {
  return (
    <svg
      aria-hidden="true"
      width="13"
      height="13"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7" />
      <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z" />
    </svg>
  )
}

/** Error icon for inline field validation errors (UXR-105 — icon+text, not colour-only). */
function ErrorIcon() {
  return (
    <svg
      aria-hidden="true"
      width="13"
      height="13"
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

/**
 * AI confidence badge (UXR-101; UXR-105).
 * Displays icon + text label — colour is supplementary, never the sole confidence indicator
 * (WCAG 1.4.1; UXR-105).
 */
function ConfidenceBadge() {
  return (
    <span
      style={{
        display: 'inline-flex',
        alignItems: 'center',
        gap: '3px',
        background: 'var(--color-ai-accent-surface)',
        color: 'var(--color-ai-accent)',
        fontSize: '10px',
        fontWeight: 600,
        padding: '1px 6px',
        borderRadius: 'var(--radius-full)',
        border: '1px solid #C7D2FE',
        marginLeft: 'var(--space-2)',
        verticalAlign: 'middle',
      }}
      aria-label="AI-extracted field — high confidence"
    >
      <SparkleIcon />
      <span>AI</span>
    </span>
  )
}

/**
 * A single editable intake field row with AI confidence badge (UXR-101) and inline
 * validation error on required-field violations (UXR-105; AC-002; us_016-II).
 *
 * Edit flow:
 * 1. Patient clicks the pencil Edit button → `isEditing = true`.
 * 2. An `<input>` pre-populated with the current value is shown.
 * 3. On Save: `patchField` is called.
 *    - Success → `onFieldUpdated` updates parent state; edit mode closes.
 *    - 400 `RequiredFieldError` → `<span role="alert">` shown; input reverts to prior value (UXR-105).
 * 4. On Cancel: edit mode closes with no state mutation.
 *
 * Corrected values live only in `useState` — never written to browser-persistent storage
 * (AIR guardrails; OWASP A02; HIPAA minimum-necessary; checklist).
 */
export function IntakeFieldRow({
  sessionId,
  accessToken,
  fieldPath,
  label,
  value,
  onFieldUpdated,
  onEditingChange,
}: IntakeFieldRowProps) {
  const [isEditing, setIsEditing]   = useState(false)
  const [inputValue, setInputValue] = useState(value ?? '')
  const [isSaving, setIsSaving]     = useState(false)
  const [fieldError, setFieldError] = useState<string | null>(null)

  function enterEdit() {
    setInputValue(value ?? '')
    setFieldError(null)
    setIsEditing(true)
    onEditingChange(fieldPath, true)
  }

  function cancelEdit() {
    setIsEditing(false)
    setFieldError(null)
    onEditingChange(fieldPath, false)
  }

  async function handleSave() {
    setFieldError(null)
    setIsSaving(true)
    try {
      const updated = await patchField(accessToken, sessionId, fieldPath, inputValue)
      onFieldUpdated(fieldPath, updated[fieldPath] ?? inputValue)
      setIsEditing(false)
      onEditingChange(fieldPath, false)
    } catch (err) {
      if (err instanceof RequiredFieldError) {
        // Edge: empty required field — show inline error + revert input to previous valid value (UXR-105)
        setFieldError(err.message)
        setInputValue(value ?? '')
      } else {
        setFieldError(err instanceof Error ? err.message : 'An unexpected error occurred.')
      }
    } finally {
      setIsSaving(false)
    }
  }

  function handleKeyDown(e: React.KeyboardEvent<HTMLInputElement>) {
    if (e.key === 'Enter') { e.preventDefault(); handleSave() }
    if (e.key === 'Escape') cancelEdit()
  }

  return (
    <div style={{ marginBottom: 'var(--space-5)' }}>
      {/* ── Field label row ─────────────────────────────────────────────── */}
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          marginBottom: 'var(--space-1)',
        }}
      >
        <span
          style={{
            fontSize: '11px',
            fontWeight: 600,
            color: 'var(--color-text-secondary)',
            textTransform: 'uppercase',
            letterSpacing: '0.5px',
          }}
        >
          {label}
          {/* UXR-101: confidence badge on every AI-collected field — icon+text always present */}
          {value !== null && <ConfidenceBadge />}
        </span>

        {/* Edit pencil button — hidden while already editing */}
        {!isEditing && (
          <button
            type="button"
            onClick={enterEdit}
            aria-label={`Edit ${label}`}
            style={{
              background: 'none',
              border: '1px solid var(--color-border)',
              borderRadius: 'var(--radius-sm)',
              padding: '3px 6px',
              cursor: 'pointer',
              color: 'var(--color-text-secondary)',
              display: 'flex',
              alignItems: 'center',
              gap: '3px',
              fontSize: '11px',
              fontWeight: 500,
            }}
          >
            <PencilIcon />
            Edit
          </button>
        )}
      </div>

      {/* ── Display mode ────────────────────────────────────────────────── */}
      {!isEditing && (
        <div
          style={{
            fontSize: '14px',
            color: value ? 'var(--color-text-primary)' : 'var(--color-text-disabled)',
            fontStyle: value ? 'normal' : 'italic',
            paddingBottom: 'var(--space-2)',
            borderBottom: '1px solid var(--color-border)',
          }}
        >
          {value ?? 'Not collected'}
        </div>
      )}

      {/* ── Edit mode ───────────────────────────────────────────────────── */}
      {isEditing && (
        <div>
          <input
            type="text"
            value={inputValue}
            onChange={e => setInputValue(e.target.value)}
            onKeyDown={handleKeyDown}
            disabled={isSaving}
            aria-label={`Edit value for ${label}`}
            autoFocus
            style={{
              width: '100%',
              fontFamily: 'var(--font-sans)',
              fontSize: '14px',
              padding: 'var(--space-2) var(--space-3)',
              border: `1px solid ${fieldError ? 'var(--color-status-error)' : 'var(--color-border-focus)'}`,
              borderRadius: 'var(--radius-sm)',
              outline: 'none',
              boxShadow: fieldError
                ? '0 0 0 3px rgba(220,38,38,0.15)'
                : '0 0 0 3px rgba(26,86,219,0.18)',
              marginBottom: 'var(--space-2)',
            }}
          />

          {/* Inline field error — icon+text required (UXR-105; WCAG 4.1.3; checklist) */}
          {fieldError && (
            <span
              role="alert"
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: '4px',
                fontSize: '12px',
                color: 'var(--color-status-error)',
                marginBottom: 'var(--space-2)',
              }}
            >
              <ErrorIcon />
              {fieldError}
            </span>
          )}

          <div style={{ display: 'flex', gap: 'var(--space-2)' }}>
            <button
              type="button"
              onClick={handleSave}
              disabled={isSaving}
              style={{
                background: 'var(--color-primary)',
                color: 'var(--color-text-inverse)',
                fontFamily: 'var(--font-sans)',
                fontSize: '13px',
                fontWeight: 600,
                padding: '5px 14px',
                borderRadius: 'var(--radius-sm)',
                border: 'none',
                cursor: isSaving ? 'default' : 'pointer',
                opacity: isSaving ? 0.7 : 1,
                minHeight: 32,
              }}
            >
              {isSaving ? 'Saving…' : 'Save'}
            </button>
            <button
              type="button"
              onClick={cancelEdit}
              disabled={isSaving}
              style={{
                background: 'var(--color-bg-surface)',
                color: 'var(--color-text-primary)',
                fontFamily: 'var(--font-sans)',
                fontSize: '13px',
                fontWeight: 500,
                padding: '5px 14px',
                borderRadius: 'var(--radius-sm)',
                border: '1px solid var(--color-border)',
                cursor: isSaving ? 'default' : 'pointer',
                minHeight: 32,
              }}
            >
              Cancel
            </button>
          </div>
        </div>
      )}
    </div>
  )
}
