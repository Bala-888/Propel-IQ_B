import { Link, useNavigate } from 'react-router-dom'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { useAuth } from '../../context/AuthContext'
import { registrationSchema, type RegisterFormValues } from './registrationSchema'
import './RegistrationForm.css'

// UXR-105: warning icon rendered alongside every inline error message (no color-only encoding)
function WarningIcon() {
  return (
    <svg
      width="14"
      height="14"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      <path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z" />
      <line x1="12" y1="9" x2="12" y2="13" />
      <line x1="12" y1="17" x2="12.01" y2="17" />
    </svg>
  )
}

type ServerValidationErrors = Record<string, string>

export function RegistrationForm() {
  const navigate = useNavigate()
  const { setPatientName } = useAuth()

  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isValid, isSubmitting },
  } = useForm<RegisterFormValues>({
    resolver: zodResolver(registrationSchema),
    // onBlur: errors surface when a field loses focus; isValid is reactive thereafter.
    // Button starts disabled (empty required fields = invalid) and enables as fields are completed.
    mode: 'onBlur',
  })

  const onSubmit = async (values: RegisterFormValues) => {
    // Combine first + last name into the single `name` field the API expects
    const name = values.lastName.trim()
      ? `${values.firstName} ${values.lastName}`
      : values.firstName

    try {
      const res = await fetch('/api/auth/register', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          name,
          dateOfBirth: values.dateOfBirth,
          email: values.email,
          phone: values.phone,
          // Omit empty-string insurance fields so server stores NULL, not empty string (Edge: insurance optional)
          ...(values.insuranceProvider ? { insuranceProvider: values.insuranceProvider } : {}),
          ...(values.insuranceId ? { insuranceId: values.insuranceId } : {}),
        }),
      })

      if (res.status === 201) {
        // AC-005: setPatientName synchronously BEFORE navigate so header renders name
        // in the same React render cycle — satisfies the 1-second display requirement
        setPatientName(name)
        navigate('/intake')
        return
      }

      const body = (await res.json()) as {
        validationErrors?: ServerValidationErrors
        error?: string
      }

      // AC-004: map server 400 validationErrors onto form fields as inline errors
      if (res.status === 400 && body.validationErrors) {
        // Server keys are camelCase; map `name` errors to the firstName field
        const serverToFormField: Record<string, keyof RegisterFormValues> = {
          name: 'firstName',
          dateOfBirth: 'dateOfBirth',
          email: 'email',
          phone: 'phone',
          insuranceProvider: 'insuranceProvider',
          insuranceId: 'insuranceId',
        }
        for (const [key, message] of Object.entries(body.validationErrors)) {
          const field = (serverToFormField[key] ?? key) as keyof RegisterFormValues
          setError(field, { message: String(message) })
        }
      }

      // AC-002 client display: map 409 duplicate-email error onto the email field
      if (res.status === 409 && body.error) {
        setError('email', { message: body.error })
      }
    } catch {
      // Network / parse failure — surface as a form-level error (not field-specific)
      setError('root', { message: 'Unable to connect to the server. Please try again.' })
    }
  }

  return (
    <div className="auth-shell">
      <div className="auth-card">
        <div className="step-indicator" aria-label="Step 1 of 1: Registration">
          <div className="step-dot step-dot--active" />
          <span className="step-label">Patient registration</span>
        </div>

        <h1 className="auth-heading">Create your account</h1>
        <p className="auth-subheading">All fields marked with * are required.</p>

        <form noValidate onSubmit={handleSubmit(onSubmit)}>
          {/* ── Personal details ─────────────────────────────────────────────── */}
          <p className="section-title">Personal details</p>

          <div className="form-row">
            <div className="form-group" style={{ marginBottom: 0 }}>
              <label className="form-label" htmlFor="firstName">
                First name <span className="required" aria-hidden="true">*</span>
              </label>
              <input
                id="firstName"
                className={`form-input${errors.firstName ? ' error' : ''}`}
                type="text"
                autoComplete="given-name"
                placeholder="e.g. Maria"
                aria-required="true"
                aria-describedby={errors.firstName ? 'firstName-error' : undefined}
                {...register('firstName')}
              />
              {errors.firstName && (
                <span id="firstName-error" className="inline-error" role="alert">
                  <WarningIcon />
                  {errors.firstName.message}
                </span>
              )}
            </div>

            <div className="form-group" style={{ marginBottom: 0 }}>
              <label className="form-label" htmlFor="lastName">
                Last name <span className="required" aria-hidden="true">*</span>
              </label>
              <input
                id="lastName"
                className={`form-input${errors.lastName ? ' error' : ''}`}
                type="text"
                autoComplete="family-name"
                placeholder="e.g. Chen"
                aria-required="true"
                aria-describedby={errors.lastName ? 'lastName-error' : undefined}
                {...register('lastName')}
              />
              {errors.lastName && (
                <span id="lastName-error" className="inline-error" role="alert">
                  <WarningIcon />
                  {errors.lastName.message}
                </span>
              )}
            </div>
          </div>

          <div className="form-group" style={{ marginTop: 'var(--space-5)' }}>
            <label className="form-label" htmlFor="dateOfBirth">
              Date of birth <span className="required" aria-hidden="true">*</span>
            </label>
            <input
              id="dateOfBirth"
              className={`form-input${errors.dateOfBirth ? ' error' : ''}`}
              type="date"
              autoComplete="bday"
              aria-required="true"
              aria-describedby={errors.dateOfBirth ? 'dob-error' : undefined}
              style={{ maxWidth: '220px' }}
              {...register('dateOfBirth')}
            />
            {errors.dateOfBirth && (
              <span id="dob-error" className="inline-error" role="alert">
                <WarningIcon />
                {errors.dateOfBirth.message}
              </span>
            )}
          </div>

          <hr className="section-divider" />

          {/* ── Contact ──────────────────────────────────────────────────────── */}
          <p className="section-title">Contact</p>

          <div className="form-group">
            <label className="form-label" htmlFor="email">
              Email address <span className="required" aria-hidden="true">*</span>
            </label>
            <input
              id="email"
              className={`form-input${errors.email ? ' error' : ''}`}
              type="email"
              autoComplete="email"
              placeholder="e.g. maria.chen@outlook.com"
              aria-required="true"
              aria-describedby={errors.email ? 'email-error' : undefined}
              {...register('email')}
            />
            {errors.email && (
              <span id="email-error" className="inline-error" role="alert">
                <WarningIcon />
                {errors.email.message}
              </span>
            )}
          </div>

          <div className="form-group">
            <label className="form-label" htmlFor="phone">
              Phone number <span className="required" aria-hidden="true">*</span>
            </label>
            <input
              id="phone"
              className={`form-input${errors.phone ? ' error' : ''}`}
              type="tel"
              autoComplete="tel"
              placeholder="e.g. +1 (555) 740-2295"
              aria-required="true"
              aria-describedby={errors.phone ? 'phone-error' : undefined}
              {...register('phone')}
            />
            {errors.phone && (
              <span id="phone-error" className="inline-error" role="alert">
                <WarningIcon />
                {errors.phone.message}
              </span>
            )}
          </div>

          <hr className="section-divider" />

          {/* ── Insurance (optional) ─────────────────────────────────────────── */}
          <p className="section-title">Insurance</p>

          <div className="form-group">
            <label className="form-label" htmlFor="insuranceProvider">
              Insurance provider
            </label>
            <select
              id="insuranceProvider"
              className="form-select"
              autoComplete="off"
              {...register('insuranceProvider')}
            >
              <option value="">Select provider (optional)</option>
              <option value="BCBS">BCBS</option>
              <option value="Aetna PPO">Aetna PPO</option>
              <option value="Aetna HMO">Aetna HMO</option>
              <option value="United Healthcare">United Healthcare</option>
              <option value="Cigna">Cigna</option>
              <option value="Medicare Part B">Medicare Part B</option>
              <option value="Medicaid">Medicaid</option>
              <option value="Other">Other</option>
            </select>
            <span className="helper-text">
              If no match is found, a soft warning appears at booking — you can still proceed.
            </span>
          </div>

          {/* Decision[2026-05-20]: insuranceId not in wireframe but required by API spec */}
          <div className="form-group">
            <label className="form-label" htmlFor="insuranceId">
              Insurance ID
            </label>
            <input
              id="insuranceId"
              className="form-input"
              type="text"
              placeholder="e.g. BCBS-2024-0001"
              autoComplete="off"
              {...register('insuranceId')}
            />
          </div>

          {/* Form-level network / unexpected error */}
          {errors.root && (
            <span
              className="inline-error"
              role="alert"
              style={{ marginBottom: 'var(--space-4)', display: 'flex' }}
            >
              <WarningIcon />
              {errors.root.message}
            </span>
          )}

          {/* AC-004: disabled when any error is visible (isValid=false) OR submission in flight */}
          <button
            type="submit"
            className="btn btn-primary"
            disabled={!isValid || isSubmitting}
          >
            {isSubmitting ? (
              <>
                <span>Creating account…</span>
                <span className="spinner" aria-hidden="true" />
              </>
            ) : (
              'Create account'
            )}
          </button>
        </form>

        <div className="auth-footer">
          <span className="auth-footer-text">Already have an account?</span>
          <Link to="/login" className="link">Sign in</Link>
        </div>
      </div>
    </div>
  )
}
