/**
 * SCR-010 — Document Processing Status page (us_039; AC-001, AC-003, AC-004, AC-005).
 *
 * On mount: fetches all patient documents (`GET /api/documents`), then starts a 5-second
 * polling interval per in-progress document using `GET /api/documents/{id}/status`.
 * Polling stops automatically when status reaches `'EntitiesExtracted'` or any terminal state.
 *
 * Accessibility:
 *  - Document list uses `role="list"` + `role="listitem"` (WCAG 2.1 SC 1.3.1).
 *  - Polling indicator uses `aria-live="polite"` (UXR-503; UXR-206).
 *  - Error/retry alert uses `role="alert"` + `aria-live="assertive"` (UXR-603; WCAG 2.1 SC 4.1.3).
 *  - Error state shows icon + text; never color alone (UXR-603; WCAG 2.1 SC 1.4.1).
 *
 * Security: ownership is enforced server-side; only the JWT sub owner's documents are returned
 * (OWASP A01). No internal error details are exposed in the Retry error toast (OWASP A03).
 */

import { useEffect, useRef, useState } from 'react'
import { useNavigate }                 from 'react-router-dom'
import { useAuth }                     from '../context/AuthContext'
import { Header }                      from '../components/layout/Header'
import { PipelineStepper }             from '../components/PipelineStepper'
import {
  getDocuments,
  getDocumentStatus,
  retryDocument,
  DocumentApiError,
} from '../api/documentApi'
import {
  TERMINAL_STATUSES,
  TERMINAL_FAIL_STATUSES,
  STATUS_LABELS,
  type DocumentStatus,
  type DocumentStatusDto,
} from '../types/document'
import styles from './DocumentStatusPage.module.css'

// ── Helpers ───────────────────────────────────────────────────────────────────────────────────────

const MAX_FILENAME_DISPLAY = 40

function truncateFileName(name: string): string {
  return name.length > MAX_FILENAME_DISPLAY
    ? `${name.substring(0, MAX_FILENAME_DISPLAY)}…`
    : name
}

function badgeClass(status: DocumentStatus): string {
  if (status === 'EntitiesExtracted') return styles.badgeComplete
  if ((TERMINAL_FAIL_STATUSES as ReadonlyArray<string>).includes(status)) return styles.badgeFailed
  if (status === 'Uploaded') return styles.badgePending
  return styles.badgeInProgress
}

// ── SVG icons ─────────────────────────────────────────────────────────────────────────────────────

function ErrorIcon() {
  return (
    <svg
      className={styles.errorIcon}
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
      <circle cx="12" cy="12" r="10" />
      <line x1="12" y1="8"  x2="12" y2="12" />
      <line x1="12" y1="16" x2="12.01" y2="16" />
    </svg>
  )
}

function SpinnerIcon() {
  return (
    <>
      <style>{`@keyframes spin { to { transform: rotate(360deg); } }`}</style>
      <svg
        aria-hidden="true"
        width="14"
        height="14"
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        strokeWidth="2"
        style={{ animation: 'spin 0.7s linear infinite', flexShrink: 0 }}
      >
        <circle cx="12" cy="12" r="10" strokeOpacity="0.25" />
        <path d="M12 2a10 10 0 0 1 10 10" />
      </svg>
    </>
  )
}

// ── Component ─────────────────────────────────────────────────────────────────────────────────────

/**
 * DocumentStatusPage — SCR-010 Document Processing Status.
 *
 * Patient-only: non-Patient roles see an Unauthorized message (OWASP A01).
 */
