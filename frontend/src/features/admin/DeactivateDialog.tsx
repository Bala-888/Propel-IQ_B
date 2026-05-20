/**
 * Confirmation dialog for deactivating or reactivating a user account.
 * `targetState = false` → Deactivate flow (destructive, logged in audit trail).
 * `targetState = true`  → Reactivate flow.
 *
 * AC-003: admin confirms before deactivation; action is logged by the backend.
 * AC-004 (own-account guard) is enforced by the caller — this dialog is never opened
 * for the currently authenticated user.
 */
import { useEffect, useRef, useState } from 'react'
import './DeactivateDialog.css'

interface DeactivateDialogProps {
  userName: string
  targetState: boolean   // false = deactivate, true = reactivate
  onConfirm: () => Promise<void>
  onCancel: () => void
}

export function DeactivateDialog({ userName, targetState, onConfirm, onCancel }: DeactivateDialogProps) {
  const [submitting, setSubmitting] = useState(false)
  const dialogRef = useRef<HTMLDivElement>(null)
  const confirmBtnRef = useRef<HTMLButtonElement>(null)

  const isDeactivate = !targetState
  const titleText = isDeactivate
    ? `Deactivate ${userName}?`
    : `Activate ${userName}?`
  const bodyText = isDeactivate
    ? 'This user will lose access to the platform immediately. They can be reactivated later. This action is logged in the audit trail.'
    : 'This user will regain access to the platform. This action is logged in the audit trail.'
  const confirmLabel = isDeactivate ? 'Deactivate user' : 'Activate user'

  // Focus confirm button on open
  useEffect(() => {
    confirmBtnRef.current?.focus()
  }, [])

  // Close on Escape; trap Tab
  useEffect(() => {
    function onKeyDown(e: KeyboardEvent) {
      if (e.key === 'Escape') { onCancel(); return }
      if (e.key !== 'Tab' || !dialogRef.current) return

      const focusable = dialogRef.current.querySelectorAll<HTMLElement>(
        'button:not([disabled])',
      )
      const first = focusable[0]
      const last = focusable[focusable.length - 1]

      if (e.shiftKey) {
        if (document.activeElement === first) { e.preventDefault(); last?.focus() }
      } else {
        if (document.activeElement === last) { e.preventDefault(); first?.focus() }
      }
    }
    document.addEventListener('keydown', onKeyDown)
    return () => document.removeEventListener('keydown', onKeyDown)
  }, [onCancel])

  async function handleConfirm() {
    setSubmitting(true)
    try {
      await onConfirm()
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div
      className="dd-overlay"
      role="dialog"
      aria-modal="true"
      aria-labelledby="dd-title"
      aria-describedby="dd-body"
      onClick={(e) => { if (e.target === e.currentTarget) onCancel() }}
    >
      <div className="dd-card" ref={dialogRef}>
        <h2 className="dd-card__title" id="dd-title">{titleText}</h2>
        <p className="dd-card__body" id="dd-body">{bodyText}</p>
        <div className="dd-card__actions">
          <button type="button" className="dd-btn dd-btn--secondary" onClick={onCancel}>
            Cancel
          </button>
          <button
            type="button"
            ref={confirmBtnRef}
            className={`dd-btn ${isDeactivate ? 'dd-btn--destructive' : 'dd-btn--primary'}`}
            disabled={submitting}
            /* eslint-disable-next-line @typescript-eslint/no-misused-promises */
            onClick={handleConfirm}
          >
            {submitting ? 'Please wait…' : confirmLabel}
          </button>
        </div>
      </div>
    </div>
  )
}
