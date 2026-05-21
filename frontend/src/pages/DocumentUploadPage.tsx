/**
 * SCR-009 — Document Upload page (us_035; AC-001, AC-002, AC-004, AC-005).
 *
 * Patient-only route guard: non-Patient roles are redirected immediately (OWASP A01).
 * File `<input>` uses `accept=".pdf,.docx"` and `capture="environment"` for mobile camera
 * fallback (AC-005; UXR-303).
 * Client-side size hint fires before the network call; server 413 is also handled as authoritative
 * rejection (AC-002; Edge: client hint).
 * All status states (uploading / success / error) use distinct icon + text — never colour alone
 * (UXR-105; WCAG 2.1 SC 1.4.1).
 */

import { useEffect, useRef, useState, type CSSProperties } from 'react'
import { useNavigate }  from 'react-router-dom'
import { useAuth }      from '../context/AuthContext'
import { Header }       from '../components/layout/Header'
import { uploadDocument, DocumentApiError } from '../api/documentApi'

// ── Constants ─────────────────────────────────────────────────────────────────────────────────────

const MAX_FILE_SIZE = 25 * 1024 * 1024  // 25 MB (AC-002)

// ── Status type ───────────────────────────────────────────────────────────────────────────────────

type UploadStatus = 'idle' | 'uploading' | 'success' | 'error'

// ── SVG icons (distinct shapes — not colour-only per UXR-105; WCAG 2.1 SC 1.4.1) ─────────────────

function SpinnerIcon() {
  return (
    <svg
      aria-hidden="true"
      width="18"
      height="18"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      style={{ animation: 'spin 1s linear infinite', flexShrink: 0 }}
    >
      <circle cx="12" cy="12" r="10" strokeOpacity="0.25" />
      <path d="M12 2a10 10 0 0 1 10 10" />
    </svg>
  )
}

function CheckIcon() {
  return (
    <svg
      aria-hidden="true"
      width="18"
      height="18"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2.5"
      strokeLinecap="round"
      strokeLinejoin="round"
      style={{ flexShrink: 0 }}
    >
      <polyline points="20 6 9 17 4 12" />
    </svg>
  )
}

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
      <line x1="12" y1="8"  x2="12" y2="12" />
      <line x1="12" y1="16" x2="12.01" y2="16" />
    </svg>
  )
}

function UploadIcon() {
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
      <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" />
      <polyline points="17 8 12 3 7 8" />
      <line x1="12" y1="3" x2="12" y2="15" />
    </svg>
  )
}

// ── Styles ────────────────────────────────────────────────────────────────────────────────────────

const pageStyle: CSSProperties = {
  minHeight:  '100vh',
  background: 'var(--color-bg-page)',
}

const contentStyle: CSSProperties = {
  maxWidth: '640px',
  margin:   '0 auto',
  padding:  'var(--space-10) var(--space-6)',
}

const cardStyle: CSSProperties = {
  background:   'var(--color-bg-surface)',
  border:       '1px solid var(--color-border)',
  borderRadius: 'var(--radius-md)',
  padding:      'var(--space-8)',
  boxShadow:    '0 1px 3px rgba(15,23,42,0.08)',
}

const pageTitleStyle: CSSProperties = {
  fontSize:     '22px',
  fontWeight:   700,
  marginBottom: 'var(--space-2)',
  color:        'var(--color-text-primary)',
}

const pageSubStyle: CSSProperties = {
  fontSize:     '14px',
  color:        'var(--color-text-secondary)',
  marginBottom: 'var(--space-6)',
}

const dropzoneStyle: CSSProperties = {
  border:        '2px dashed var(--color-border-strong)',
  borderRadius:  'var(--radius-md)',
  padding:       'var(--space-8)',
  textAlign:     'center',
  background:    'var(--color-bg-surface)',
  cursor:        'pointer',
  marginBottom:  'var(--space-6)',
}

const dropzoneSubStyle: CSSProperties = {
  fontSize:     '13px',
  color:        'var(--color-text-secondary)',
  marginBottom: 'var(--space-4)',
}

const labelBtnStyle: CSSProperties = {
  display:        'inline-flex',
  alignItems:     'center',
  gap:            'var(--space-2)',
  fontFamily:     'var(--font-sans)',
  fontSize:       '14px',
  fontWeight:     600,
  padding:        'var(--space-3) var(--space-5)',
  borderRadius:   'var(--radius-sm)',
  background:     'var(--color-bg-surface)',
  color:          'var(--color-text-primary)',
  border:         '1px solid var(--color-border)',
  cursor:         'pointer',
  minHeight:      '44px',
}

