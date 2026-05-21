import { useRef, useState, useEffect } from 'react'
import { useNavigate, useLocation } from 'react-router-dom'
import { useAuth } from '../../context/AuthContext'
import {
  startSession,
  sendMessage,
  AiUnavailableError,
  AiTimeoutError,
  type ChatMessage,
  type IntakeSummary,
} from '../../api/intakeAiApi'
import { switchMode, type ModeSwitchResponse } from '../../api/intakeModeSwitchApi'
import { ChatMessageList } from './ChatMessageList'
import { IntakeSummaryPanel } from './IntakeSummaryPanel'
import { IntakeSwitchButton } from './IntakeSwitchButton'
import { IntakeReviewBuffer } from './IntakeReviewBuffer'

// ── Inline SVG icons ─────────────────────────────────────────────────────────────────────────────

/** Error icon for the 503 unavailable banner (UXR-105 — icon+text, not colour-only). */
function ErrorIcon() {
  return (
    <svg
      aria-hidden="true"
      width="18"
      height="18"
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

/** Clock icon for the timeout retry message (UXR-105 — icon+text, not colour-only). */
function ClockIcon() {
  return (
    <svg
      aria-hidden="true"
      width="16"
      height="16"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      style={{ flexShrink: 0 }}
    >
      <circle cx="12" cy="12" r="10" />
      <polyline points="12 6 12 12 16 14" />
    </svg>
  )
}

/** Send arrow icon for the Send button. */
function SendIcon() {
  return (
    <svg
      aria-hidden="true"
      width="16"
      height="16"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      <line x1="22" y1="2" x2="11" y2="13" />
      <polygon points="22 2 15 22 11 13 2 9 22 2" />
    </svg>
  )
}

// ── Helpers ───────────────────────────────────────────────────────────────────────────────────────

function newId(): string {
  return `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`
}

// ── AiIntakePage (SCR-004) ───────────────────────────────────────────────────────────────────────

/**
 * SCR-004 — AI Conversational Intake page.
 *
 * State machine:
 * - `idle`: pre-session; shows the "Start AI Intake" intro panel.
 * - `loading`: waiting for the first AI question from `POST /intake/ai/start`.
 * - `active`: session running; chat input is enabled.
 * - `complete`: all 5 field groups collected; `IntakeSummaryPanel` is shown below the chat.
 * - `unavailable`: Ollama 503 received; error banner with link to manual form is shown.
 *
 * PHI guardrails:
 * - Patient messages are stored exclusively in the `messages` React state array —
 *   never written to localStorage, sessionStorage, IndexedDB, or any browser-persistent
 *   storage (AIR guardrails; OWASP A02; HIPAA minimum-necessary; checklist).
 * - The `isLoading` guard disables input and Send while an API call is in flight,
 *   preventing double-submit and session turn-order corruption (AC-002; AC-001; checklist).
 * - A 401 from either endpoint triggers `setAuth(null, null)` which redirects to /login
 *   via `ProtectedRoute` — the patient is not left on a confusing error screen (AC-001; OWASP A01).
 */
/** Router-state shape passed to SCR-004 on a Manual→AI mode switch (us_018; AC-002; AC-003). */
interface AiPageRouterState {
  mappedFields?: ModeSwitchResponse['mappedFields']
  reviewItems?:  string[]
  cacheVersion?: string
}

export function AiIntakePage() {
  const { accessToken, setAuth } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()

  // ── Session state ────────────────────────────────────────────────────────────────────────────
  const [sessionId, setSessionId]   = useState<string | null>(null)
  // Chat messages stored ONLY in React state — never persisted to any browser storage (AIR guardrails)
  const [messages, setMessages]     = useState<ChatMessage[]>([])
  const [isLoading, setIsLoading]   = useState(false)
  const [inputText, setInputText]   = useState('')
  const [summary, setSummary]       = useState<IntakeSummary | null>(null)

  // ── Error state ──────────────────────────────────────────────────────────────────────────────
  const [unavailable, setUnavailable]   = useState(false) // 503 — show manual-form banner
  const [timeoutError, setTimeoutError] = useState(false) // 504 — show inline retry message
  const [generalError, setGeneralError] = useState<string | null>(null)

  // ── Mode-switch state (us_018) ───────────────────────────────────────────────────────────────
  const [isSwitching, setIsSwitching]     = useState(false)
  const [switchError, setSwitchError]     = useState<string | null>(null)
  const [reviewItems, setReviewItems]     = useState<string[]>([])

  // Keep a stable ref to the textarea for focus-restore after loading
  const inputRef = useRef<HTMLTextAreaElement>(null)

  // ── Read router state on mount (Manual→AI switch pre-population; AC-002; AC-003) ────────────
  useEffect(() => {
    const state = location.state as AiPageRouterState | undefined
    if (!state) return

    // reviewItems[] from the mode-switch response — rendered above the chat window (AC-003)
    if (Array.isArray(state.reviewItems) && state.reviewItems.length > 0) {
      setReviewItems(state.reviewItems)
    }
    // mappedFields — if a newSessionId was set by the backend, the session state in IDistributedCache
    // already has pre-populated fields; the chat dialogue will skip already-completed fields
    // on its first turn because AllFieldsCollected check in IntakeSessionState is aware (AC-002).
    // No client-side action is needed beyond starting the session normally.
  }, []) // eslint-disable-line react-hooks/exhaustive-deps

  // ── Start session ────────────────────────────────────────────────────────────────────────────

  async function handleStart() {
    if (!accessToken) return
    setIsLoading(true)
    setUnavailable(false)
    setTimeoutError(false)
    setGeneralError(null)

    try {
      const res = await startSession(accessToken)
      setSessionId(res.sessionId)
      setMessages([{ id: newId(), role: 'ai', content: res.message }])
    } catch (err) {
      if (err instanceof AiUnavailableError) {
        setUnavailable(true)
      } else if (err instanceof AiTimeoutError) {
        // Timeout on start — no messages yet, treat same as unavailable for UX
        setUnavailable(true)
      } else if (err instanceof Error && err.message.includes('Authentication')) {
        setAuth(null, null)
        navigate('/login', { replace: true })
      } else {
        setGeneralError(err instanceof Error ? err.message : 'An unexpected error occurred.')
      }
    } finally {
      setIsLoading(false)
    }
  }

  // ── Send message ─────────────────────────────────────────────────────────────────────────────

  async function handleSend(e: React.FormEvent) {
    e.preventDefault()
    const text = inputText.trim()
    if (!text || !sessionId || !accessToken || isLoading) return

    setInputText('')
    setTimeoutError(false)
    setGeneralError(null)

    // Optimistic update: push patient message immediately before API call returns (checklist)
    const patientMsg: ChatMessage = { id: newId(), role: 'patient', content: text }
    setMessages(prev => [...prev, patientMsg])
    setIsLoading(true)

    try {
      const res = await sendMessage(accessToken, sessionId, text)

      setMessages(prev => [...prev, { id: newId(), role: 'ai', content: res.message }])

      if (res.allFieldsCollected && res.summary) {
        setSummary(res.summary)
      }
    } catch (err) {
      if (err instanceof AiUnavailableError) {
        setUnavailable(true)
      } else if (err instanceof AiTimeoutError) {
        // Inline retry message; input re-enabled; session preserved on backend (Edge: timeout; checklist)
        setTimeoutError(true)
      } else if (err instanceof Error && err.message.includes('Authentication')) {
        setAuth(null, null)
        navigate('/login', { replace: true })
      } else {
        setGeneralError(err instanceof Error ? err.message : 'An unexpected error occurred.')
      }
    } finally {
      setIsLoading(false)
      // Restore focus to input after loading so patient can continue typing (UX)
      setTimeout(() => inputRef.current?.focus(), 0)
    }
  }

  // ── Mode switch: AI → Manual (us_018; AC-001; AC-003) ──────────────────────────────────────
  async function handleSwitchToManual() {
    if (!accessToken || isSwitching) return
    setSwitchError(null)
    setIsSwitching(true)
    try {
      const res = await switchMode(accessToken, {
        from:      'AI',
        to:        'Manual',
        sessionId: sessionId ?? undefined,
      })
      // Navigate to manual form — pass mapped fields and review items via router state
      // (no sessionStorage; PHI stays in React state only; OWASP A02; AIR guardrails)
      navigate('/intake/manual', {
        state: {
          mappedFields: res.mappedFields,
          reviewItems:  res.reviewItems,
          cacheVersion: res.cacheVersion,
        },
      })
    } catch (err) {
      setSwitchError(err instanceof Error ? err.message : 'Mode switch failed. Please try again.')
    } finally {
      setIsSwitching(false)
    }
  }

  function handleKeyDown(e: React.KeyboardEvent<HTMLTextAreaElement>) {
    // Submit on Enter (without Shift); Shift+Enter inserts newline
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault()
      handleSend(e as unknown as React.FormEvent)
    }
  }

  // ── Render ───────────────────────────────────────────────────────────────────────────────────

  const hasSession = sessionId !== null

  return (
    <div
      style={{
        display: 'flex',
        flexDirection: 'column',
        height: '100vh',
        background: 'var(--color-bg-page)',
      }}
    >
      {/* ── Page header ───────────────────────────────────────────────────────────────────── */}
      <header
        style={{
          height: 56,
          background: 'var(--color-bg-surface)',
          borderBottom: '1px solid var(--color-border)',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          padding: '0 var(--space-8)',
          flexShrink: 0,
        }}
      >
        <div style={{ fontSize: '16px', fontWeight: 600, color: 'var(--color-text-primary)' }}>
          AI conversational intake
        </div>
        {/* AC-004: switch button — 44×44px, icon+text, keyboard focusable (IntakeSwitchButton) */}
        <IntakeSwitchButton
          targetMode="Manual"
          onClick={handleSwitchToManual}
          isLoading={isSwitching}
        />
      </header>

      {/* ── 503 Unavailable banner ─────────────────────────────────────────────────────────── */}
      {unavailable && (
        <div
          role="alert"
          style={{
            display: 'flex',
            alignItems: 'flex-start',
            gap: 'var(--space-3)',
            padding: 'var(--space-3) var(--space-8)',
            background: 'var(--color-error-surface)',
            borderBottom: '1px solid var(--color-error-border)',
            color: 'var(--color-status-error)',
            fontSize: '14px',
          }}
        >
          {/* Icon + text required — colour is supplementary, not the sole indicator (UXR-105; WCAG 1.4.1) */}
          <ErrorIcon />
          <span>
            AI intake is temporarily unavailable. You can{' '}
            <a
              href="/intake"
              style={{ color: 'var(--color-status-error)', fontWeight: 600 }}
            >
              use the manual form instead
            </a>
            .
          </span>
        </div>
      )}

      {/* ── Mode-switch error (icon + text; role="alert"; UXR-105; WCAG 4.1.3) ─────────────── */}
      {switchError && (
        <div
          role="alert"
          style={{
            display:       'flex',
            alignItems:    'flex-start',
            gap:           'var(--space-3)',
            padding:       'var(--space-3) var(--space-8)',
            background:    'var(--color-error-surface)',
            borderBottom:  '1px solid var(--color-error-border)',
            color:         'var(--color-status-error)',
            fontSize:      '14px',
          }}
        >
          <ErrorIcon />
          <span>{switchError}</span>
        </div>
      )}

      {/* ── General error banner ──────────────────────────────────────────────────────────── */}
      {generalError && (
        <div
          role="alert"
          style={{
            display: 'flex',
            alignItems: 'flex-start',
            gap: 'var(--space-3)',
            padding: 'var(--space-3) var(--space-8)',
            background: 'var(--color-error-surface)',
            borderBottom: '1px solid var(--color-error-border)',
            color: 'var(--color-status-error)',
            fontSize: '14px',
          }}
        >
          <ErrorIcon />
          <span>{generalError}</span>
        </div>
      )}

      {/* ── Main content ─────────────────────────────────────────────────────────────────── */}
      <main
        style={{
          flex: 1,
          overflow: 'hidden',
          display: 'flex',
          justifyContent: 'center',
        }}
      >
        <div
          style={{
            width: '100%',
            maxWidth: 720,
            display: 'flex',
            flexDirection: 'column',
            overflow: 'hidden',
          }}
        >
          {/* ── Review buffer — unmapped items from Manual→AI switch (AC-003; UXR-105) ───── */}
          <IntakeReviewBuffer reviewItems={reviewItems} />

          {/* ── Pre-session intro panel ─────────────────────────────────────────────────── */}
          {!hasSession && !unavailable && (
            <div
              style={{
                flex: 1,
                display: 'flex',
                flexDirection: 'column',
                alignItems: 'center',
                justifyContent: 'center',
                padding: 'var(--space-8)',
                gap: 'var(--space-6)',
                textAlign: 'center',
              }}
            >
              <div
                style={{
                  width: 56,
                  height: 56,
                  borderRadius: '50%',
                  background: 'var(--color-ai-accent-surface)',
                  color: 'var(--color-ai-accent)',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  fontSize: '22px',
                  fontWeight: 700,
                }}
                aria-hidden="true"
              >
                AI
              </div>
              <div>
                <h1 style={{ fontSize: '20px', fontWeight: 700, marginBottom: 'var(--space-2)' }}>
                  AI health intake
                </h1>
                <p style={{ fontSize: '14px', color: 'var(--color-text-secondary)', maxWidth: 420 }}>
                  Answer a few questions in plain language. Our AI assistant will guide you through
                  collecting your chief complaint, medical history, medications, allergies, and
                  demographics — typically 5–8 minutes.
                </p>
              </div>
              <button
                type="button"
                onClick={handleStart}
                disabled={isLoading}
                style={{
                  background: 'var(--color-primary)',
                  color: 'var(--color-text-inverse)',
                  fontFamily: 'var(--font-sans)',
                  fontSize: '15px',
                  fontWeight: 600,
                  padding: 'var(--space-3) var(--space-8)',
                  borderRadius: 'var(--radius-sm)',
                  border: 'none',
                  cursor: isLoading ? 'default' : 'pointer',
                  minHeight: 44,
                  opacity: isLoading ? 0.7 : 1,
                }}
              >
                {isLoading ? 'Starting…' : 'Start AI Intake'}
              </button>
            </div>
          )}

          {/* ── Active chat interface ───────────────────────────────────────────────────── */}
          {hasSession && (
            <div
              style={{
                flex: 1,
                display: 'flex',
                flexDirection: 'column',
                overflow: 'hidden',
              }}
            >
              {/* Chat header */}
              <div
                style={{
                  padding: 'var(--space-4) var(--space-6)',
                  borderBottom: '1px solid var(--color-border)',
                  background: 'var(--color-bg-surface)',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'space-between',
                  flexShrink: 0,
                }}
              >
                <div>
                  <div
                    style={{
                      display: 'inline-flex',
                      alignItems: 'center',
                      gap: '4px',
                      background: 'var(--color-ai-accent-surface)',
                      color: 'var(--color-ai-accent)',
                      fontSize: '11px',
                      fontWeight: 600,
                      padding: '2px 8px',
                      borderRadius: 'var(--radius-full)',
                      border: '1px solid #C7D2FE',
                      marginBottom: 'var(--space-1)',
                    }}
                    aria-label="AI-generated content"
                  >
                    <span aria-hidden="true">✦</span> AI
                  </div>
                  <div style={{ fontSize: '15px', fontWeight: 600 }}>Health intake conversation</div>
                </div>
              </div>

              {/* Message list — flex-grows to fill available space; internally scrollable */}
              <div style={{ flex: 1, overflow: 'hidden', display: 'flex', flexDirection: 'column' }}>
                <ChatMessageList messages={messages} isLoading={isLoading} />

                {/* Timeout retry message — inline below last message (Edge: timeout; UXR-105) */}
                {timeoutError && (
                  <div
                    role="alert"
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      gap: 'var(--space-2)',
                      padding: 'var(--space-3) var(--space-6)',
                      color: 'var(--color-status-warning)',
                      fontSize: '13px',
                    }}
                  >
                    {/* Icon + text required — colour is supplementary, not the sole indicator (UXR-105) */}
                    <ClockIcon />
                    <span>The AI took too long to respond. Please try your message again.</span>
                  </div>
                )}

                {/* Summary panel — shown when all 5 field groups are collected (AC-002; AIR-002) */}
                {summary && sessionId && accessToken && (
                  <div style={{ padding: '0 var(--space-6) var(--space-6)' }}>
                    <IntakeSummaryPanel
                      sessionId={sessionId}
                      accessToken={accessToken}
                      initialSummary={summary}
                    />
                  </div>
                )}
              </div>

              {/* ── Chat input bar ──────────────────────────────────────────────────────── */}
              <form
                onSubmit={handleSend}
                style={{
                  padding: 'var(--space-4) var(--space-6)',
                  borderTop: '1px solid var(--color-border)',
                  background: 'var(--color-bg-surface)',
                  display: 'flex',
                  gap: 'var(--space-3)',
                  alignItems: 'flex-end',
                  flexShrink: 0,
                }}
              >
                <label htmlFor="chat-input" style={{ position: 'absolute', left: -9999 }}>
                  Type your response
                </label>
                <textarea
                  id="chat-input"
                  ref={inputRef}
                  value={inputText}
                  onChange={e => setInputText(e.target.value)}
                  onKeyDown={handleKeyDown}
                  // isLoading guard — disables input while API call is in flight (checklist; AC-002)
                  disabled={isLoading || !!summary}
                  rows={1}
                  placeholder={summary ? 'Intake complete' : 'Type your response…'}
                  aria-label="Type your response"
                  aria-disabled={isLoading || !!summary}
                  style={{
                    flex: 1,
                    fontFamily: 'var(--font-sans)',
                    fontSize: '15px',
                    padding: 'var(--space-3) var(--space-4)',
                    border: '1px solid var(--color-border)',
                    borderRadius: 'var(--radius-sm)',
                    background: 'var(--color-bg-surface)',
                    color: 'var(--color-text-primary)',
                    outline: 'none',
                    resize: 'none',
                    lineHeight: 1.5,
                    overflowY: 'hidden',
                    opacity: (isLoading || !!summary) ? 0.6 : 1,
                  }}
                  onFocus={e => { e.currentTarget.style.borderColor = 'var(--color-border-focus)'; e.currentTarget.style.boxShadow = '0 0 0 3px rgba(26,86,219,0.18)' }}
                  onBlur={e => { e.currentTarget.style.borderColor = 'var(--color-border)'; e.currentTarget.style.boxShadow = 'none' }}
                />
                {/* isLoading guard — disables Send while in flight to prevent double-submit (checklist) */}
                <button
                  type="submit"
                  disabled={isLoading || !inputText.trim() || !!summary}
                  aria-label="Send message"
                  style={{
                    background: 'var(--color-primary)',
                    color: 'var(--color-text-inverse)',
                    border: 'none',
                    borderRadius: 'var(--radius-sm)',
                    padding: 'var(--space-3)',
                    cursor: (isLoading || !inputText.trim() || !!summary) ? 'default' : 'pointer',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    minHeight: 44,
                    minWidth: 44,
                    opacity: (isLoading || !inputText.trim() || !!summary) ? 0.5 : 1,
                    flexShrink: 0,
                  }}
                >
                  <SendIcon />
                </button>
              </form>
            </div>
          )}
        </div>
      </main>
    </div>
  )
}
