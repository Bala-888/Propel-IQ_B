import { useEffect, useRef, useState } from 'react'
import { useAuth }                     from '../context/AuthContext'
import { resolveConflict }             from '../api/conflictsApi'
import type { ConflictDto }            from '../types/patient'
import styles                          from './ResolveConflictDrawer.module.css'

// ── ConflictAlertIcon ──────────────────────────────────────────────────────────────────────────────
// Alert circle: matches wireframe MOD-005 header icon (info-circle, stroke #DC2626)

function ConflictAlertIcon() {
  return (
    <svg
      aria-hidden="true"
      width="18"
      height="18"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2.5"
    >
      <circle cx="12" cy="12" r="10" />
      <line x1="12" y1="8" x2="12" y2="12" />
      <line x1="12" y1="16" x2="12.01" y2="16" />
    </svg>
  )
}

// ── Props ──────────────────────────────────────────────────────────────────────────────────────────

interface ResolveConflictDrawerProps {
  open:       boolean
  conflict:   ConflictDto | null
  patientId:  string
  onClose:    () => void
  onResolved: (conflictId: string) => void
}

// ── Helpers ────────────────────────────────────────────────────────────────────────────────────────

function formatConflictDesc(conflict: ConflictDto): string {
  return `${conflict.entityA.type} conflict — ${conflict.description}`
}

const MAX_NOTE = 1000

// ── ResolveConflictDrawer ──────────────────────────────────────────────────────────────────────────
/**
 * MOD-005 Resolve Conflict Drawer (us_042/AC-001, AC-002, AC-003, AC-004).
 *
 * Accessibility:
 * - `role="dialog"` + `aria-modal="true"` + `aria-labelledby="drawer-title"` (UXR-202; WCAG SC 4.1.2)
 * - Focus trap: Tab/Shift+Tab cycle within drawer; `e.preventDefault()` before manual focus
 *   prevents browser default scroll (WCAG 2.1 AA SC 2.1.2)
 * - Escape key closes drawer (UXR-202)
 * - On open: focus moves to first focusable element inside drawer
 * - On close: focus returns to the trigger element that opened the drawer
 * - Inline error `role="alert"` is always present in DOM (never conditionally mounted)
 *   so dynamic content changes are announced (WCAG SC 4.1.3)
 * - Entity separator "⬆ conflicts with ⬇" uses `aria-hidden="true"` (UXR-105; WCAG SC 1.4.1)
 */
