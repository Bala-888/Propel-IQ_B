import { useEffect, useRef, useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { createWalkinPatient, type WalkinPatientResponse } from '../../api/walkInApi'

// ── Zod schema (AC-004; OWASP A03: validate at client boundary) ───────────────────────────────────
const schema = z.object({
  firstName:   z.string().min(1, 'First name is required').max(100),
  lastName:    z.string().min(1, 'Last name is required').max(100),
  dateOfBirth: z.string().min(1, 'Date of birth is required'),
  phoneNumber: z.string().max(30).optional(),
})

type FormValues = z.infer<typeof schema>

// ── Icons ─────────────────────────────────────────────────────────────────────────────────────────

function WarningIcon() {
  return (
    <svg aria-hidden="true" width="14" height="14" viewBox="0 0 20 20" fill="none">
      <path d="M10 3L17.794 17H2.206L10 3Z" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
      <path d="M10 9v3.5" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
      <circle cx="10" cy="14.5" r="0.75" fill="currentColor" />
    </svg>
  )
}

// ── Props ─────────────────────────────────────────────────────────────────────────────────────────

interface WalkinAccountCreationModalProps {
  isOpen:       boolean
  accessToken:  string
  /** Called when the modal is closed without creating a patient. */
  onClose:      () => void
  /** Called on successful patient creation; parent pre-fills the booking form. */
  onPatientCreated: (patient: WalkinPatientResponse) => void
}

/**
 * WalkinAccountCreationModal (MOD-004) — minimal patient record creation modal (AC-004).
 *
 * Only creates a `Patient` row (firstName, lastName, DOB, optional phone).
 * No full registration flow, intake form, or insurance record is triggered from here.
 * On success, passes the new patient to `onPatientCreated` so the parent pre-fills the form.
 */
export function WalkinAccountCreationModal({
  isOpen,
  accessToken,
  onClose,
  onPatientCreated,
}: WalkinAccountCreationModalProps) {
  const [submitting, setSubmitting] = useState(false)
  const [apiError, setApiError]     = useState<string | null>(null)
  const firstFieldRef = useRef<HTMLInputElement | null>(null)

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<FormValues>({ resolver: zodResolver(schema) })

  // Focus first field when modal opens (accessibility)
  useEffect(() => {
    if (isOpen) {
      setTimeout(() => firstFieldRef.current?.focus(), 50)
    } else {
      reset()
      setApiError(null)
    }
  }, [isOpen, reset])

  // Close on Escape
  useEffect(() => {
    if (!isOpen) return
    function handleEsc(e: KeyboardEvent) { if (e.key === 'Escape') onClose() }
    document.addEventListener('keydown', handleEsc)
    return () => document.removeEventListener('keydown', handleEsc)
  }, [isOpen, onClose])

  if (!isOpen) return null

  async function onSubmit(values: FormValues) {
    setApiError(null)
    setSubmitting(true)
    try {
      const result = await createWalkinPatient(accessToken, {
        firstName:   values.firstName.trim(),
        lastName:    values.lastName.trim(),
        dateOfBirth: values.dateOfBirth,
        phoneNumber: values.phoneNumber?.trim() || undefined,
      })
      onPatientCreated(result)
      onClose()
    } catch (err) {
      setApiError(err instanceof Error ? err.message : 'Something went wrong. Please try again.')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    /* Overlay */
    <div
      className="modal-overlay"
      role="presentation"
      aria-hidden="false"
      onClick={e => { if (e.target === e.currentTarget) onClose() }}
    >
      {/* Dialog */}
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="mod004-title"
        className="modal-dialog"
      >
        <div className="modal-header">
          <h2 id="mod004-title" className="modal-title">Create new patient</h2>
          <button
            type="button"
            className="modal-close-btn"
            aria-label="Close modal"
            onClick={onClose}
          >
            ✕
          </button>
        </div>

        <p className="modal-description">
          Creates a minimal patient record for pre-filling the walk-in booking form.
        </p>

        <form onSubmit={e => void handleSubmit(onSubmit)(e)} noValidate>
          {/* First + Last Name */}
          <div className="form-grid form-grid--2col">
            <div className="form-field">
              <label htmlFor="mod004-firstName" className="field-label">
                First name <span aria-hidden="true" className="field-required">*</span>
              </label>
              <input
                id="mod004-firstName"
                type="text"
                autoComplete="given-name"
                className={`field-input${errors.firstName ? ' field-input--error' : ''}`}
                aria-required="true"
                aria-invalid={!!errors.firstName}
                aria-describedby={errors.firstName ? 'mod004-firstName-err' : undefined}
                {...register('firstName')}
                ref={e => {
                  register('firstName').ref(e)
                  firstFieldRef.current = e
                }}
              />
              {errors.firstName && (
                <span id="mod004-firstName-err" role="alert" className="field-error">
                  <WarningIcon /> {errors.firstName.message}
                </span>
              )}
            </div>

            <div className="form-field">
              <label htmlFor="mod004-lastName" className="field-label">
                Last name <span aria-hidden="true" className="field-required">*</span>
              </label>
              <input
                id="mod004-lastName"
                type="text"
                autoComplete="family-name"
                className={`field-input${errors.lastName ? ' field-input--error' : ''}`}
                aria-required="true"
                aria-invalid={!!errors.lastName}
                aria-describedby={errors.lastName ? 'mod004-lastName-err' : undefined}
                {...register('lastName')}
              />
              {errors.lastName && (
                <span id="mod004-lastName-err" role="alert" className="field-error">
                  <WarningIcon /> {errors.lastName.message}
                </span>
              )}
            </div>
          </div>

          {/* DOB + Phone */}
          <div className="form-grid form-grid--2col">
            <div className="form-field">
              <label htmlFor="mod004-dob" className="field-label">
                Date of birth <span aria-hidden="true" className="field-required">*</span>
              </label>
              <input
                id="mod004-dob"
                type="date"
                className={`field-input${errors.dateOfBirth ? ' field-input--error' : ''}`}
                aria-required="true"
                aria-invalid={!!errors.dateOfBirth}
                aria-describedby={errors.dateOfBirth ? 'mod004-dob-err' : undefined}
                {...register('dateOfBirth')}
              />
              {errors.dateOfBirth && (
                <span id="mod004-dob-err" role="alert" className="field-error">
                  <WarningIcon /> {errors.dateOfBirth.message}
                </span>
              )}
            </div>

            <div className="form-field">
              <label htmlFor="mod004-phone" className="field-label">
                Phone <span className="field-optional">(optional)</span>
              </label>
              <input
                id="mod004-phone"
                type="tel"
                autoComplete="tel"
                className="field-input"
                placeholder="+1 (555) 000-0000"
                {...register('phoneNumber')}
              />
            </div>
          </div>

          {/* API error */}
          {apiError && (
            <div role="alert" className="alert-banner alert-banner--error">
              <WarningIcon />
              <span>{apiError}</span>
            </div>
          )}

          <div className="modal-actions">
            <button
              type="button"
              className="btn btn-secondary"
              onClick={onClose}
              disabled={submitting}
            >
              Cancel
            </button>
            <button
              type="submit"
              className="btn btn-primary"
              disabled={submitting}
              aria-disabled={submitting}
            >
              {submitting ? 'Creating…' : 'Create patient'}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}
