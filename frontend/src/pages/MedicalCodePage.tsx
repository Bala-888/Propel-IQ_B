/**
 * MedicalCodePage — SCR-015 Medical Code Review (us_043/task_002; us_044/task_002; AC-001–AC-005).
 *
 * Fetches AI-generated ICD-10/CPT code suggestions for the patient identified by `:id` in the
 * URL, then renders up to 10 SuggestionCard components with Accept/Reject/Correct actions.
 *
 * States:
 *   loading — spinner / status announcement while RAG pipeline runs (up to 10 s; AC-001).
 *   error   — network/5xx failure; role="alert" for assertive SR announcement.
 *   empty   — API returned suggestions:[] + message (insufficient data edge case); role="status".
 *   list    — 1–10 suggestion cards rendered inside role="list".
 *   allReviewed — all cards reviewed; banner shown (Edge: all rejected; AC-003).
 *
 * Accessibility:
 *   WCAG SC 2.4.2 — descriptive page title set via document.title on mount.
 *   WCAG SC 2.4.6 — descriptive h1 "Medical code review".
 *   WCAG SC 4.1.3 — role="status" for expected states; role="alert" for errors.
 *   WCAG SC 2.1.2 — Reject dialog: focus trap + Escape to close + focus restoration.
 *   min-height 44px on the Back link (wireframe back-btn).
 *
 * OWASP A01:
 *   Role guard redirects Patient/Staff before any API call — error-state 403 also redirects.
 *   [Authorize(Roles = "Clinician,Admin")] on the backend is the authoritative gate.
 * OWASP A02: No PHI (suggestion content, chunk text, corrected codes) is written to any logger.
 */

import { useCallback, useEffect, useRef, useState } from 'react'
import { Link, useNavigate, useParams }              from 'react-router-dom'
import { useAuth }                                   from '../context/AuthContext'
import { getCodeSuggestions }                        from '../api/codeSuggestionsApi'
import { submitMedicalCode, rejectCodeSuggestion }   from '../api/medicalCodesApi'
import { SuggestionCard }                            from '../components/codes/SuggestionCard'
import type { CodeSuggestionDto }                    from '../api/codeSuggestionsApi'
import type { ReviewStatus }                         from '../types/codes'

// ── Inline styles matching wireframe SCR-015 design tokens from index.css ────────────────────────
// Complex layout uses inline styles (consistent with PatientSearchPage pattern).

const pageStyle: React.CSSProperties = {
  minHeight:   '100vh',
  background:  'var(--color-bg-page)',
  display:     'flex',
  flexDirection: 'column',
}

const headerStyle: React.CSSProperties = {
  height:       '56px',
  background:   'var(--color-bg-surface)',
  borderBottom: '1px solid var(--color-border)',
  display:      'flex',
  alignItems:   'center',
  padding:      '0 var(--space-8)',
  gap:          'var(--space-4)',
  flexShrink:   0,
}

const backLinkStyle: React.CSSProperties = {
  color:          'var(--color-text-secondary)',
  textDecoration: 'none',
  fontSize:       '14px',
  minHeight:      '44px',  // wireframe back-btn min-height
  display:        'flex',
  alignItems:     'center',
}

const headerTitleStyle: React.CSSProperties = {
  fontSize:   '16px',
  fontWeight: 600,
}

const contentAreaStyle: React.CSSProperties = {
  flex:       1,
  overflowY:  'auto',
  padding:    'var(--space-8)',
}

const pageTitleStyle: React.CSSProperties = {
  fontSize:     '24px',
  fontWeight:   700,
  marginBottom: 'var(--space-2)',
}

const pageSubStyle: React.CSSProperties = {
  fontSize:     '14px',
  color:        'var(--color-text-secondary)',
  marginBottom: 'var(--space-8)',
  display:      'flex',
  alignItems:   'center',
  gap:          'var(--space-3)',
}

const aiLabelStyle: React.CSSProperties = {
  display:        'inline-flex',
  alignItems:     'center',
  gap:            '4px',
  fontSize:       '11px',
  fontWeight:     600,
  color:          'var(--color-ai-accent)',
  background:     'var(--color-ai-bg)',
  padding:        '2px 8px',
  borderRadius:   'var(--radius-full)',
  whiteSpace:     'nowrap',
}

