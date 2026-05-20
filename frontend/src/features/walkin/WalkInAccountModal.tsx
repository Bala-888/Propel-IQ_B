import {
  forwardRef,
  useImperativeHandle,
  useRef,
  useState,
} from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import {
  createWalkIn,
  DuplicateEmailError,
  type CreateWalkInResponse,
} from '../../api/walkInApi'

// ── Zod schema (AC-002; checklist: uses .min(1) and .email()) ────────────────────────────────────
const emailSchema = z.object({
  email: z
    .string()
    .min(1, 'Email is required to create an account')
    .email('Must be a valid email address'),
})

type EmailFormValues = z.infer<typeof emailSchema>

// ── Public handle exposed to the parent via ref ───────────────────────────────────────────────────
export interface WalkInAccountModalHandle {
  /**
   * Validate the email form then POST /walkins with createAccount=true.
   *
   * Returns the booking response on success.
   * Returns `null` when validation fails or a 409 conflict was detected (conflict view shown).
   */
  submit: (patientName: string, dateOfBirth: string) => Promise<CreateWalkInResponse | null>
}

// ── Props ─────────────────────────────────────────────────────────────────────────────────────────
interface Props {
  accessToken: string
  /**
   * Called when the link-existing flow (Yes path) or account-creation path succeeds.
   * The parent uses this to transition to WalkInConfirmation.
   */
  onLinkSuccess: (result: CreateWalkInResponse) => void
}

// ── Warning icon (UXR-105) ────────────────────────────────────────────────────────────────────────
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
 * WalkInAccountModal (MOD-004)
 *
 * Inline section that captures a patient email address for account creation.
 * Visibility is controlled by the parent (WalkInBookingForm's `createAccount` toggle).
 *
 * Internal `mode` state governs whether the email-capture or conflict view is rendered
 * so the component is never unmounted on a 409, preserving react-hook-form context for
 * the "No, use different email" reset path (checklist item 4).
 */
const WalkInAccountModal = forwardRef<WalkInAccountModalHandle, Props>(
  ({ accessToken, onLinkSuccess }, ref) => {
    const [mode, setMode] = useState<'email-capture' | 'conflict'>('email-capture')
    const [conflictUserId, setConflictUserId] = useState<number | null>(null)
    const [submitting, setSubmitting] = useState(false)
    const [apiError, setApiError] = useState<string | null>(null)

    /** Stores the most recent patient data so the link-existing path can re-use it. */
    const latestPatientDataRef = useRef<{ patientName: string; dateOfBirth: string } | null>(null)

    const {
      register,
      handleSubmit,
      formState: { errors },
      reset,
    } = useForm<EmailFormValues>({ resolver: zodResolver(emailSchema) })

    // ── Imperative handle — called by parent "Add to queue" button ──────────────────────────────
    useImperativeHandle(ref, () => ({
      submit(patientName, dateOfBirth) {
        latestPatientDataRef.current = { patientName, dateOfBirth }
        setApiError(null)

        return new Promise<CreateWalkInResponse | null>((resolve) => {
          void handleSubmit(
            async (values) => {
              setSubmitting(true)
              try {
                const result = await createWalkIn(accessToken, {
                  patientName,
                  dateOfBirth,
                  createAccount: true,
                  email: values.email,
                })
                resolve(result)
              } catch (err) {
                if (err instanceof DuplicateEmailError) {
                  setMode('conflict')
                  setConflictUserId(err.existingUserId)
                  resolve(null)
                } else {
                  setApiError(err instanceof Error ? err.message : 'Something went wrong. Please try again.')
                  resolve(null)
                }
              } finally {
                setSubmitting(false)
              }
            },
            () => {
              // email validation failed — errors are already shown by react-hook-form
              resolve(null)
            },
          )()
        })
      },
    }))

    // ── Conflict view: Yes path ───────────────────────────────────────────────────────────────────
    async function handleLinkExisting() {
      const patientData = latestPatientDataRef.current
      if (!patientData || conflictUserId == null) return

      setSubmitting(true)
      setApiError(null)
      try {
        const result = await createWalkIn(accessToken, {
          patientName: patientData.patientName,
          dateOfBirth: patientData.dateOfBirth,
          createAccount: false,
          linkExistingAccountId: conflictUserId,
        })
        onLinkSuccess(result)
      } catch (err) {
        setApiError(err instanceof Error ? err.message : 'Something went wrong. Please try again.')
      } finally {
        setSubmitting(false)
      }
    }

    // ── Conflict view: No path ────────────────────────────────────────────────────────────────────
    function handleUseNewEmail() {
      setMode('email-capture')
      setConflictUserId(null)
      setApiError(null)
      reset()
    }

    // ── Render ────────────────────────────────────────────────────────────────────────────────────
    return (
      <div className="account-section" aria-live="polite">
        {mode === 'email-capture' ? (
          <>
            <h3 className="section-subtitle">Account credentials (optional)</h3>

            <div className="form-field">
              <label htmlFor="modal-email" className="field-label">
                Email address
              </label>
              <input
                id="modal-email"
                type="email"
                autoComplete="email"
                className={`field-input${errors.email ? ' field-input--error' : ''}`}
                aria-describedby={errors.email ? 'modal-email-error' : undefined}
                aria-invalid={!!errors.email}
                disabled={submitting}
                {...register('email')}
              />
              {errors.email && (
                <span id="modal-email-error" role="alert" className="field-error">
                  <WarningIcon />
                  {errors.email.message}
                </span>
              )}
            </div>

            {apiError && (
              <div role="alert" className="field-error account-api-error">
                <WarningIcon />
                {apiError}
              </div>
            )}
          </>
        ) : (
          /* ── Conflict view (mode === 'conflict') ─────────────────────────────────────────────── */
          <div className="conflict-view" role="region" aria-label="Account conflict">
            <p className="conflict-message">
              An account with this email already exists. Would you like to link this walk-in to
              that account?
            </p>
            {apiError && (
              <div role="alert" className="field-error account-api-error">
                <WarningIcon />
                {apiError}
              </div>
            )}
            <div className="conflict-actions">
              <button
                type="button"
                className="btn btn-primary"
                onClick={() => void handleLinkExisting()}
                disabled={submitting}
              >
                {submitting ? 'Linking…' : 'Yes, link account'}
              </button>
              <button
                type="button"
                className="btn btn-secondary"
                onClick={handleUseNewEmail}
                disabled={submitting}
              >
                No, use different email
              </button>
            </div>
          </div>
        )}
      </div>
    )
  },
)

WalkInAccountModal.displayName = 'WalkInAccountModal'

export { WalkInAccountModal }