const fileSelectedStyle: CSSProperties = {
  display:       'flex',
  alignItems:    'center',
  gap:           'var(--space-3)',
  fontSize:      '14px',
  color:         'var(--color-text-primary)',
  padding:       'var(--space-3) var(--space-4)',
  background:    'var(--color-bg-subtle)',
  borderRadius:  'var(--radius-sm)',
  marginBottom:  'var(--space-4)',
  border:        '1px solid var(--color-border)',
}

const submitBtnStyle = (disabled: boolean): CSSProperties => ({
  display:        'inline-flex',
  alignItems:     'center',
  gap:            'var(--space-2)',
  fontFamily:     'var(--font-sans)',
  fontSize:       '14px',
  fontWeight:     600,
  padding:        'var(--space-3) var(--space-6)',
  borderRadius:   'var(--radius-sm)',
  background:     disabled ? 'var(--color-text-disabled)' : 'var(--color-primary)',
  color:          'var(--color-text-inverse)',
  border:         'none',
  cursor:         disabled ? 'not-allowed' : 'pointer',
  minHeight:      '44px',
  opacity:        disabled ? 0.65 : 1,
})

const statusStyle = (type: 'uploading' | 'success' | 'error'): CSSProperties => ({
  display:     'flex',
  alignItems:  'flex-start',
  gap:         'var(--space-2)',
  fontSize:    '14px',
  fontWeight:  500,
  marginTop:   'var(--space-4)',
  padding:     'var(--space-3) var(--space-4)',
  borderRadius: 'var(--radius-sm)',
  color: type === 'success'
    ? 'var(--color-status-success)'
    : type === 'error'
      ? 'var(--color-status-error)'
      : 'var(--color-primary)',
  background: type === 'success'
    ? '#DCFCE7'
    : type === 'error'
      ? '#FEF2F2'
      : 'var(--color-primary-subtle)',
})

const clientErrorStyle: CSSProperties = {
  display:     'flex',
  alignItems:  'flex-start',
  gap:         '4px',
  fontSize:    '13px',
  color:       'var(--color-status-error)',
  marginTop:   '4px',
  marginBottom: 'var(--space-4)',
}

// ── Component ─────────────────────────────────────────────────────────────────────────────────────

/**
 * SCR-009 Document Upload page — Patient-only.
 * File input supports `.pdf` and `.docx`; `capture="environment"` enables camera fallback on mobile
 * (AC-005; UXR-303). Upload status is conveyed by icon + text, never by colour alone (UXR-105).
 */