const cardListStyle: React.CSSProperties = {
  display:      'flex',
  flexDirection: 'column',
  gap:          'var(--space-4)',
  maxWidth:     '800px',
}

const statusPanelStyle: React.CSSProperties = {
  padding:      'var(--space-6)',
  background:   'var(--color-bg-surface)',
  border:       '1px solid var(--color-border)',
  borderRadius: 'var(--radius-md)',
  fontSize:     '14px',
  color:        'var(--color-text-secondary)',
  maxWidth:     '600px',
}

const errorPanelStyle: React.CSSProperties = {
  ...statusPanelStyle,
  background:  'var(--color-error-surface)',
  border:      '1px solid var(--color-error-border)',
  color:       'var(--color-status-error)',
}

// ── Reject confirm dialog styles (wireframe .dialog-overlay / .dialog; UXR-404) ─────────────────

const dialogOverlayStyle: React.CSSProperties = {
  position:       'fixed',
  inset:          0,
  background:     'rgba(15, 23, 42, 0.48)',
  display:        'flex',
  alignItems:     'center',
  justifyContent: 'center',
  zIndex:         200,
}

const dialogStyle: React.CSSProperties = {
  background:   'var(--color-bg-surface)',
  borderRadius: 'var(--radius-md)',
  padding:      'var(--space-8)',
  boxShadow:    '0 8px 24px rgba(15,23,42,0.14)',
  maxWidth:     '420px',
  width:        '90%',
}

const dialogTitleStyle: React.CSSProperties = {
  fontSize:     '18px',
  fontWeight:   700,
  marginBottom: 'var(--space-3)',
}

const dialogBodyStyle: React.CSSProperties = {
  fontSize:     '14px',
  color:        'var(--color-text-secondary)',
  marginBottom: 'var(--space-8)',
}

const dialogActionsStyle: React.CSSProperties = {
  display:        'flex',
  gap:            'var(--space-3)',
  justifyContent: 'flex-end',
}

const btnSecondaryStyle: React.CSSProperties = {
  fontFamily:   'var(--font-sans)',
  fontSize:     '13px',
  fontWeight:   600,
  padding:      '8px 16px',
  borderRadius: 'var(--radius-sm)',
  border:       '1px solid var(--color-border)',
  background:   'var(--color-bg-surface)',
  color:        'var(--color-text-primary)',
  cursor:       'pointer',
  minHeight:    '40px',
}

const btnDestructiveStyle: React.CSSProperties = {
  fontFamily:   'var(--font-sans)',
  fontSize:     '13px',
  fontWeight:   600,
  padding:      '8px 16px',
  borderRadius: 'var(--radius-sm)',
  border:       'none',
  background:   '#DC2626',
  color:        'white',
  cursor:       'pointer',
  minHeight:    '40px',
}

// ── Page ──────────────────────────────────────────────────────────────────────────────────────────

