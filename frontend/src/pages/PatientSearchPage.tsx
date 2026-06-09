/**
 * SCR-013 — Patient Search page (us_040; task_002; AC-001, AC-004).
 *
 * Debounced input (300ms) fires `GET /api/patients/search?q=` when value ≥ 3 chars.
 * Each new debounce cycle creates a fresh AbortController and aborts the prior request
 * so stale results from a slower request cannot overwrite newer ones (checklist; AC-001).
 * Role guard: Patient role is redirected to /intake (OWASP A01; AC-004).
 * aria-live="polite" on results region announces result count to screen readers (UXR-206).
 */

import { useEffect, useRef, useState } from 'react'
import { useNavigate, Link }           from 'react-router-dom'
import { useAuth }                     from '../context/AuthContext'
import { searchPatients }              from '../api/patientApi'
import { StaffSidebar }               from '../components/layout/StaffSidebar'
import type { PatientSearchResultDto } from '../types/patient'

// ── Date formatter ────────────────────────────────────────────────────────────────────────────────

const DATE_FORMAT: Intl.DateTimeFormatOptions = { day: '2-digit', month: 'short', year: 'numeric' }

function formatDob(iso: string): string {
  const d = new Date(`${iso}T00:00:00`)
  return isNaN(d.getTime()) ? iso : d.toLocaleDateString('en-GB', DATE_FORMAT)
}

// ── Page ──────────────────────────────────────────────────────────────────────────────────────────

