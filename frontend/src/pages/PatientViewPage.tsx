/**
 * SCR-014 — 360° Patient View page (us_040; task_002; AC-002, AC-003, AC-004).
 *
 * Loads `GET /api/patients/{id}/summary?page=1&pageSize=20` on mount.
 * Five tabs (Demographics, Intake, Documents, Extracted data, Medical codes) rendered from
 * PatientSummaryDto. Tab bar uses ARIA tabs pattern with roving tabindex and ArrowLeft/ArrowRight
 * keyboard navigation (WCAG 2.1 AA SC 2.1.1, SC 4.1.2).
 *
 * Document pagination re-fetches only `documents` + pagination metadata; stable fields
 * (demographics, entities, activeBookings) are never re-requested on page change
 * (Edge: 100+ documents; performance checklist).
 *
 * Role guard: Patient role → redirects to /intake without rendering PHI (OWASP A01; AC-004).
 */

import { useCallback, useEffect, useRef, useState } from 'react'
import { Link, useNavigate, useParams }             from 'react-router-dom'
import { useAuth }                                   from '../context/AuthContext'
import { getPatientSummary }                        from '../api/patientApi'
import { AiLabel }                                  from '../components/AiLabel'
import { ConflictBanner }                           from '../components/ConflictBanner'
import { ResolveConflictDrawer }                    from '../components/ResolveConflictDrawer'
import { getConfidenceLabel }                       from '../types/patient'
import type {
  PatientSummaryDto,
  EntityDto,
  DocumentSummaryDto,
  ConflictDto,
} from '../types/patient'
import styles from './PatientViewPage.module.css'

// ── Types ─────────────────────────────────────────────────────────────────────────────────────────

type TabId = 'demographics' | 'intake' | 'documents' | 'extracted' | 'codes'

const TABS: { id: TabId; label: string }[] = [
  { id: 'demographics', label: 'Demographics'   },
  { id: 'intake',       label: 'Intake'         },
  { id: 'documents',    label: 'Documents'      },
  { id: 'extracted',    label: 'Extracted data' },
  { id: 'codes',        label: 'Medical codes'  },
]

// ── Date formatters ───────────────────────────────────────────────────────────────────────────────

const DOB_FORMAT: Intl.DateTimeFormatOptions = { day: 'numeric', month: 'long', year: 'numeric' }

function formatDob(iso: string): string {
  const d = new Date(`${iso}T00:00:00`)
  return isNaN(d.getTime()) ? iso : d.toLocaleDateString('en-GB', DOB_FORMAT)
}

function formatUploadDate(iso: string): string {
  const d = new Date(iso)
  return isNaN(d.getTime()) ? iso : d.toLocaleDateString('en-GB', { day: 'numeric', month: 'short', year: 'numeric' })
}

function getInitials(first: string, last: string): string {
  return `${first.charAt(0).toUpperCase()}${last.charAt(0).toUpperCase()}`
}

// ── ConfidenceChip ────────────────────────────────────────────────────────────────────────────────

/**
 * Renders the confidence level as text + SVG icon — text and icon always together,
 * never colour alone (UXR-105; WCAG 2.1 AA SC 1.4.1; AC-003).
 */
function ConfidenceChip({ confidence, lowConfidence }: { confidence: number; lowConfidence: boolean }) {
  const level = getConfidenceLabel(confidence)

  const icon =
    level === 'High'
      ? (
        <svg aria-hidden="true" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
          <polyline points="20 6 9 17 4 12" />
        </svg>
      )
      : (
        <svg aria-hidden="true" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
          <path d="M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z" />
          <line x1="12" y1="9" x2="12" y2="13" />
          <line x1="12" y1="17" x2="12.01" y2="17" />
        </svg>
      )

  const ariaLabel = lowConfidence
    ? `Confidence: Low – unverified — verify before clinical use`
    : `Confidence: ${level}`

  return (
    <span className={styles.confidenceChip} aria-label={ariaLabel}>
      {icon}
      {level}
    </span>
  )
}