export function DocumentUploadPage() {
  const { accessToken, role } = useAuth()
  const navigate = useNavigate()

  const [selectedFile, setSelectedFile] = useState<File | null>(null)
  const [clientError,  setClientError]  = useState<string | null>(null)
  const [status,       setStatus]       = useState<UploadStatus>('idle')
  const [errorMessage, setErrorMessage] = useState<string | null>(null)
  const [documentId,   setDocumentId]   = useState<string | null>(null)

  const inputRef = useRef<HTMLInputElement>(null)

  // ── Role guard: Patient-only route (OWASP A01; AC-001; task validation plan) ─────────────────
  useEffect(() => {
    if (!role) return
    if (role !== 'Patient') {
      // Redirect non-patients to their role home — Staff → /queue, Admin → /admin, other → /login
      if (role === 'Staff')      navigate('/queue',  { replace: true })
      else if (role === 'Admin') navigate('/admin',  { replace: true })
      else                       navigate('/login',  { replace: true })
    }
  }, [role, navigate])

  // Block render until role check resolves — prevents flash of patient content for wrong role
  if (role && role !== 'Patient') return null

  // ── Handlers ──────────────────────────────────────────────────────────────────────────────────

  function handleFileChange(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0] ?? null
    setSelectedFile(file)
    setStatus('idle')
    setErrorMessage(null)
    setDocumentId(null)

    // Client-side size hint — convenience check only; server 413 is the authoritative rejection (AC-002; Edge: client hint)
    if (file && file.size > MAX_FILE_SIZE) {
      setClientError('File exceeds 25 MB limit.')
    } else {
      setClientError(null)
    }
  }

  async function handleSubmit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault()

    // Prevent empty submission — submit button is also disabled, this is a safety net (OWASP A03)
    if (!selectedFile || !accessToken) return

    // Client-side size guard — prevents network call for an oversized file (AC-002; checklist)
    if (selectedFile.size > MAX_FILE_SIZE) {
      setClientError('File exceeds 25 MB limit.')
      return
    }

    setStatus('uploading')
    setErrorMessage(null)
    setDocumentId(null)

    try {
      const result = await uploadDocument(selectedFile, accessToken)
      setDocumentId(result.documentId)
      setStatus('success')

      // Clear file input after success so the user can upload a subsequent document (AC-004; checklist)
      setSelectedFile(null)
      if (inputRef.current) inputRef.current.value = ''
    } catch (err) {
      if (err instanceof DocumentApiError) {
        setErrorMessage(err.message) // server's error field — not hard-coded (checklist; AC-001, AC-002)
      } else {
        setErrorMessage('An unexpected error occurred. Please try again.')
      }
      setStatus('error')
    }
  }

  // ── Render ────────────────────────────────────────────────────────────────────────────────────

  const isSubmitDisabled =
    !selectedFile || status === 'uploading' || clientError !== null

  return (
    <div style={pageStyle}>
      <style>{`
        @keyframes spin { to { transform: rotate(360deg); } }
        .upload-label-btn:hover { background: var(--color-bg-subtle) !important; }
        .upload-label-btn:focus-within { outline: 2px solid var(--color-border-focus); outline-offset: 2px; }
        .upload-submit-btn:focus-visible { outline: 2px solid var(--color-border-focus); outline-offset: 2px; }
      `}</style>

      <Header />

      <main style={contentStyle} id="main-content">
        <h1 style={pageTitleStyle}>Upload documents</h1>
        <p style={pageSubStyle}>
          Upload clinical documents such as lab results or previous prescriptions.
          Accepted formats: PDF, DOCX · Maximum 25 MB.
        </p>

        <div style={cardStyle}>
          <form onSubmit={handleSubmit} noValidate>

            {/* File drop-zone / input area (UXR-303: file input with camera fallback on mobile) */}
            <div style={dropzoneStyle}>
              {/* Hidden file input — label acts as the visible click target (WCAG 2.1 SC 1.3.1) */}
              <input
                ref={inputRef}
                id="docFile"
                type="file"
                // accept constrains the file picker; capture enables camera app on mobile (AC-005; UXR-303)
                accept=".pdf,.docx"
                capture="environment"
                onChange={handleFileChange}
                style={{ position: 'absolute', opacity: 0, width: 0, height: 0 }}
                aria-label="Select a PDF or DOCX file to upload"
                tabIndex={-1}
              />

              <div style={{ fontSize: '36px', marginBottom: 'var(--space-3)', color: 'var(--color-text-disabled)' }} aria-hidden="true">
                📄
              </div>
              <p style={dropzoneSubStyle}>PDF or DOCX · Max 25 MB</p>

              {/* Visible label acting as the select-file button (paired label for input — WCAG 2.1 SC 1.3.1) */}
              <label
                htmlFor="docFile"
                className="upload-label-btn"
                style={labelBtnStyle}
              >
                <UploadIcon />
                Select file
              </label>
            </div>

            {/* Selected file display */}
            {selectedFile && (
              <div style={fileSelectedStyle} aria-live="polite">
                <span style={{ fontSize: '11px', fontWeight: 700, color: 'var(--color-text-secondary)', background: 'var(--color-bg-page)', padding: '2px 6px', borderRadius: '2px', border: '1px solid var(--color-border)' }}>
                  {selectedFile.name.split('.').pop()?.toUpperCase() ?? 'FILE'}
                </span>
                <span style={{ flex: 1, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                  {selectedFile.name}
                </span>
                <span style={{ fontSize: '12px', color: 'var(--color-text-secondary)', flexShrink: 0 }}>
                  {(selectedFile.size / (1024 * 1024)).toFixed(1)} MB
                </span>
              </div>
            )}

            {/* Client-side size hint — inline error shown before network call (AC-002; Edge: client hint; UXR-105) */}
            {clientError && (
              <p role="alert" style={clientErrorStyle}>
                <ErrorIcon />
                {clientError}
              </p>
            )}

            {/* Submit button — disabled when no file or client error or uploading (OWASP A03; checklist) */}
            <button
              type="submit"
              disabled={isSubmitDisabled}
              className="upload-submit-btn"
              style={submitBtnStyle(isSubmitDisabled)}
              aria-disabled={isSubmitDisabled}
            >
              {status === 'uploading' ? <SpinnerIcon /> : <UploadIcon />}
              {status === 'uploading' ? 'Uploading…' : 'Upload document'}
            </button>

          </form>

          {/* ── Upload status area (UXR-105: icon + text, not colour alone; WCAG 2.1 SC 1.4.1) ── */}

          {status === 'uploading' && (
            <p role="status" aria-live="polite" style={statusStyle('uploading')}>
              <SpinnerIcon />
              Uploading document…
            </p>
          )}

          {status === 'success' && documentId && (
            <p role="status" aria-live="polite" style={statusStyle('success')}>
              <CheckIcon />
              Document uploaded successfully. ID: {documentId}
            </p>
          )}

          {status === 'error' && errorMessage && (
            <p role="alert" style={statusStyle('error')}>
              <ErrorIcon />
              {errorMessage}
            </p>
          )}
        </div>
      </main>
    </div>
  )
}
