import { useEffect, useRef, useState } from 'react'
import { searchPatients, type PatientSearchResult } from '../../api/walkInApi'

// ── Icons ─────────────────────────────────────────────────────────────────────────────────────────

function SearchIcon() {
  return (
    <svg aria-hidden="true" width="16" height="16" viewBox="0 0 20 20" fill="none">
      <circle cx="8.5" cy="8.5" r="5.5" stroke="currentColor" strokeWidth="1.5" />
      <path d="M13 13l3.5 3.5" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
    </svg>
  )
}

function UserPlusIcon() {
  return (
    <svg aria-hidden="true" width="16" height="16" viewBox="0 0 20 20" fill="none">
      <circle cx="8" cy="7" r="4" stroke="currentColor" strokeWidth="1.5" />
      <path d="M1 17c0-3.314 3.134-6 7-6" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
      <path d="M15 12v6M12 15h6" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
    </svg>
  )
}

// ── Props ─────────────────────────────────────────────────────────────────────────────────────────

interface PatientTypeaheadProps {
  accessToken: string
  /** Called when a patient row is selected (or null when selection is cleared). */
  onSelect: (patient: PatientSearchResult | null) => void
  /** Called when "Create New Patient" is clicked. */
  onCreateNew: () => void
}

/**
 * PatientTypeahead (AC-002; UXR-105)
 *
 * Debounced search input that fires `GET /api/patients/search?q=` after 300ms when the
 * query has at least 3 characters. Implements ARIA combobox pattern for accessibility.
 *
 * After a patient is selected, displays a confirmation row with name + DOB.
 * "Create New Patient" option at the bottom of the listbox opens MOD-004 (AC-004).
 */
export function PatientTypeahead({ accessToken, onSelect, onCreateNew }: PatientTypeaheadProps) {
  const [query, setQuery] = useState('')
  const [results, setResults] = useState<PatientSearchResult[]>([])
  const [isOpen, setIsOpen] = useState(false)
  const [isLoading, setIsLoading] = useState(false)
  const [selected, setSelected] = useState<PatientSearchResult | null>(null)
  const [activeIndex, setActiveIndex] = useState(-1)
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null)
  const listboxId = 'patient-typeahead-listbox'
  const inputId   = 'patient-typeahead-input'

  // ── Debounced search (AC-002; OWASP A03: 3-char minimum before DB query) ──────────────────────
  useEffect(() => {
    // Clear pending timer whenever query changes
    if (timerRef.current !== null) clearTimeout(timerRef.current)

    if (query.trim().length < 3) {
      setResults([])
      setIsOpen(false)
      return
    }

    setIsLoading(true)
    // Cleanup returned so stale timer is cancelled on unmount or next effect run
    timerRef.current = setTimeout(async () => {
      try {
        const data = await searchPatients(accessToken, query.trim())
        setResults(data)
        setIsOpen(true)
        setActiveIndex(-1)
      } catch {
        setResults([])
      } finally {
        setIsLoading(false)
      }
    }, 300)

    return () => {
      if (timerRef.current !== null) clearTimeout(timerRef.current)
    }
  }, [query, accessToken])

  function selectPatient(patient: PatientSearchResult) {
    setSelected(patient)
    setQuery(`${patient.firstName} ${patient.lastName}`)
    setIsOpen(false)
    onSelect(patient)
  }

  function clearSelection() {
    setSelected(null)
    setQuery('')
    setIsOpen(false)
    onSelect(null)
  }

  function handleKeyDown(e: React.KeyboardEvent<HTMLInputElement>) {
    const itemCount = results.length + 1 // +1 for "Create New Patient"
    if (e.key === 'ArrowDown') {
      e.preventDefault()
      setActiveIndex(i => Math.min(i + 1, itemCount - 1))
    } else if (e.key === 'ArrowUp') {
      e.preventDefault()
      setActiveIndex(i => Math.max(i - 1, 0))
    } else if (e.key === 'Enter' && isOpen) {
      e.preventDefault()
      if (activeIndex >= 0 && activeIndex < results.length) {
        selectPatient(results[activeIndex])
      } else if (activeIndex === results.length) {
        onCreateNew()
        setIsOpen(false)
      }
    } else if (e.key === 'Escape') {
      setIsOpen(false)
    }
  }

  return (
    <div className="typeahead-root">
      {/* Input — ARIA combobox (AC-002; https://www.w3.org/WAI/ARIA/apg/patterns/combobox/) */}
      <div className="typeahead-input-wrap">
        <span className="typeahead-input-icon" aria-hidden="true"><SearchIcon /></span>
        <input
          id={inputId}
          type="text"
          role="combobox"
          aria-expanded={isOpen}
          aria-controls={listboxId}
          aria-autocomplete="list"
          aria-activedescendant={activeIndex >= 0 ? `typeahead-option-${activeIndex}` : undefined}
          aria-label="Search patients by name"
          className="field-input typeahead-input"
          placeholder="Type 3+ characters to search…"
          value={query}
          autoComplete="off"
          onChange={e => {
            setQuery(e.target.value)
            if (selected) { setSelected(null); onSelect(null) }
          }}
          onKeyDown={handleKeyDown}
          onBlur={() => setTimeout(() => setIsOpen(false), 150)}
          onFocus={() => { if (results.length > 0 && query.length >= 3) setIsOpen(true) }}
        />
        {isLoading && (
          <span className="typeahead-loading" aria-live="polite" aria-label="Searching…">
            <span className="typeahead-spinner" aria-hidden="true" />
          </span>
        )}
      </div>

      {/* Listbox dropdown */}
      {isOpen && (
        <ul
          id={listboxId}
          role="listbox"
          aria-label="Patient search results"
          className="typeahead-listbox"
        >
          {results.length === 0 ? (
            <li role="option" aria-selected="false" className="typeahead-option typeahead-option--empty">
              No patients found
            </li>
          ) : (
            results.map((p, i) => (
              <li
                key={p.patientId}
                id={`typeahead-option-${i}`}
                role="option"
                aria-selected={activeIndex === i}
                className={`typeahead-option${activeIndex === i ? ' typeahead-option--active' : ''}`}
                onMouseDown={() => selectPatient(p)}
              >
                <span className="typeahead-option__name">{p.firstName} {p.lastName}</span>
                {p.dateOfBirth && (
                  <span className="typeahead-option__dob">DOB: {p.dateOfBirth}</span>
                )}
              </li>
            ))
          )}

          {/* "Create New Patient" — always shown as the last option (AC-004) */}
          <li
            id={`typeahead-option-${results.length}`}
            role="option"
            aria-selected={activeIndex === results.length}
            className={`typeahead-option typeahead-option--create${activeIndex === results.length ? ' typeahead-option--active' : ''}`}
            onMouseDown={() => { onCreateNew(); setIsOpen(false) }}
          >
            <UserPlusIcon />
            <span>Create New Patient</span>
          </li>
        </ul>
      )}

      {/* Confirmation row — shown when a patient is selected (AC-002) */}
      {selected && (
        <div className="typeahead-confirmation" role="status" aria-live="polite">
          <div className="typeahead-confirmation__info">
            <span className="typeahead-confirmation__name">
              {selected.firstName} {selected.lastName}
            </span>
            {selected.dateOfBirth && (
              <span className="typeahead-confirmation__dob">DOB: {selected.dateOfBirth}</span>
            )}
          </div>
          <button
            type="button"
            className="typeahead-confirmation__clear"
            aria-label="Clear patient selection"
            onClick={clearSelection}
          >
            ✕
          </button>
        </div>
      )}
    </div>
  )
}