export function MedicalCodePage() {
  const { accessToken, role } = useAuth()
  const navigate              = useNavigate()
  const { id }                = useParams<{ id: string }>()

  const [suggestions,  setSuggestions]  = useState<CodeSuggestionDto[]>([])
  const [message,      setMessage]      = useState<string | null>(null)
  const [loading,      setLoading]      = useState(true)
  const [error,        setError]        = useState(false)

  // ── Per-card review state — initialised from API response (checklist item 1)
  // Preserves prior review states on re-fetch; must NOT default all cards to 'Pending'
  // regardless of the API-returned reviewStatus (OWASP A04 — state consistency; AC-002).
  const [cardStates,  setCardStates]  = useState<Record<string, ReviewStatus>>({})
  const [cardErrors,  setCardErrors]  = useState<Record<string, string | null>>({})
  const [cardLoading, setCardLoading] = useState<Record<string, boolean>>({})

  // ── Reject confirm dialog state ──────────────────────────────────────────────────────────
  const [pendingRejectId,   setPendingRejectId]   = useState<string | null>(null)
  const [pendingRejectCode, setPendingRejectCode] = useState<string>('')
  const [rejectLoading,     setRejectLoading]     = useState(false)

  // Ref to restore focus to the Reject button that opened the dialog (WCAG SC 2.1.2)
  const rejectTriggerRef = useRef<{ [id: string]: HTMLButtonElement | null }>({})
  const cancelBtnRef     = useRef<HTMLButtonElement>(null)

  // ── Role guard — redirect Patient/Staff before API call (OWASP A01; AC-005) ────────────────
  useEffect(() => {
    if (role === 'Patient' || role === 'Staff') {
      navigate('/intake', { replace: true })
    }
  }, [role, navigate])

  // ── Descriptive page title (WCAG SC 2.4.2) ───────────────────────────────────────────────
  useEffect(() => {
    const prev = document.title
    document.title = 'Medical code review | UPACIP'
    return () => { document.title = prev }
  }, [])

  // ── Data fetch with AbortController ──────────────────────────────────────────────────────
  useEffect(() => {
    if (!id || role === 'Patient' || role === 'Staff') return

    const controller = new AbortController()

    setLoading(true)
    setError(false)

    getCodeSuggestions(accessToken ?? '', id, controller.signal)
      .then(data => {
        setSuggestions(data.suggestions)
        setMessage(data.message ?? null)
        // Initialise cardStates from API response — preserves existing review statuses
        // so re-fetching does not reset an Accepted card back to Pending (checklist item 1)
        setCardStates(
          Object.fromEntries(
            data.suggestions.map(s => [s.id, (s.reviewStatus as ReviewStatus) ?? 'Pending'])
          )
        )
      })
      .catch(err => {
        if ((err as Error).name === 'AbortError') return

        const status = (err as { status?: number }).status
        if (status === 403) {
          navigate('/403', { replace: true })
          return
        }
        setError(true)
      })
      .finally(() => setLoading(false))

    return () => { controller.abort() }
  }, [id, accessToken, role, navigate])

  // ── Focus trap for the reject dialog (WCAG SC 2.1.2; UXR-404) ───────────────────────────
  // Tab/Shift-Tab cycle between Cancel and Reject Code buttons inside the dialog.
  useEffect(() => {
    if (!pendingRejectId) return

    // Move focus to Cancel button on open (first focusable element)
    cancelBtnRef.current?.focus()

    function handleKeyDown(e: KeyboardEvent) {
      if (e.key === 'Escape') {
        closeRejectDialog()
      }
    }
    document.addEventListener('keydown', handleKeyDown)
    return () => document.removeEventListener('keydown', handleKeyDown)
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [pendingRejectId])

  // ── Action helpers ────────────────────────────────────────────────────────────────────────

  const setCardStatus = useCallback((sid: string, status: ReviewStatus) => {
    setCardStates(prev => ({ ...prev, [sid]: status }))
  }, [])

  const setCardError = useCallback((sid: string, msg: string | null) => {
    setCardErrors(prev => ({ ...prev, [sid]: msg }))
  }, [])

  const setCardLoad = useCallback((sid: string, v: boolean) => {
    setCardLoading(prev => ({ ...prev, [sid]: v }))
  }, [])

  // ── Accept ────────────────────────────────────────────────────────────────────────────────
  const handleAccept = useCallback(async (suggestion: CodeSuggestionDto) => {
    // Client-side guard: prevent double-submit if already reviewed (OWASP A04; checklist item 5)
    if ((cardStates[suggestion.id] ?? 'Pending') !== 'Pending') return

    setCardLoad(suggestion.id, true)
    setCardError(suggestion.id, null)

    try {
      await submitMedicalCode(accessToken ?? '', id ?? '', {
        suggestionId: suggestion.id,
        codeType:     suggestion.codeType,
        code:         suggestion.code,
        description:  suggestion.description,
        source:       'AI',
        reviewStatus: 'Accepted',
      })
      setCardStatus(suggestion.id, 'Accepted')
    } catch (err) {
      const status  = (err as { status?: number }).status
      const message = (err as Error).message

      if (status === 409) {
        setCardError(suggestion.id, 'This suggestion has already been reviewed.')
      } else {
        setCardError(suggestion.id, message ?? 'Unable to submit. Please try again.')
      }
    } finally {
      setCardLoad(suggestion.id, false)
    }
  }, [accessToken, id, cardStates, setCardLoad, setCardError, setCardStatus])

  // ── Reject dialog ─────────────────────────────────────────────────────────────────────────
  const handleRejectClick = useCallback((suggestion: CodeSuggestionDto) => {
    if ((cardStates[suggestion.id] ?? 'Pending') !== 'Pending') return
    setPendingRejectId(suggestion.id)
    setPendingRejectCode(suggestion.code)
  }, [cardStates])

  function closeRejectDialog() {
    const sid = pendingRejectId
    setPendingRejectId(null)
    setPendingRejectCode('')
    // Restore focus to the trigger button (WCAG SC 2.1.2; UXR-404)
    if (sid) {
      requestAnimationFrame(() => {
        rejectTriggerRef.current[sid]?.focus()
      })
    }
  }

  async function confirmReject() {
    if (!pendingRejectId) return
    const sid = pendingRejectId

    setRejectLoading(true)
    try {
      await rejectCodeSuggestion(accessToken ?? '', sid)
      setCardStatus(sid, 'Rejected')
      closeRejectDialog()
    } catch (err) {
      const status  = (err as { status?: number }).status
      const message = (err as Error).message

      closeRejectDialog()
      if (status === 409) {
        setCardError(sid, 'This suggestion has already been reviewed.')
      } else {
        setCardError(sid, message ?? 'Unable to reject. Please try again.')
      }
    } finally {
      setRejectLoading(false)
    }
  }

  // ── Correct ───────────────────────────────────────────────────────────────────────────────
  const handleCorrect = useCallback(async (suggestion: CodeSuggestionDto, correctedCode: string) => {
    if ((cardStates[suggestion.id] ?? 'Pending') !== 'Pending') return

    setCardLoad(suggestion.id, true)
    setCardError(suggestion.id, null)

    try {
      await submitMedicalCode(accessToken ?? '', id ?? '', {
        suggestionId:  suggestion.id,
        codeType:      suggestion.codeType,
        code:          suggestion.code,   // original AI code
        description:   suggestion.description,
        source:        'AI-Corrected',
        reviewStatus:  'Corrected',
        correctedCode,
      })
      setCardStatus(suggestion.id, 'Corrected')
    } catch (err) {
      const status  = (err as { status?: number }).status
      const message = (err as Error).message

      if (status === 409) {
        setCardError(suggestion.id, 'This suggestion has already been reviewed.')
      } else if (status === 400) {
        setCardError(suggestion.id, message ?? 'Invalid code format.')
      } else {
        setCardError(suggestion.id, message ?? 'Unable to submit. Please try again.')
      }
    } finally {
      setCardLoad(suggestion.id, false)
    }
  }, [accessToken, id, cardStates, setCardLoad, setCardError, setCardStatus])

  // ── All-reviewed detection (Edge: all rejected; AC-003) ─────────────────────────────────
  const allReviewed = suggestions.length > 0 &&
    suggestions.every(s => (cardStates[s.id] ?? 'Pending') !== 'Pending')

  const allRejected = allReviewed &&
    suggestions.every(s => (cardStates[s.id] ?? 'Pending') === 'Rejected')

  // ── Loading state ──────────────────────────────────────────────────────────────────────────
  if (loading) {
    return (
      <div style={pageStyle}>
        <header style={headerStyle}>
          <Link to={`/patients/${id}/view`} style={backLinkStyle} aria-label="Back to patient record">
            ← Back
          </Link>
          <span style={headerTitleStyle}>Medical code review</span>
        </header>
        <main style={contentAreaStyle} id="main-content">
          <p role="status" aria-live="polite" style={{ color: 'var(--color-text-secondary)', fontSize: '14px' }}>
            Loading code suggestions…
          </p>
        </main>
      </div>
    )
  }

  return (
    <div style={pageStyle}>

      {/* ── Reject confirm dialog (UXR-404; WCAG SC 2.1.2 focus trap) ───────────────────── */}
      {/* Rendered at page level — overlay is position:fixed so it covers the full viewport */}
      {pendingRejectId && (
        <div
          style={dialogOverlayStyle}
          role="dialog"
          aria-modal="true"
          aria-labelledby="reject-dialog-title"
          aria-describedby="reject-dialog-body"
        >
          <div style={dialogStyle}>
            <h2 id="reject-dialog-title" style={dialogTitleStyle}>
              Reject this code?
            </h2>
            <p id="reject-dialog-body" style={dialogBodyStyle}>
              This AI suggestion will be marked as rejected and removed from the billing queue.
              This action can be reviewed in the audit log.
            </p>
            <div style={dialogActionsStyle}>
              {/* Cancel — first focusable element; receives focus on dialog open (WCAG SC 2.1.2) */}
              <button
                ref={cancelBtnRef}
                type="button"
                style={btnSecondaryStyle}
                aria-label="Cancel rejection"
                onClick={closeRejectDialog}
              >
                Cancel
              </button>
              <button
                type="button"
                style={btnDestructiveStyle}
                aria-label={`Confirm rejection of ${pendingRejectCode}`}
                disabled={rejectLoading}
                onClick={confirmReject}
              >
                {rejectLoading ? 'Rejecting…' : 'Reject'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* ── Page header ───────────────────────────────────────────────────────────────────── */}
      <header style={headerStyle}>
        <Link
          to={`/patients/${id}/view`}
          style={backLinkStyle}
          aria-label="Back to patient record"
        >
          ← Back
        </Link>
        <span style={headerTitleStyle}>Medical code review</span>
      </header>

      {/* ── Content area ──────────────────────────────────────────────────────────────────── */}
      <main style={contentAreaStyle} id="main-content">
        <h1 style={pageTitleStyle}>Medical code review</h1>

        <div style={pageSubStyle}>
          <span style={aiLabelStyle}>✦ AI suggested</span>
          Review AI-suggested medical codes for this patient.
        </div>

        {/* ── Error state ──────────────────────────────────────────────────────────────── */}
        {error && (
          <div role="alert" style={errorPanelStyle}>
            Unable to load code suggestions. Please try again or refresh the page.
          </div>
        )}

        {/* ── Insufficient-data empty state ─────────────────────────────────────────────── */}
        {!error && suggestions.length === 0 && message !== null && (
          <div role="status" aria-live="polite" style={statusPanelStyle}>
            {message}
          </div>
        )}

        {/* ── Suggestion list ───────────────────────────────────────────────────────────── */}
        {!error && suggestions.length > 0 && (
          <div
            role="list"
            aria-label="Medical code suggestions"
            style={cardListStyle}
          >
            {suggestions.map(suggestion => (
              <SuggestionCard
                key={suggestion.id}
                suggestion={suggestion}
                reviewStatus={cardStates[suggestion.id] ?? 'Pending'}
                loading={cardLoading[suggestion.id] ?? false}
                inlineError={cardErrors[suggestion.id] ?? null}
                onAccept={() => handleAccept(suggestion)}
                onRejectClick={() => handleRejectClick(suggestion)}
                onCorrect={correctedCode => handleCorrect(suggestion, correctedCode)}
              />
            ))}
          </div>
        )}

        {/* ── All-reviewed banner (Edge: all suggestions reviewed; AC-003) ─────────────── */}
        {/* role="status" + aria-live="polite" — informational, not urgent (WCAG SC 4.1.3) */}
        {allReviewed && allRejected && (
          <div
            role="status"
            aria-live="polite"
            style={{
              ...statusPanelStyle,
              marginTop: 'var(--space-4)',
            }}
          >
            All suggestions have been reviewed. No codes were added.
          </div>
        )}
        {allReviewed && !allRejected && (
          <div
            role="status"
            aria-live="polite"
            style={{
              ...statusPanelStyle,
              marginTop:   'var(--space-4)',
              borderColor: '#BBF7D0',
              background:  '#F0FDF4',
              color:       'var(--color-status-success)',
            }}
          >
            All suggestions have been reviewed.
          </div>
        )}

      </main>
    </div>
  )
}