export function ResolveConflictDrawer({
  open,
  conflict,
  patientId: _patientId,
  onClose,
  onResolved,
}: ResolveConflictDrawerProps) {
  const { accessToken } = useAuth()

  const [note,        setNote]        = useState('')
  const [loading,     setLoading]     = useState(false)
  const [inlineError, setInlineError] = useState<string>('')

  // Focus trap boundary refs
  const firstFocusRef = useRef<HTMLButtonElement>(null)
  const lastFocusRef  = useRef<HTMLButtonElement>(null)
  const drawerRef     = useRef<HTMLDivElement>(null)

  // Ref that captures the element that triggered the drawer open (for focus restoration on close)
  const triggerRef = useRef<Element | null>(null)

  // AbortController ref — cancelled if the drawer closes while a PATCH is in flight
  const abortRef = useRef<AbortController | null>(null)

  // ── Capture trigger on open; focus first element; reset form state ──────────────────────────
  useEffect(() => {
    if (open) {
      triggerRef.current = document.activeElement
      setNote('')
      setInlineError('')
      setLoading(false)
      // Allow the DOM to paint before focusing (needed if drawer was previously closed)
      requestAnimationFrame(() => {
        firstFocusRef.current?.focus()
      })
    } else {
      // Abort any in-flight PATCH request when drawer closes (AC-002, AC-003)
      abortRef.current?.abort()
      abortRef.current = null

      // Restore focus to the element that opened the drawer (UXR-202)
      if (triggerRef.current instanceof HTMLElement) {
        triggerRef.current.focus()
      }
    }
  }, [open])

  // ── Keyboard handler (focus trap + Escape) ────────────────────────────────────────────────────
  function handleKeyDown(e: React.KeyboardEvent<HTMLDivElement>) {
    if (e.key === 'Escape') {
      e.stopPropagation()
      onClose()
      return
    }

    if (e.key === 'Tab') {
      const focusableSelectors =
        'button:not([disabled]), [href], input:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'
      const focusable = Array.from(
        drawerRef.current?.querySelectorAll<HTMLElement>(focusableSelectors) ?? [],
      )
      if (focusable.length === 0) return

      const first = focusable[0]
      const last  = focusable[focusable.length - 1]

      if (e.shiftKey) {
        if (document.activeElement === first) {
          e.preventDefault()
          last.focus()
        }
      } else {
        if (document.activeElement === last) {
          e.preventDefault()
          first.focus()
        }
      }
    }
  }

  // ── Submit handler ────────────────────────────────────────────────────────────────────────────
  async function handleSubmit(resolution: 'Resolved' | 'Dismissed') {
    if (!conflict || !accessToken) return

    // Client-side validation mirrors server rules (OWASP A03 — validate at boundary)
    if (resolution === 'Resolved' && !note.trim()) return // button is disabled anyway
    if (note.length > MAX_NOTE) return // maxLength attr prevents this; guard for safety

    setInlineError('')
    setLoading(true)

    const controller = new AbortController()
    abortRef.current = controller

    try {
      await resolveConflict(
        accessToken,
        conflict.id,
        { resolution, note: note.trim() || null },
        controller.signal,
      )
      // Success: notify parent → removes conflict from SCR-014 state (AC-004)
      onResolved(conflict.id)
      onClose()
    } catch (err: unknown) {
      if (err instanceof Error && err.name === 'AbortError') return // drawer closed during request

      const error = err as Error & { status?: number }

      if (error.status === 409) {
        setInlineError('This conflict has already been resolved or dismissed.')
      } else if (error.status === 400) {
        setInlineError(error.message || 'Invalid request. Please check your input.')
      } else {
        // Network / 5xx — surface as inline message (no toast infrastructure in project)
        setInlineError('Unable to submit. Please try again.')
      }
    } finally {
      setLoading(false)
    }
  }

  // Not open and no conflict loaded — render nothing (avoid stale content flash)
  if (!open && !conflict) return null

  // Keep conflict in scope after `onClose()` so drawer can animate out with content intact
  const c = conflict

  return (
    <>
      {/* Semi-transparent overlay — click to close (UXR-202) */}
      <div
        className={styles.overlay}
        aria-hidden="true"
        onClick={onClose}
      />

      {/* Drawer panel */}
      <div
        ref={drawerRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby="drawer-title"
        aria-describedby="drawer-desc"
        className={styles.drawer}
        onKeyDown={handleKeyDown}
      >
        {/* ── Header ──────────────────────────────────────────────────────────────────── */}
        <div className={styles.drawerHeader}>
          <div>
            <h2 className={styles.drawerTitle} id="drawer-title">
              <ConflictAlertIcon />
              Resolve conflict
            </h2>
            {c && (
              <p className={styles.drawerDesc} id="drawer-desc">
                {c.entityA.type} · {formatConflictDesc(c)}
              </p>
            )}
          </div>

          {/* Close button — first focusable element for focus trap entry */}
          <button
            ref={firstFocusRef}
            className={styles.closeBtn}
            aria-label="Close conflict resolution drawer"
            onClick={onClose}
            disabled={loading}
          >
            ×
          </button>
        </div>

        {/* ── Body ────────────────────────────────────────────────────────────────────── */}
        <div className={styles.drawerBody}>

          {/* AI-flagged label — UXR label (AI References: "AI flagged this conflict" badge) */}
          <span className={styles.aiLabel} aria-label="AI flagged this conflict">
            ✦ AI flagged this conflict
          </span>

          {/* ── Conflicting entity cards (AC-001; UXR-601; UXR-105) ──────────────────── */}
          {c && (
            <div
              className={styles.conflictEntities}
              aria-label="Conflicting clinical entities"
            >
              <div
                role="group"
                aria-label={`Conflicting ${c.entityA.type}: ${c.entityA.value}`}
                className={styles.entityCard}
              >
                <div className={styles.entityLabel}>{c.entityA.type}</div>
                <div className={styles.entityName}>{c.entityA.value}</div>
              </div>

              {/* Separator — aria-hidden: icon + text used, not colour alone (UXR-105; WCAG SC 1.4.1) */}
              <div className={styles.conflictSeparator} aria-hidden="true">
                ⬆ conflicts with ⬇
              </div>

              <div
                role="group"
                aria-label={`Conflicting ${c.entityB.type}: ${c.entityB.value}`}
                className={styles.entityCard}
              >
                <div className={styles.entityLabel}>{c.entityB.type}</div>
                <div className={styles.entityName}>{c.entityB.value}</div>
              </div>
            </div>
          )}

          {/* ── Inline error (always in DOM — never conditionally removed; WCAG SC 4.1.3) ── */}
          <div
            role="alert"
            aria-live="assertive"
            className={styles.inlineError}
            style={{ display: inlineError ? undefined : 'none' }}
          >
            {inlineError}
          </div>

          {/* ── Resolution note (AC-001; UXR-601) ────────────────────────────────────── */}
          <div className={styles.formGroup}>
            <label className={styles.formLabel} htmlFor="resolution-notes">
              Resolution note{' '}
              <span className={styles.optionalHint}>
                (required for Mark Resolved)
              </span>
            </label>

            <textarea
              id="resolution-notes"
              className={styles.formTextarea}
              aria-label="Resolution note"
              aria-describedby="note-counter"
              maxLength={MAX_NOTE}
              value={note}
              onChange={e => setNote(e.target.value)}
              rows={4}
              placeholder="Enter clinical rationale or reason for dismissal…"
              disabled={loading}
            />

            <span id="note-counter" className={styles.charCounter}>
              {note.length.toLocaleString()}/{MAX_NOTE.toLocaleString()} characters
            </span>
          </div>

        </div>

        {/* ── Footer ──────────────────────────────────────────────────────────────────── */}
        <div className={styles.drawerFooter}>

          {/* Cancel */}
          <button
            className={`${styles.btn} ${styles.btnSecondary}`}
            onClick={onClose}
            disabled={loading}
          >
            Cancel
          </button>

          {/* Dismiss — note is optional for Dismissed (AC-003) */}
          <button
            className={`${styles.btn} ${styles.btnSecondary}`}
            onClick={() => handleSubmit('Dismissed')}
            disabled={loading}
            aria-busy={loading}
          >
            {loading ? 'Submitting…' : 'Dismiss'}
          </button>

          {/* Mark Resolved — note is required; button disabled when note is empty (AC-002) */}
          <button
            ref={lastFocusRef}
            className={`${styles.btn} ${styles.btnPrimary}`}
            onClick={() => handleSubmit('Resolved')}
            disabled={loading || !note.trim()}
            aria-busy={loading}
            aria-disabled={!note.trim()}
          >
            {loading ? 'Submitting…' : 'Mark Resolved'}
          </button>

        </div>
      </div>
    </>
  )
}