export function PatientSearchPage() {
  const { accessToken, role } = useAuth()
  const navigate              = useNavigate()

  // ── Role guard (OWASP A01; AC-004) ───────────────────────────────────────────────────────────
  useEffect(() => {
    if (role === 'Patient') navigate('/intake', { replace: true })
  }, [role, navigate])

  const [query,   setQuery]   = useState('')
  const [results, setResults] = useState<PatientSearchResultDto[]>([])
  const [loading, setLoading] = useState(false)
  const [error,   setError]   = useState<string | null>(null)

  const debounceTimer  = useRef<ReturnType<typeof setTimeout> | null>(null)
  const abortController = useRef<AbortController | null>(null)

  // ── Debounced search ──────────────────────────────────────────────────────────────────────────
  useEffect(() => {
    if (debounceTimer.current) clearTimeout(debounceTimer.current)

    if (query.length < 3) {
      setResults([])
      setError(null)
      return
    }

    debounceTimer.current = setTimeout(() => {
      // Abort any in-flight request before starting a new one (checklist; AC-001)
      abortController.current?.abort()
      abortController.current = new AbortController()

      const signal = abortController.current.signal

      setLoading(true)
      setError(null)

      searchPatients(accessToken ?? '', query, signal)
        .then(data => {
          setResults(data)
          setLoading(false)
        })
        .catch(err => {
          if ((err as Error).name === 'AbortError') return  // superseded request — ignore
          setError('Search failed. Please try again.')
          setLoading(false)
        })
    }, 300)

    return () => {
      if (debounceTimer.current) clearTimeout(debounceTimer.current)
    }
  }, [query, accessToken])

  if (role === 'Patient') return null

  const resultCount = results.length
  const countLabel  = loading
    ? 'Searching…'
    : query.length >= 3
      ? `${resultCount} result${resultCount !== 1 ? 's' : ''} found`
      : ''

  return (
    <div style={{ minHeight: '100vh', background: 'var(--color-bg-page)', display: 'flex' }}>
      <StaffSidebar />

      <div style={{ flex: 1, minWidth: 0, display: 'flex', flexDirection: 'column' }}>

      {/* ── Search form ───────────────────────────────────────────────────────────────────── */}
      <main
        style={{
          maxWidth: '720px',
          margin:   '0 auto',
          padding:  'var(--space-10) var(--space-6)',
        }}
      >
        <h1 style={{ fontSize: '28px', fontWeight: 700, marginBottom: 'var(--space-2)' }}>
          Find a patient
        </h1>
        <p style={{ fontSize: '14px', color: 'var(--color-text-secondary)', marginBottom: 'var(--space-6)' }}>
          Search by name or patient ID. Enter at least 3 characters.
        </p>

        <div style={{ position: 'relative', marginBottom: 'var(--space-4)' }}>
          <input
            type="search"
            aria-label="Search patients"
            aria-autocomplete="list"
            aria-controls="search-results"
            value={query}
            onChange={e => setQuery(e.target.value)}
            placeholder="e.g. Sarah Mitchell or PAT-00001"
            style={{
              width:        '100%',
              padding:      'var(--space-3) var(--space-4)',
              fontSize:     '15px',
              border:       '1px solid var(--color-border)',
              borderRadius: 'var(--radius-md)',
              background:   'var(--color-bg-surface)',
              color:        'var(--color-text-primary)',
              outline:      'none',
              fontFamily:   'var(--font-sans)',
              minHeight:    '44px',
              boxSizing:    'border-box',
            }}
            onFocus={e => (e.target.style.borderColor = 'var(--color-border-focus)')}
            onBlur={e  => (e.target.style.borderColor = 'var(--color-border)')}
          />
        </div>

        {/* ── Live result count (UXR-206 — screen reader announcement) ──────────────────── */}
        <span
          aria-live="polite"
          style={{
            display:  'block',
            fontSize: '13px',
            color:    'var(--color-text-secondary)',
            minHeight: '20px',
            marginBottom: 'var(--space-3)',
          }}
        >
          {countLabel}
        </span>

        {error && (
          <div
            role="alert"
            style={{
              background:   'var(--color-error-surface)',
              border:       '1px solid var(--color-error-border)',
              borderRadius: 'var(--radius-md)',
              padding:      'var(--space-3) var(--space-4)',
              fontSize:     '14px',
              color:        'var(--color-status-error)',
              marginBottom: 'var(--space-4)',
            }}
          >
            {error}
          </div>
        )}

        {/* ── Results list ──────────────────────────────────────────────────────────────── */}
        <ul
          id="search-results"
          role="listbox"
          aria-label="Patient search results"
          style={{
            listStyle: 'none',
            padding:   0,
            margin:    0,
          }}
        >
          {results.map(r => (
            <li
              key={r.id}
              role="option"
              aria-selected="false"
              style={{
                background:    'var(--color-bg-surface)',
                border:        '1px solid var(--color-border)',
                borderRadius:  'var(--radius-md)',
                padding:       'var(--space-4)',
                marginBottom:  'var(--space-2)',
                cursor:        'pointer',
                display:       'flex',
                justifyContent: 'space-between',
                alignItems:    'center',
              }}
              onClick={() => navigate(`/patients/${r.id}/view`)}
              onKeyDown={e => {
                if (e.key === 'Enter' || e.key === ' ') {
                  e.preventDefault()
                  navigate(`/patients/${r.id}/view`)
                }
              }}
              tabIndex={0}
            >
              <div>
                <div style={{ fontWeight: 600, fontSize: '15px' }}>{r.fullName}</div>
                <div style={{ fontSize: '13px', color: 'var(--color-text-secondary)', marginTop: '2px' }}>
                  DOB: {formatDob(r.dateOfBirth)}
                </div>
              </div>
              <span style={{ fontFamily: 'var(--font-mono)', fontSize: '13px', color: 'var(--color-text-secondary)' }}>
                {r.patientCode}
              </span>
            </li>
          ))}
        </ul>

        {!loading && query.length >= 3 && results.length === 0 && !error && (
          <p style={{ fontSize: '14px', color: 'var(--color-text-secondary)', marginTop: 'var(--space-4)' }}>
            No patients found for "{query}".
          </p>
        )}

        {query.length < 3 && query.length > 0 && (
          <p style={{ fontSize: '13px', color: 'var(--color-text-secondary)' }}>
            Enter at least 3 characters to search.
          </p>
        )}
      </main>

      </div>
    </div>
  )
}