// ── EntityCard ────────────────────────────────────────────────────────────────────────────────────

function EntityCard({ entity }: { entity: EntityDto }) {
  return (
    <div className={styles.entityCard}>
      <div className={styles.entityHeader}>
        <span className={styles.entityType}>{entity.type}</span>
        <AiLabel />
      </div>
      <div className={styles.entityValue}>
        {entity.code && <span className={styles.codeMono}>{entity.code} — </span>}
        {entity.value}
      </div>
      {entity.source && (
        <div className={styles.entityMeta}>
          Source: {entity.source}
          {entity.extractedAt ? ` · Extracted ${formatUploadDate(entity.extractedAt)}` : ''}
        </div>
      )}
      <ConfidenceChip confidence={entity.confidence} lowConfidence={entity.lowConfidence} />
    </div>
  )
}

// ── Page ──────────────────────────────────────────────────────────────────────────────────────────

export function PatientViewPage() {
  const { accessToken, role } = useAuth()
  const navigate              = useNavigate()
  const { id }                = useParams<{ id: string }>()

  // ── Role guard (OWASP A01; AC-004) ───────────────────────────────────────────────────────────
  useEffect(() => {
    if (role === 'Patient') navigate('/intake', { replace: true })
  }, [role, navigate])

  const [summary,      setSummary]      = useState<PatientSummaryDto | null>(null)
  const [loadError,    setLoadError]    = useState<string | null>(null)
  const [activeTab,    setActiveTab]    = useState<TabId>('extracted')
  const [docPage,      setDocPage]      = useState(1)
  const [docLoading,   setDocLoading]   = useState(false)
  const [drawerConflict, setDrawerConflict] = useState<ConflictDto | null>(null)

  const tabRefs = useRef<Record<TabId, HTMLButtonElement | null>>({
    demographics: null,
    intake:       null,
    documents:    null,
    extracted:    null,
    codes:        null,
  })

  // ── Initial load ──────────────────────────────────────────────────────────────────────────────
  useEffect(() => {
    if (!id || role === 'Patient') return

    getPatientSummary(accessToken ?? '', id, 1, 20)
      .then(data => {
        setSummary(data)
        setDocPage(data.currentPage)
      })
      .catch(err => setLoadError((err as Error).message))
  }, [id, accessToken, role])

  // ── Document page navigation (Edge: 100+ docs; checklist — only doc fields updated) ──────────
  const loadDocPage = useCallback((page: number) => {
    if (!id || !summary) return

    setDocLoading(true)
    getPatientSummary(accessToken ?? '', id, page, 20)
      .then(data => {
        // Replace only documents + pagination metadata — demographics/entities untouched
        setSummary(prev => prev ? {
          ...prev,
          documents:          data.documents,
          totalDocumentCount: data.totalDocumentCount,
          currentPage:        data.currentPage,
          totalPages:         data.totalPages,
        } : prev)
        setDocPage(data.currentPage)
        setDocLoading(false)
      })
      .catch(() => setDocLoading(false))
  }, [id, accessToken, summary])

  // ── Conflict resolved handler (AC-004) ─────────────────────────────────────────────────────────
  // Removes the resolved/dismissed conflict from in-memory PatientSummaryDto.conflicts.
  // No re-fetch needed — the next full page load will return only Open conflicts from the API.
  const handleConflictResolved = useCallback((conflictId: string) => {
    setSummary(prev => prev
      ? { ...prev, conflicts: prev.conflicts.filter((c: ConflictDto) => c.id !== conflictId) }
      : prev
    )
  }, [])

  // ── Tab keyboard navigation (WCAG 2.1 AA SC 2.1.1 — roving tabindex + arrow keys) ───────────
  function handleTabKeyDown(e: React.KeyboardEvent, index: number) {
    const last = TABS.length - 1

    if (e.key === 'ArrowRight') {
      e.preventDefault()
      const next = index < last ? index + 1 : 0
      const nextId = TABS[next].id
      setActiveTab(nextId)
      tabRefs.current[nextId]?.focus()
    }

    if (e.key === 'ArrowLeft') {
      e.preventDefault()
      const prev = index > 0 ? index - 1 : last
      const prevId = TABS[prev].id
      setActiveTab(prevId)
      tabRefs.current[prevId]?.focus()
    }
  }

  if (role === 'Patient') return null

  // ── Loading / error ───────────────────────────────────────────────────────────────────────────
  if (loadError) {
    return (
      <div className={styles.page}>
        <div className={styles.contentArea}>
          <div className={styles.errorBanner} role="alert">
            Failed to load patient: {loadError}
          </div>
          <Link to="/patients/search" className={styles.backLink} style={{ marginTop: 'var(--space-4)', display: 'inline-flex' }}>
            ← Back to search
          </Link>
        </div>
      </div>
    )
  }

  if (!summary) {
    return (
      <div className={styles.page}>
        <div className={styles.contentArea}>
          <p className={styles.loadingState} aria-live="polite">Loading patient record…</p>
        </div>
      </div>
    )
  }

  const { demographics, entities, documents } = summary
  const initials   = getInitials(demographics.firstName, demographics.lastName)
  const dobDisplay = formatDob(demographics.dateOfBirth)

  const totalDocs = summary.totalDocumentCount
  const fromDoc   = totalDocs === 0 ? 0 : (docPage - 1) * 20 + 1
  const toDoc     = Math.min(docPage * 20, totalDocs)

  return (
    <div className={styles.page}>

      {/* ── Patient header ──────────────────────────────────────────────────────────────── */}
      <div className={styles.patientHeader}>
        <div className={styles.patientIdRow}>
          <Link
            to="/patients/search"
            className={styles.backLink}
            aria-label="Back to patient search"
          >
            ← Back
          </Link>
          <div className={styles.avatarLg} aria-hidden="true">{initials}</div>
          <div>
            <div className={styles.patientName}>{demographics.firstName} {demographics.lastName}</div>
            <div className={styles.patientMetaRow}>
              <span className={styles.patientMetaItem}>DOB: {dobDisplay}</span>
              <span className={styles.patientMetaItem}>·</span>
              <span className={styles.patientMetaItem}>{demographics.insurance}</span>
              <span className={styles.patientMetaItem}>·</span>
              <span className={styles.patientCode}>{demographics.patientCode}</span>
            </div>
          </div>
        </div>

        <Link
          to={`/medical-codes/${demographics.id}`}
          id="review-codes-link"
          className={styles.reviewCodesBtn}
        >
          Review codes →
        </Link>
      </div>

      {/* ── Tab bar ─────────────────────────────────────────────────────────────────────── */}
      <div
        className={styles.tabsBar}
        role="tablist"
        aria-label="Patient record sections"
      >
        {TABS.map((tab, index) => (
          <button
            key={tab.id}
            id={`tab-${tab.id}`}
            role="tab"
            aria-selected={activeTab === tab.id}
            aria-controls={`panel-${tab.id}`}
            tabIndex={activeTab === tab.id ? 0 : -1}
            className={`${styles.tab}${activeTab === tab.id ? ` ${styles.tabActive}` : ''}`}
            ref={el => { tabRefs.current[tab.id] = el }}
            onClick={() => setActiveTab(tab.id)}
            onKeyDown={e => handleTabKeyDown(e, index)}
          >
            {tab.label}
          </button>
        ))}
      </div>

      {/* ── Tab panels ──────────────────────────────────────────────────────────────────── */}
      <main className={styles.contentArea} id="main-content">

        {/* DEMOGRAPHICS */}
        <div
          id="panel-demographics"
          role="tabpanel"
          aria-labelledby="tab-demographics"
          hidden={activeTab !== 'demographics'}
        >
          <DemographicsPanel demographics={demographics} dobDisplay={dobDisplay} />
        </div>

        {/* INTAKE */}
        <div
          id="panel-intake"
          role="tabpanel"
          aria-labelledby="tab-intake"
          hidden={activeTab !== 'intake'}
        >
          <IntakePanel demographics={demographics} />
        </div>

        {/* DOCUMENTS */}
        <div
          id="panel-documents"
          role="tabpanel"
          aria-labelledby="tab-documents"
          hidden={activeTab !== 'documents'}
        >
          <DocumentsPanel
            documents={documents}
            totalCount={totalDocs}
            fromDoc={fromDoc}
            toDoc={toDoc}
            currentPage={docPage}
            totalPages={summary.totalPages}
            loading={docLoading}
            onPageChange={loadDocPage}
          />
        </div>

        {/* EXTRACTED DATA */}
        <div
          id="panel-extracted"
          role="tabpanel"
          aria-labelledby="tab-extracted"
          hidden={activeTab !== 'extracted'}
        >
          <ExtractedPanel
            entities={entities}
            conflicts={summary.conflicts ?? []}
            patientId={id ?? ''}
            onResolveConflict={(c) => setDrawerConflict(c)}
          />
        </div>

        {/* MEDICAL CODES */}
        <div
          id="panel-codes"
          role="tabpanel"
          aria-labelledby="tab-codes"
          hidden={activeTab !== 'codes'}
        >
          <MedicalCodesPanel patientId={demographics.id} />
        </div>

      </main>

      {/* MOD-005 Resolve Conflict Drawer (us_042/AC-001–AC-004) */}
      <ResolveConflictDrawer
        open={drawerConflict !== null}
        conflict={drawerConflict}
        patientId={id ?? ''}
        onClose={() => setDrawerConflict(null)}
        onResolved={handleConflictResolved}
      />

    </div>
  )
}