export function DocumentStatusPage() {
  const { accessToken, userId, role } = useAuth()
  const navigate                      = useNavigate()

  const [documents,    setDocuments]    = useState<DocumentStatusDto[]>([])
  const [loading,      setLoading]      = useState(true)
  const [loadError,    setLoadError]    = useState<string | null>(null)
  const [retryLoading, setRetryLoading] = useState<Record<string, boolean>>({})
  const [retryError,   setRetryError]   = useState<Record<string, string>>({})
  const [toast,        setToast]        = useState<string | null>(null)

  // Per-document interval IDs (checklist — useRef per document; memory leak prevention)
  const intervalRefs    = useRef<Map<string, ReturnType<typeof setInterval>>>(new Map())
  // Per-document AbortControllers (checklist — stale closure protection; AC-001)
  const abortControllers = useRef<Map<string, AbortController>>(new Map())

  // ── Toast auto-dismiss ──────────────────────────────────────────────────────────────────────────
  useEffect(() => {
    if (!toast) return
    const id = setTimeout(() => setToast(null), 4000)
    return () => clearTimeout(id)
  }, [toast])

  // ── Redirect if not Patient role ────────────────────────────────────────────────────────────────
  useEffect(() => {
    if (role && role !== 'Patient') navigate('/login', { replace: true })
  }, [role, navigate])

  // ── Polling helpers ─────────────────────────────────────────────────────────────────────────────

  function stopPolling(docId: string) {
    const id = intervalRefs.current.get(docId)
    if (id !== undefined) {
      clearInterval(id)
      intervalRefs.current.delete(docId)
    }
    abortControllers.current.get(docId)?.abort()
    abortControllers.current.delete(docId)
  }

  function startPolling(docId: string) {
    stopPolling(docId)  // clear any stale interval first

    const id = setInterval(async () => {
      // Abort any in-flight request for this document before issuing a new one
      abortControllers.current.get(docId)?.abort()
      const controller = new AbortController()
      abortControllers.current.set(docId, controller)

      try {
        const updated = await getDocumentStatus(docId, accessToken!, controller.signal)

        // Do not update state if this request was aborted (stale closure guard; checklist)
        if (controller.signal.aborted) return

        setDocuments(prev => prev.map(d => d.id === docId ? updated : d))

        // Stop polling on terminal state (AC-001 — polling stops when EntitiesExtracted)
        if ((TERMINAL_STATUSES as ReadonlyArray<string>).includes(updated.status)) {
          stopPolling(docId)
        }
      } catch (err) {
        if (err instanceof Error && err.name === 'AbortError') return // aborted — ignore
        // Transient network errors: keep polling (do not stop interval)
      }
    }, 5_000)

    intervalRefs.current.set(docId, id)
  }

  // ── Initial load ────────────────────────────────────────────────────────────────────────────────
  useEffect(() => {
    if (!accessToken) return

    let isMounted = true

    async function loadDocuments() {
      try {
        const docs = await getDocuments(accessToken!)
        if (!isMounted) return
        setDocuments(docs)
        setLoading(false)

        // Start polling for all in-progress documents (AC-001)
        docs.forEach(doc => {
          if (!(TERMINAL_STATUSES as ReadonlyArray<string>).includes(doc.status)) {
            startPolling(doc.id)
          }
        })
      } catch (err) {
        if (!isMounted) return
        const msg = err instanceof DocumentApiError
          ? `Failed to load documents (${err.status})`
          : 'Failed to load documents'
        setLoadError(msg)
        setLoading(false)
      }
    }

    loadDocuments()

    return () => {
      isMounted = false
      // Clear all intervals and abort all in-flight requests on unmount (checklist)
      intervalRefs.current.forEach(id => clearInterval(id))
      intervalRefs.current.clear()
      abortControllers.current.forEach(ctrl => ctrl.abort())
      abortControllers.current.clear()
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [accessToken])

  // ── Retry handler ───────────────────────────────────────────────────────────────────────────────

  async function handleRetry(docId: string) {
    setRetryLoading(prev => ({ ...prev, [docId]: true }))
    setRetryError(prev => ({ ...prev, [docId]: '' }))  // clear previous inline error

    try {
      await retryDocument(docId, accessToken!)

      // Reset status to 'Uploaded' locally; stepper resets to first step (AC-004)
      setDocuments(prev => prev.map(d =>
        d.id === docId
          ? { ...d, status: 'Uploaded' as DocumentStatus }
          : d,
      ))

      // Restart polling now that the document is back in flight (AC-004)
      startPolling(docId)
    } catch (err) {
      if (err instanceof DocumentApiError && err.status === 409) {
        // Edge: document is already complete — show inline message (Edge: 409)
        setRetryError(prev => ({
          ...prev,
          [docId]: 'Document processing is already complete.',
        }))
      } else {
        // Network / unexpected errors — toast (Edge: network error; OWASP A03 — no internal details)
        setToast('Unable to retry. Please check your connection.')
      }
    } finally {
      setRetryLoading(prev => ({ ...prev, [docId]: false }))
    }
  }

  // ── Render ──────────────────────────────────────────────────────────────────────────────────────

  const hasPolling = intervalRefs.current.size > 0

  return (
    <div className={styles.page}>
      <Header />

      {/* Secondary page header with polling indicator (UXR-503) */}
      <div className={styles.pageHeader}>
        <span className={styles.pageHeaderTitle}>Document processing status</span>
        <span
          className={styles.pollingIndicator}
          aria-live="polite"     // polite: does not interrupt screen reader flow (UXR-503; UXR-206)
          aria-atomic="true"
        >
          {hasPolling ? 'Polling every 5s…' : ''}
        </span>
      </div>

      <main className={styles.contentArea} id="main-content">
        <h1 className={styles.pageTitle}>Document processing status</h1>
        <p className={styles.pageSub}>
          AI extraction runs in the background. Status updates automatically every 5 seconds.
        </p>

        {loading && (
          <p style={{ color: 'var(--color-text-secondary)', fontSize: '14px' }}>
            Loading documents…
          </p>
        )}

        {loadError && (
          <div className={styles.loadError} role="alert">
            <ErrorIcon />
            <span>{loadError}</span>
          </div>
        )}

        {!loading && !loadError && (
          <div
            role="list"
            aria-label="Document processing status list"
            className={styles.statusList}
          >
            {documents.length === 0 && (
              <div className={styles.emptyState}>
                No documents uploaded yet.{' '}
                <a
                  href="/documents/upload"
                  style={{ color: 'var(--color-primary)' }}
                >
                  Upload your first document →
                </a>
              </div>
            )}

            {documents.map(doc => {
              const isFailed    = (TERMINAL_FAIL_STATUSES as ReadonlyArray<string>).includes(doc.status)
              const isComplete  = doc.status === 'EntitiesExtracted'
              const isRetrying  = retryLoading[doc.id] ?? false
              const inlineError = retryError[doc.id]   ?? ''
              const displayName = truncateFileName(doc.fileName)

              return (
                <div
                  key={doc.id}
                  role="listitem"
                  aria-label={`${doc.fileName}, ${STATUS_LABELS[doc.status]}`}
                  className={`${styles.statusCard} ${isFailed ? styles.statusCardFailed : ''}`}
                >
                  {/* ── Card header ─────────────────────────────────────────────────────── */}
                  <div className={styles.cardHeader}>
                    <div className={styles.fileInfo}>
                      <div className={styles.fileName} title={doc.fileName}>
                        {displayName}
                      </div>
                      <div className={styles.fileMeta}>
                        Uploaded {new Date(doc.processingStartedAt).toLocaleDateString(undefined, {
                          day: '2-digit', month: 'short', year: 'numeric',
                        })}
                      </div>
                    </div>

                    {/* Status badge (UXR-603; checklist — STATUS_LABELS, never raw API string) */}
                    <span className={`${styles.badge} ${badgeClass(doc.status)}`}>
                      {doc.status !== 'EntitiesExtracted'
                        && !(TERMINAL_FAIL_STATUSES as ReadonlyArray<string>).includes(doc.status)
                        && doc.status !== 'Uploaded'
                        && <SpinnerIcon />
                      }
                      {STATUS_LABELS[doc.status]}
                    </span>
                  </div>

                  {/* ── 5-step pipeline stepper ─────────────────────────────────────────── */}
                  <PipelineStepper status={doc.status} />

                  {/* ── Hint text for in-progress documents ─────────────────────────────── */}
                  {!isComplete && !isFailed && (
                    <p className={styles.inProgressHint}>
                      AI extraction in progress — typically 30–120 seconds.
                    </p>
                  )}

                  {/* ── Retry CTA (AC-003, AC-005; UXR-603 — icon + text + button) ────── */}
                  {isFailed && (
                    <div
                      role="alert"
                      aria-live="assertive"  // assertive: announces failure immediately (UXR-603; WCAG 2.1 SC 4.1.3)
                      className={styles.retryAlert}
                    >
                      <div className={styles.retryAlertRow}>
                        <ErrorIcon />
                        {/* Visible text alongside icon — UXR-603: never color alone */}
                        <p id={`retry-desc-${doc.id}`} className={styles.retryMessage}>
                          Document processing failed. Please try again.
                        </p>
                      </div>

                      <button
                        id="retry-btn"
                        className={styles.retryBtn}
                        aria-describedby={`retry-desc-${doc.id}`}
                        onClick={() => handleRetry(doc.id)}
                        disabled={isRetrying}  // prevents double-submit (checklist)
                      >
                        {isRetrying ? <SpinnerIcon /> : null}
                        {isRetrying ? 'Retrying…' : 'Retry'}
                      </button>

                      {/* Inline 409 message (Edge: complete document; checklist) */}
                      {inlineError && (
                        <p
                          role="status"
                          aria-live="polite"
                          className={styles.retryConflictMessage}
                        >
                          {inlineError}
                        </p>
                      )}
                    </div>
                  )}

                  {/* ── View extracted data link (complete documents) ─────────────────── */}
                  {isComplete && (
                    <a
                      href={`/patients/${userId}`}
                      className={styles.viewLink}
                    >
                      View extracted data →
                    </a>
                  )}
                </div>
              )
            })}
          </div>
        )}
      </main>

      {/* Toast notification (Edge: network error; OWASP A03 — generic message, no internals) */}
      {toast && (
        <div
          role="status"
          aria-live="polite"
          className={styles.toast}
        >
          {toast}
        </div>
      )}
    </div>
  )
}
