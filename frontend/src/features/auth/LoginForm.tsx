import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { useNavigate, useLocation, Link } from 'react-router-dom'
import { useAuth } from '../../context/AuthContext'
import { decodeJwtRole } from '../../utils/jwt'
import { loginSchema, type LoginFormValues } from './loginSchema'
import './LoginForm.css'

/**
 * Role → destination path mapping.
 * Mirrors useRoleRedirect.ts — unknown roles fall back to '/login' to prevent
 * silent access to an incorrect dashboard (AC-002, AC-003, AC-004).
 */
const ROLE_DESTINATIONS: Record<string, string> = {
  Patient: '/intake',
  Staff: '/queue',
  Admin: '/admin',
}

/** Warning icon — UXR-105: every error state must have icon + text, not color-only. */
function WarningIcon() {
  return (
    <svg
      aria-hidden="true"
      focusable="false"
      width="14"
      height="14"
      viewBox="0 0 16 16"
      fill="currentColor"
    >
      <path d="M8.982 1.566a1.13 1.13 0 0 0-1.964 0L.165 13.233c-.457.778.091 1.767.982 1.767h13.706c.9 0 1.44-.99.982-1.767L8.982 1.566zM8 5c.535 0 .954.462.9.995l-.35 3.507a.552.552 0 0 1-1.1 0L7.1 5.995A.905.905 0 0 1 8 5zm.002 6a1 1 0 1 1 0 2 1 1 0 0 1 0-2z" />
    </svg>
  )
}

/** Clock icon — session-expired info banner (AC-002). */
function ClockIcon() {
  return (
    <svg
      aria-hidden="true"
      focusable="false"
      width="14"
      height="14"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2.5"
    >
      <circle cx="12" cy="12" r="10" />
      <polyline points="12 6 12 12 16 14" />
    </svg>
  )
}

export function LoginForm() {
  const { setAuth } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  // Session-expired message injected via navigate state from AppSessionManager (AC-002).
  const sessionMessage = (location.state as { message?: string } | null)?.message ?? null

  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting, isValid },
  } = useForm<LoginFormValues>({
    resolver: zodResolver(loginSchema),
    mode: 'onBlur',
  })

  const onSubmit = async (data: LoginFormValues) => {
    try {
      const res = await fetch('/api/auth/login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email: data.email, password: data.password }),
      })

      if (!res.ok) {
        const body = await res.json().catch(() => ({}))
        const message: string =
          typeof body?.error === 'string'
            ? body.error
            : typeof body?.validationErrors === 'object'
            ? (Object.values(body.validationErrors as Record<string, string>)[0] ?? 'Login failed')
            : 'Login failed. Check your credentials and try again.'

        setError('root', { message })
        return
      }

      const body = await res.json()
      const accessToken: string = body.accessToken
      const refreshToken: string = body.refreshToken

      // Decode role from JWT payload without a third-party library (AC-002..AC-004; bundle-size hygiene)
      const role = decodeJwtRole(accessToken)

      // Store access token in React context (in-memory) — NEVER in localStorage (OWASP A02)
      setAuth(accessToken, role)

      // Store refresh token in sessionStorage — scope-limited to tab session
      sessionStorage.setItem('refreshToken', refreshToken)

      // Role-based default redirect — unknown role falls back to /login (AC-002, AC-003, AC-004)
      const dest = ROLE_DESTINATIONS[role]
      if (!dest) {
        console.warn(`[LoginForm] Unknown role received: ${role}`)
        navigate('/login', { replace: true })
        return
      }

      // Restore stored redirect only if it lives within the user's role area
      // (prevents e.g. an Admin landing on /intake because a Patient visit was cached)
      const stored = sessionStorage.getItem('redirectAfterLogin')
      sessionStorage.removeItem('redirectAfterLogin')
      if (stored && stored.startsWith(dest)) {
        navigate(stored, { replace: true })
        return
      }

      navigate(dest, { replace: true })
    } catch {
      setError('root', { message: 'Network error. Please try again.' })
    }
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', minHeight: '100vh' }}>
      <span className="wordmark">UPACIP</span>
      <div className="auth-shell">
        <div className="auth-card">
          <h1 className="auth-heading">Sign in</h1>
          <p className="auth-subheading">
            Unified Patient Access &amp; Clinical Intelligence Platform
          </p>

          {/* Session-expired banner — persistent (not a toast); displayed when navigated from timeout (AC-002) */}
          {sessionMessage && (
            <div className="alert-banner info" role="status" aria-live="polite">
              <ClockIcon />
              <span>{sessionMessage}</span>
            </div>
          )}

          {/* Root API error — role="alert" + aria-live announces to screen readers (UXR-105) */}
          {errors.root && (
            <div className="alert-banner error" role="alert" aria-live="polite">
              <WarningIcon />
              <span>{errors.root.message}</span>
            </div>
          )}

          <form onSubmit={handleSubmit(onSubmit)} noValidate>
            <div className="form-group">
              <label className="form-label" htmlFor="email">
                Email address
              </label>
              <input
                id="email"
                type="email"
                autoComplete="username"
                placeholder="e.g. sarah.mitchell@email.com"
                aria-required="true"
                aria-describedby={errors.email ? 'email-error' : undefined}
                aria-invalid={errors.email ? 'true' : undefined}
                className={`form-input${errors.email ? ' error' : ''}`}
                {...register('email')}
              />
              {errors.email && (
                <span className="inline-error" id="email-error" role="alert">
                  <WarningIcon />
                  {errors.email.message}
                </span>
              )}
            </div>

            <div className="form-group no-mb">
              <label className="form-label" htmlFor="password">
                Password
              </label>
              <input
                id="password"
                type="password"
                autoComplete="current-password"
                aria-required="true"
                aria-describedby={errors.password ? 'password-error' : undefined}
                aria-invalid={errors.password ? 'true' : undefined}
                className={`form-input${errors.password ? ' error' : ''}`}
                {...register('password')}
              />
              {errors.password && (
                <span className="inline-error" id="password-error" role="alert">
                  <WarningIcon />
                  {errors.password.message}
                </span>
              )}
            </div>

            <div className="forgot-row">
              <a href="#" className="link" onClick={(e) => e.preventDefault()}>
                Forgot password?
              </a>
            </div>

            <button
              type="submit"
              className="btn btn-primary"
              disabled={isSubmitting}
              aria-label="Sign in"
            >
              {isSubmitting ? (
                <>
                  <span className="spinner" aria-hidden="true" />
                  Signing in…
                </>
              ) : (
                'Sign in'
              )}
            </button>
          </form>

          <div className="auth-footer">
            <span style={{ fontSize: '14px', color: 'var(--color-text-secondary)' }}>
              New patient?
            </span>
            <Link to="/register" className="link">
              Create an account
            </Link>
          </div>
        </div>
      </div>
    </div>
  )
}