// ── Panel sub-components ──────────────────────────────────────────────────────────────────────────

function DemographicsPanel({
  demographics,
  dobDisplay,
}: {
  demographics: PatientSummaryDto['demographics']
  dobDisplay:   string
}) {
  return (
    <>
      <div className={styles.sectionTitle}>Personal information</div>
      <table className={styles.dataTable}>
        <tbody>
          <tr>
            <td className={styles.dataTableLabelCell}>Full name</td>
            <td style={{ fontWeight: 500 }}>{demographics.firstName} {demographics.lastName}</td>
          </tr>
          <tr>
            <td className={styles.dataTableLabelCell}>Date of birth</td>
            <td>{dobDisplay}</td>
          </tr>
          <tr>
            <td className={styles.dataTableLabelCell}>Email</td>
            <td>{demographics.email}</td>
          </tr>
          <tr>
            <td className={styles.dataTableLabelCell}>Phone</td>
            <td>{demographics.phone}</td>
          </tr>
          <tr>
            <td className={styles.dataTableLabelCell}>Insurance</td>
            <td>
              {demographics.insurance}{' '}
              {demographics.patientCode && (
                <span className={styles.codeMono}>· {demographics.patientCode}</span>
              )}
            </td>
          </tr>
        </tbody>
      </table>
    </>
  )
}

