import { useRef, useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../../context/AuthContext'
import { createWalkIn, type CreateWalkInResponse } from '../../api/walkInApi'
import {
  WalkInAccountModal,
  type WalkInAccountModalHandle,
} from './WalkInAccountModal'
import { WalkInConfirmation } from './WalkInConfirmation'
import './walkin.css'

// ── Zod schema ────────────────────────────────────────────────────────────────────────────────────
const schema = z.object({
  firstName: z.string().min(1, 'First name is required'),
  lastName: z.string().min(1, 'Last name is required'),
  dateOfBirth: z.string().min(1, 'Date of birth is required'),
})

type FormValues = z.infer<typeof schema>

// ── Warning icon ──────────────────────────────────────────────────────────────────────────────────
function WarningIcon() {
  return (
    <svg
      aria-hidden="true"
      width="16"
      height="16"
      viewBox="0 0 20 20"
      fill="none"
      xmlns="http://www.w3.org/2000/svg"
      className="field-error-icon"
    >
      <path
        d="M10 3L17.794 17H2.206L10 3Z"
        stroke="currentColor"
        strokeWidth="1.5"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
      <path d="M10 9v3.5" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
      <circle cx="10" cy="14.5" r="0.75" fill="currentColor" />
    </svg>
  )
}

/**
 * WalkInBookingForm (SCR-012)
 *
 * Allows front-desk staff to register a walk-in patient and optionally create or link
 * a patient portal account.
 *
 * Submission paths:
 * - Toggle OFF  → direct booking-only POST (AC-001)
 * - Toggle ON   → account-creation POST via WalkInAccountModal (AC-002 / AC-004)
 */
export function WalkInBookingForm() {
  const { accessToken } = useAuth()
  const navigate = useNavigate()

  const [createAccount, setCreateAccount] = useState(false)
  const [submitting, setSubmitting] = useState(false)
  const [apiError, setApiError] = useState<string | null>(null)
  const [bookingResult, setBookingResult] = useState<CreateWalkInResponse | null>(null)

  const accountModalRef = useRef<WalkInAccountModalHandle>(null)

  const {
    register,
    trigger,
    getValues,
    formState: { errors },
  } = useForm<FormValues>({ resolver: zodResolver(schema) })

  // ── Submit logic ──────────────────────────────────────────────────────────────────────────────

  async function handleAddToQueue(e: React.FormEvent) {
    e.preventDefault()
    setApiError(null)

    // Validate main form fields
    const mainValid = await trigger(['firstName', 'lastName', 'dateOfBirth'])
    if (!mainValid) return

    const values = getValues()
    const patientName = `${values.firstName.trim()} ${values.lastName.trim()}`
    const dateOfBirth = values.dateOfBirth

    if (createAccount) {
      // Delegate to WalkInAccountModal which validates email and calls the API.
      // Returns null when email validation fails or a 409 conflict was raised (conflict view shown).
      const result = await accountModalRef.current?.submit(patientName, dateOfBirth)
      if (result) setBookingResult(result)
      return
    }

    // Booking-only path (AC-001)
    setSubmitting(true)
    try {
      const result = await createWalkIn(accessToken!, {
        patientName,
        dateOfBirth,
        createAccount: false,
      })
      setBookingResult(result)
    } catch (err) {
      setApiError(err instanceof Error ? err.message : 'Something went wrong. Please try again.')
    } finally {
      setSubmitting(false)
    }
  }

  // ── After successful booking/link ─────────────────────────────────────────────────────────────
  if (bookingResult) {
    return <WalkInConfirmation booking={bookingResult} onAddAnother={() => navigate(0)} />
  }

  // ── Render ────────────────────────────────────────────────────────────────────────────────────
  return (
    <div className="walkin-page">
      <div className="walkin-page__sidebar">
        <h2 className="walkin-page__sidebar-title">Walk-in Queue</h2>
        <nav aria-label="Queue navigation">
          <a href="/queue" className="walkin-page__nav-link">
            ← Back to queue
          </a>
        </nav>
      </div>

      <main className="walkin-page__content" aria-labelledby="walkin-form-heading">
        <div className="walkin-form-card">
          <h1 id="walkin-form-heading" className="walkin-form-card__title">
            Add walk-in patient
          </h1>

          <form onSubmit={(e) => void handleAddToQueue(e)} noValidate>
            {/* ── Patient information ─────────────────────────────────────────────────────── */}
            <section aria-labelledby="patient-info-heading">
              <h2 id="patient-info-heading" className="section-heading">
                Patient information
              </h2>

              <div className="form-grid form-grid--2col">
                {/* First name */}
                <div className="form-field">
                  <label htmlFor="firstName" className="field-label">
                    First name <span aria-hidden="true" className="field-required">*</span>
                  </label>
                  <input
                    id="firstName"
                    type="text"
                    autoComplete="given-name"
                    className={`field-input${errors.firstName ? ' field-input--error' : ''}`}
                    aria-required="true"
                    aria-describedby={errors.firstName ? 'firstName-error' : undefined}
                    aria-invalid={!!errors.firstName}
                    {...register('firstName')}
                  />
                  {errors.firstName && (
                    <span id="firstName-error" role="alert" className="field-error">
                      <WarningIcon />
                      {errors.firstName.message}
                    </span>
                  )}
                </div>

                {/* Last name */}
                <div className="form-field">
                  <label htmlFor="lastName" className="field-label">
                    Last name <span aria-hidden="true" className="field-required">*</span>
                  </label>
                  <input
                    id="lastName"
                    type="text"
                    autoComplete="family-name"
                    className={`field-input${errors.lastName ? ' field-input--error' : ''}`}
                    aria-required="true"
                    aria-describedby={errors.lastName ? 'lastName-error' : undefined}
                    aria-invalid={!!errors.lastName}
                    {...register('lastName')}
                  />
                  {errors.lastName && (
                    <span id="lastName-error" role="alert" className="field-error">
                      <WarningIcon />
                      {errors.lastName.message}
                    </span>
                  )}
                </div>
              </div>

              {/* DOB row */}
              <div className="form-grid form-grid--2col">
                <div className="form-field">
                  <label htmlFor="dateOfBirth" className="field-label">
                    Date of birth <span aria-hidden="true" className="field-required">*</span>
                  </label>
                  <input
                    id="dateOfBirth"
                    type="date"
                    className={`field-input${errors.dateOfBirth ? ' field-input--error' : ''}`}
                    aria-required="true"
                    aria-describedby={errors.dateOfBirth ? 'dateOfBirth-error' : undefined}
                    aria-invalid={!!errors.dateOfBirth}
                    {...register('dateOfBirth')}
                  />
                  {errors.dateOfBirth && (
                    <span id="dateOfBirth-error" role="alert" className="field-error">
                      <WarningIcon />
                      {errors.dateOfBirth.message}
                    </span>
                  )}
                </div>

                {/* Phone — UI only, not sent to current API version */}
                <div className="form-field">
                  <label htmlFor="phone" className="field-label">
                    Phone <span className="field-optional">(optional)</span>
                  </label>
                  <input
                    id="phone"
                    type="tel"
                    autoComplete="tel"
                    className="field-input"
                  />
                </div>
              </div>

              {/* Insurance — UI only */}
              <div className="form-field">
                <label htmlFor="insurance" className="field-label">
                  Insurance <span className="field-optional">(optional)</span>
                </label>
                <select id="insurance" className="field-input field-select">
                  <option value="">Select insurance…</option>
                  <option value="medicare">Medicare</option>
                  <option value="medicaid">Medicaid</option>
                  <option value="bcbs">Blue Cross Blue Shield</option>
                  <option value="aetna">Aetna</option>
                  <option value="unitedhealthcare">UnitedHealthcare</option>
                  <option value="cigna">Cigna</option>
                  <option value="self-pay">Self-pay</option>
                  <option value="other">Other</option>
                </select>
              </div>

              {/* Chief complaint — UI only */}
              <div className="form-field">
                <label htmlFor="chiefComplaint" className="field-label">
                  Chief complaint <span className="field-optional">(optional)</span>
                </label>
                <textarea
                  id="chiefComplaint"
                  rows={3}
                  className="field-input field-textarea"
                  placeholder="Describe the patient's reason for visit…"
                />
              </div>

              {/* Time slot — UI only */}
              <div className="form-field">
                <label htmlFor="timeSlot" className="field-label">
                  Time slot <span className="field-optional">(optional)</span>
                </label>
                <select id="timeSlot" className="field-input field-select">
                  <option value="">Select time slot…</option>
                  <option value="8:00">8:00 AM</option>
                  <option value="8:30">8:30 AM</option>
                  <option value="9:00">9:00 AM</option>
                  <option value="9:30">9:30 AM</option>
                  <option value="10:00">10:00 AM</option>
                  <option value="10:30">10:30 AM</option>
                  <option value="11:00">11:00 AM</option>
                  <option value="11:30">11:30 AM</option>
                  <option value="13:00">1:00 PM</option>
                  <option value="13:30">1:30 PM</option>
                  <option value="14:00">2:00 PM</option>
                  <option value="14:30">2:30 PM</option>
                  <option value="15:00">3:00 PM</option>
                  <option value="15:30">3:30 PM</option>
                  <option value="16:00">4:00 PM</option>
                  <option value="16:30">4:30 PM</option>
                </select>
              </div>
            </section>

            <hr className="section-divider" />

            {/* ── Account creation toggle ─────────────────────────────────────────────────── */}
            <div className="check-row">
              <input
                id="create-account-toggle"
                type="checkbox"
                className="check-input"
                checked={createAccount}
                onChange={(e) => setCreateAccount(e.target.checked)}
              />
              <label htmlFor="create-account-toggle" className="check-label">
                Create a patient portal account
              </label>
            </div>

            {/* WalkInAccountModal: visibility controlled by parent toggle state (checklist item 1) */}
            {createAccount && (
              <WalkInAccountModal
                ref={accountModalRef}
                accessToken={accessToken!}
                onLinkSuccess={setBookingResult}
              />
            )}

            {/* Global API error (booking-only path) */}
            {apiError && (
              <div role="alert" className="form-api-error">
                <WarningIcon />
                {apiError}
              </div>
            )}

            {/* ── Form actions ────────────────────────────────────────────────────────────── */}
            <div className="form-actions">
              <a href="/queue" className="btn btn-secondary">
                Cancel
              </a>
              <button
                type="submit"
                className="btn btn-primary"
                disabled={submitting}
              >
                {submitting ? 'Adding…' : 'Add to queue'}
              </button>
            </div>
          </form>
        </div>
      </main>
    </div>
  )
}
