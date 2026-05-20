import { useEffect, useRef, useState } from 'react'
import { useAuth } from '../../context/AuthContext'
import { decodeJwtRole } from '../../utils/jwt'
import './SessionTimeoutModal.css'

interface SessionTimeoutModalProps {
  /** Whether the modal is visible. */
  isOpen: boolean
  /**
   * Called after a successful token refresh: close the modal and reset the inactivity timer.
   * Called AFTER auth state is updated and the BroadcastChannel message has been posted.
   */
  onExtend: () => void
  /**
   * Called when the countdown reaches zero or when "Sign out now" is clicked.
   * Parent clears auth state and navigates to /login (AC-002).
   */
  onLogout: () => void
}

const BROADCAST_CHANNEL_NAME = 'upacip-session'
const COUNTDOWN_SECONDS = 60
const CRITICAL_THRESHOLD = 10

/** Clock SVG — matches the MOD-001 wireframe icon. */
function ClockIcon() {
  return (
    <svg
      width="28"
      height="28"
      viewBox="0 0 24 24"
      fill="none"
      stroke="var(--color-status-warning)"
      strokeWidth="2.5"
      aria-hidden="true"
      focusable="false"
    >
      <circle cx="12" cy="12" r="10" />
      <polyline points="12 6 12 12 16 14" />
    </svg>
  )
}

/**
 * Session Timeout Warning Modal (MOD-001).
 *
 * Shows when the user has been inactive for 14 minutes (AC-001; UXR-202).
 * Displays a 60-second countdown to forced sign-out (UXR-202).
 * "Stay signed in" calls POST /api/auth/refresh, updates AuthContext, broadcasts
 * SESSION_EXTENDED to all tabs (AC-003; Edge: multiple tabs).
 * Countdown reaching zero calls onLogout() — same path as "Sign out now" (AC-002).
 * Focus trap keeps keyboard navigation inside the modal while open (UXR-202).
 */
export function SessionTimeoutModal({ isOpen, onExtend, onLogout }: SessionTimeoutModalProps) {
  const { setAuth } = useAuth()
  const [secondsLeft, setSecondsLeft] = useState(COUNTDOWN_SECONDS)
  const [isExtending, setIsExtending] = useState(false)
  const [extendError, setExtendError] = useState<string | null>(null)

  // Refs ensure stale closures do not capture outdated callbacks in the interval/effect.
  const onLogoutRef = useRef(onLogout)
  const onExtendRef = useRef(onExtend)
  useEffect(() => { onLogoutRef.current = onLogout }, [onLogout])
  useEffect(() => { onExtendRef.current = onExtend }, [onExtend])

  // Countdown — resets when modal opens; fires onLogout at 0 (AC-001; UXR-202).
  useEffect(() => {
    if (!isOpen) {
      setSecondsLeft(COUNTDOWN_SECONDS)
      setExtendError(null)
      return
    }

    setSecondsLeft(COUNTDOWN_SECONDS)
    setExtendError(null)

    const interval = setInterval(() => {
      setSecondsLeft((s) => {
        if (s <= 1) {
          clearInterval(interval)
          onLogoutRef.current()
          return 0
        }
        return s - 1
      })
    }, 1000)

    return () => clearInterval(interval)
  }, [isOpen])

  // Focus trap — keeps Tab / Shift+Tab inside the modal (UXR-202; keyboard accessibility).
  const modalRef = useRef<HTMLDivElement>(null)
  useEffect(() => {
    if (!isOpen || !modalRef.current) return

    const focusable = modalRef.current.querySelectorAll<HTMLElement>(
      'button,[tabindex]:not([tabindex="-1"])',
    )
    const first = focusable[0]
    const last = focusable[focusable.length - 1]
    first?.focus()

    function handleKeyDown(e: KeyboardEvent) {
      if (e.key !== 'Tab') return
      if (e.shiftKey) {
        if (document.activeElement === first) { e.preventDefault(); last?.focus() }
      } else {
        if (document.activeElement === last) { e.preventDefault(); first?.focus() }
      }
    }

    document.addEventListener('keydown', handleKeyDown)
    return () => document.removeEventListener('keydown', handleKeyDown)
  }, [isOpen])

  /**
   * "Stay signed in" handler (AC-003):
   * 1. POST /api/auth/refresh with current refresh token from sessionStorage.
   * 2. Update AuthContext with new access token + role.
   * 3. Broadcast SESSION_EXTENDED BEFORE calling onExtend so other tabs receive
   *    the message before this tab's modal closes and timer resets.
   * 4. Call onExtend() — parent closes modal and resets inactivity timer.
   */
  async function handleStaySignedIn() {
    const refreshToken = sessionStorage.getItem('refreshToken')
    if (!refreshToken) {
      onLogoutRef.current()
      return
    }

    setIsExtending(true)
    setExtendError(null)

    try {
      const res = await fetch('/api/auth/refresh', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken }),
      })

      if (!res.ok) throw new Error('Refresh failed')

      const body = await res.json()
      const newAccessToken: string = body.accessToken
      const newRefreshToken: string = body.refreshToken
      const role = decodeJwtRole(newAccessToken)

      setAuth(newAccessToken, role)
      sessionStorage.setItem('refreshToken', newRefreshToken)

      // Broadcast BEFORE onExtend() — ensures other tabs receive the signal
      // before this tab's timer resets and modal closes (Edge: multiple tabs).
      const channel = new BroadcastChannel(BROADCAST_CHANNEL_NAME)
      channel.postMessage({ type: 'SESSION_EXTENDED' })
      channel.close()

      onExtendRef.current()
    } catch {
      setExtendError('Unable to extend your session. Please sign in again.')
      setIsExtending(false)
    }
  }

  function formatCountdown(s: number): string {
    const m = Math.floor(s / 60)
    const sec = s % 60
    return `${m}:${sec.toString().padStart(2, '0')}`
  }

  if (!isOpen) return null

  return (
    <div
      className="session-overlay"
      role="dialog"
      aria-modal="true"
      aria-labelledby="session-title"
      aria-describedby="session-desc"
    >
      <div className="session-modal" ref={modalRef}>
        <div className="session-modal__icon">
          <ClockIcon />
        </div>

        <h2 className="session-modal__title" id="session-title">
          Your session is about to expire
        </h2>

        <p className="session-modal__body" id="session-desc">
          For your security, you will be automatically signed out in
        </p>

        <div
          className={`session-modal__timer${secondsLeft <= CRITICAL_THRESHOLD ? ' session-modal__timer--critical' : ''}`}
          role="timer"
          aria-label="Time remaining before session expires"
          aria-live="polite"
        >
          {formatCountdown(secondsLeft)}
        </div>

        <p className="session-modal__timer-label">seconds remaining</p>

        {extendError && (
          <p className="session-modal__error" role="alert">
            {extendError}
          </p>
        )}

        <button
          className="session-modal__btn session-modal__btn--primary"
          onClick={() => { void handleStaySignedIn() }}
          disabled={isExtending}
          // eslint-disable-next-line jsx-a11y/no-autofocus
          autoFocus
        >
          {isExtending ? 'Extending…' : 'Stay signed in'}
        </button>

        <button
          className="session-modal__btn session-modal__btn--secondary"
          onClick={onLogout}
        >
          Sign out now
        </button>

        <p className="session-modal__footer">
          Your work is saved. You can sign back in at any time.
        </p>
      </div>
    </div>
  )
}