function IntakePanel({ demographics }: { demographics: PatientSummaryDto['demographics'] }) {
  if (!demographics.intake) {
    return <p className={styles.emptyState}>No intake form submitted.</p>
  }

  const { smoking, alcohol, exercise, chiefComplaint, submittedAt } = demographics.intake
  const submittedDisplay = formatUploadDate(submittedAt)

  return (
    <>
      <div className={styles.sectionTitle}>Intake summary</div>
      <table className={styles.dataTable}>
        <tbody>
          <tr><td className={styles.dataTableLabelCell}>Smoking</td><td>{smoking}</td></tr>
          <tr><td className={styles.dataTableLabelCell}>Alcohol</td><td>{alcohol}</td></tr>
          <tr><td className={styles.dataTableLabelCell}>Exercise</td><td>{exercise}</td></tr>
          <tr><td className={styles.dataTableLabelCell}>Chief complaint</td><td>{chiefComplaint}</td></tr>
          <tr><td className={styles.dataTableLabelCell}>Submitted</td><td>{submittedDisplay}</td></tr>
        </tbody>
      </table>
    </>
  )
}

function DocumentsPanel({
  documents,
  totalCount,
  fromDoc,
  toDoc,
  currentPage,
  totalPages,
  loading,
  onPageChange,
}: {
  documents:   DocumentSummaryDto[]
  totalCount:  number
  fromDoc:     number
  toDoc:       number
  currentPage: number
  totalPages:  number
  loading:     boolean
  onPageChange: (page: number) => void
}) {
  return (
    <>
      <div className={styles.sectionTitle}>Uploaded documents</div>

      {documents.length === 0 && !loading ? (
        <p className={styles.emptyState}>No documents uploaded yet.</p>
      ) : (
        <>
          <table
            className={styles.dataTable}
            aria-label="Patient documents"
          >
            <caption style={{ textAlign: 'left', marginBottom: 'var(--space-3)', fontSize: '13px', color: 'var(--color-text-secondary)' }}>
              {totalCount > 0
                ? `Showing ${fromDoc}–${toDoc} of ${totalCount} document${totalCount !== 1 ? 's' : ''}`
                : 'No documents'}
            </caption>
            <thead>
              <tr>
                <th>Document</th>
                <th>Uploaded</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              {documents.map(doc => (
                <tr key={doc.documentId}>
                  <td>{doc.fileName}</td>
                  <td>{formatUploadDate(doc.uploadedAt)}</td>
                  <td>{doc.status}</td>
                </tr>
              ))}
            </tbody>
          </table>

          {totalPages > 1 && (
            <div className={styles.docPagination}>
              <span>Page {currentPage} of {totalPages}</span>
              <div className={styles.paginationControls}>
                <button
                  className={styles.paginationBtn}
                  disabled={currentPage <= 1 || loading}
                  onClick={() => onPageChange(currentPage - 1)}
                  aria-label="Previous page of documents"
                >
                  Prev
                </button>
                <button
                  className={styles.paginationBtn}
                  disabled={currentPage >= totalPages || loading}
                  onClick={() => onPageChange(currentPage + 1)}
                  aria-label="Next page of documents"
                >
                  Next
                </button>
              </div>
            </div>
          )}
        </>
      )}
    </>
  )
}

function ExtractedPanel({
  entities,
  conflicts,
  patientId,
  onResolveConflict,
}: {
  entities:          EntityDto[]
  conflicts:         ConflictDto[]
  patientId:         string
  onResolveConflict: (conflict: ConflictDto) => void
}) {
  const openConflicts = conflicts.filter(c => c.status === 'Open')

  if (entities.length === 0 && openConflicts.length === 0) {
    return (
      <p className={styles.emptyState}>No clinical entities extracted yet.</p>
    )
  }

  return (
    <>
      {/* Conflict banners above entity cards — matching wireframe order (AC-003; checklist) */}
      {openConflicts.map(c => (
        <ConflictBanner
          key={c.id}
          conflict={c}
          patientId={patientId}
          onResolve={onResolveConflict}
        />
      ))}

      {entities.length > 0 && (
        <>
          <div className={styles.sectionTitle}>Extracted entities ({entities.length})</div>

          {/* AI disclaimer (UXR-402) */}
          <div className={styles.aiDisclaimer}>
            <AiLabel />
            <span>All entities extracted by AI from uploaded documents. Verify before clinical use.</span>
          </div>

          {entities.map((entity, i) => (
            <EntityCard key={i} entity={entity} />
          ))}
        </>
      )}
    </>
  )
}

function MedicalCodesPanel({ patientId }: { patientId: string }) {
  return (
    <>
      <p style={{ fontSize: '14px', color: 'var(--color-text-secondary)', marginBottom: 'var(--space-4)' }}>
        Medical code review is available in the next section.
      </p>
      <Link
        to={`/medical-codes/${patientId}`}
        id="review-codes-link"
        className={styles.codeReviewLink}
      >
        Review codes →
      </Link>
    </>
  )
}
