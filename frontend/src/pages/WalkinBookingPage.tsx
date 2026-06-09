import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../context/AuthContext'
import { getSlots, type SlotDto } from '../api/slotsApi'
import {
  createWalkinBooking,
  DuplicateTodayError,
  SlotUnavailableError,
  BookingLockTimeoutError,
  type PatientSearchResult,
  type WalkinPatientResponse,
} from '../api/walkInApi'
import { PatientTypeahead } from '../components/walkin/PatientTypeahead'
import { WalkinAccountCreationModal } from '../components/walkin/WalkinAccountCreationModal'
import { StaffSidebar } from '../components/layout/StaffSidebar'
import '../features/walkin/walkin.css'

// ── Icons ─────────────────────────────────────────────────────────────────────────────────────────

function WarningIcon() {
  return (
    <svg aria-hidden="true" width="16" height="16" viewBox="0 0 20 20" fill="none">
      <path d="M10 3L17.794 17H2.206L10 3Z" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
      <path d="M10 9v3.5" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
      <circle cx="10" cy="14.5" r="0.75" fill="currentColor" />
    </svg>
  )
}

function InfoIcon() {
  return (
    <svg aria-hidden="true" width="16" height="16" viewBox="0 0 20 20" fill="none">
      <circle cx="10" cy="10" r="8" stroke="currentColor" strokeWidth="1.5" />
      <path d="M10 9v5" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
      <circle cx="10" cy="6.5" r="0.75" fill="currentColor" />
    </svg>
  )
}

/** Normal priority — neutral circle icon */
function NormalIcon() {
  return (
    <svg aria-hidden="true" width="16" height="16" viewBox="0 0 20 20" fill="none">
      <circle cx="10" cy="10" r="7" stroke="currentColor" strokeWidth="1.5" />
    </svg>
  )
}

/** Urgent priority — filled exclamation icon */
function UrgentIcon() {
  return (
    <svg aria-hidden="true" width="16" height="16" viewBox="0 0 20 20" fill="currentColor">
      <path d="M10 2a8 8 0 100 16A8 8 0 0010 2zM9 6h2v6H9V6zm0 8h2v2H9v-2z" />
    </svg>
  )
}

// ── Helpers ───────────────────────────────────────────────────────────────────────────────────────

function todayIso() {
  return new Date().toISOString().slice(0, 10)   // "yyyy-MM-dd"
}

function formatSlotLabel(slot: SlotDto) {
  const [h, m] = slot.startTime.split(':')
  const hour   = parseInt(h, 10)
  const ampm   = hour < 12 ? 'AM' : 'PM'
  const h12    = hour === 0 ? 12 : hour > 12 ? hour - 12 : hour
  const label  = `${h12}:${m} ${ampm}`
  return slot.providerName ? `${label} — ${slot.providerName}` : label
}

// ── Main component ────────────────────────────────────────────────────────────────────────────────

/**
 * WalkinBookingPage (SCR-012) — Staff/Admin walk-in booking form (us_030; AC-001–AC-004).
 *
 * Role guard: Patients are redirected to /queue immediately (OWASP A01; AC-001).
 * Today's available slots are fetched on mount and filtered client-side.
 */
export function WalkinBookingPage() {
  const { accessToken, role } = useAuth()
  const navigate = useNavigate()

  // ── Role guard (OWASP A01; AC-001) ───────────────────────────────────────────────────────────
  useEffect(() => {
    if (role === 'Patient') navigate('/queue', { replace: true })
  }, [role, navigate])

  // ── Today's slots ─────────────────────────────────────────────────────────────────────────────
  const [slots, setSlots]           = useState<SlotDto[]>([])
  const [slotsLoading, setSlotsLoading] = useState(true)

  useEffect(() => {
    if (!accessToken) return
    async function fetchTodaySlots() {
      setSlotsLoading(true)
      try {
        // Fetch today's available slots server-side using the date param so we don't rely on
        // client-side filtering of a fixed page — avoids missing slots when total count > pageSize
        const res = await getSlots(accessToken!, { page: 1, pageSize: 100, date: todayIso() })
        setSlots(res.slots)
      } catch {
        setSlots([])
      } finally {
        setSlotsLoading(false)
      }
    }
    void fetchTodaySlots()
  }, [accessToken])

  // ── Form state ────────────────────────────────────────────────────────────────────────────────
  const [selectedPatient,   setSelectedPatient]   = useState<PatientSearchResult | null>(null)
  const [slotId,            setSlotId]            = useState<number | ''>('')
  const [reason,            setReason]            = useState('')
  const [priority,          setPriority]          = useState<'Normal' | 'Urgent'>('Normal')
  const [phone,             setPhone]             = useState('')
  const [insuranceProvider, setInsuranceProvider] = useState('')
  const [isModalOpen,       setIsModalOpen]       = useState(false)

  // ── Error / status state ──────────────────────────────────────────────────────────────────────
  const [fieldErrors,         setFieldErrors]         = useState<Record<string, string>>({})
  const [duplicateBookingId,  setDuplicateBookingId]  = useState<number | null>(null)
  const [apiError,            setApiError]            = useState<string | null>(null)
  const [submitting,          setSubmitting]          = useState(false)

  // ── Patient creation callback (AC-004) ───────────────────────────────────────────────────────
  function handlePatientCreated(patient: WalkinPatientResponse) {
    // Convert WalkinPatientResponse to PatientSearchResult shape for the typeahead confirmation row
    setSelectedPatient({
      patientId:   patient.patientId,
      firstName:   patient.firstName,
      lastName:    patient.lastName,
      dateOfBirth: null,
    })
  }

  // ── Submit ────────────────────────────────────────────────────────────────────────────────────
  async function handleSubmit(overrideDuplicate = false) {
    // Client-side boundary validation (OWASP A03; AC-001; checklist)
    const errors: Record<string, string> = {}
    if (!selectedPatient)    errors.patient = 'Please select or create a patient.'
    if (!slotId)             errors.slot    = 'Please select a time slot.'
    if (!reason.trim())      errors.reason  = 'Reason for visit is required.'
    if (Object.keys(errors).length) { setFieldErrors(errors); return }

    setFieldErrors({})
    setApiError(null)
    setSubmitting(true)

    try {
      const res = await createWalkinBooking(accessToken!, {
        patientId:        selectedPatient!.patientId,
        slotId:           slotId as number,
        reasonForVisit:   reason.trim(),
        priority,
        overrideDuplicate,
        phone:             phone.trim()             || undefined,
        insuranceProvider: insuranceProvider.trim() || undefined,
      })
      // AC-003: navigate to queue with queue position in state for toast display
      navigate('/queue', {
        state: { queuePosition: res.queuePosition, bookingId: res.bookingId },
        replace: false,
      })
    } catch (err) {
      if (err instanceof DuplicateTodayError) {
        setDuplicateBookingId(err.existingBookingId)
      } else if (err instanceof SlotUnavailableError) {
        setApiError('The selected slot is no longer available. Please choose another slot.')
      } else if (err instanceof BookingLockTimeoutError) {
        setApiError('The booking system is temporarily busy. Please try again in a moment.')
      } else {
        setApiError(err instanceof Error ? err.message : 'Something went wrong. Please try again.')
      }
    } finally {
      setSubmitting(false)
    }
  }

  // Do not render patient content until role check is complete
  if (role === 'Patient') return null

  const noSlotsToday = !slotsLoading && slots.length === 0

  return (
    <div className="walkin-page" style={{ display: 'flex', minHeight: '100vh' }}>
      <StaffSidebar />

      <main className="walkin-page__content" aria-labelledby="scr012-heading">
        <div className="walkin-form-card">
          <h1 id="scr012-heading" className="walkin-form-card__title">New walk-in booking</h1>
          <p className="walkin-form-card__subtitle">
            Search for an existing patient or create a minimal record, then assign a same-day slot.
          </p>

          {/* ── 1. Patient search ─────────────────────────────────────────────────────────── */}
          <section aria-labelledby="section-patient">
            <h2 id="section-patient" className="section-heading">Patient</h2>

            <div className="form-field">
              <label htmlFor="patient-typeahead-input" className="field-label">
                Search patient <span aria-hidden="true" className="field-required">*</span>
              </label>
              <PatientTypeahead
                accessToken={accessToken!}
                onSelect={p => { setSelectedPatient(p); setFieldErrors(e => ({ ...e, patient: '' })) }}
                onCreateNew={() => setIsModalOpen(true)}
              />
              {fieldErrors.patient && (
                <span role="alert" className="field-error">
                  <WarningIcon /> {fieldErrors.patient}
                </span>
              )}
            </div>
          </section>

          <hr className="section-divider" />

          {/* ── 2. Patient contact ─────────────────────────────────────────────── */}
          <section aria-labelledby="section-contact">
            <h2 id="section-contact" className="section-heading">Patient contact</h2>

            <div className="form-grid form-grid--2col">
              {/* Phone number */}
              <div className="form-field">
                <label htmlFor="phone-input" className="field-label">
                  Phone <span className="field-optional">(optional)</span>
                </label>
                <input
                  id="phone-input"
                  type="tel"
                  autoComplete="tel"
                  className="field-input"
                  placeholder="+1 (555) 000-0000"
                  value={phone}
                  onChange={e => setPhone(e.target.value)}
                />
              </div>

              {/* Insurance provider */}
              <div className="form-field">
                <label htmlFor="insurance-select" className="field-label">
                  Insurance <span className="field-optional">(optional)</span>
                </label>
                <select
                  id="insurance-select"
                  className="field-input field-select"
                  value={insuranceProvider}
                  onChange={e => setInsuranceProvider(e.target.value)}
                >
                  <option value="">Select insurance…</option>
                  <option value="Medicare">Medicare</option>
                  <option value="Medicaid">Medicaid</option>
                  <option value="Blue Cross Blue Shield">Blue Cross Blue Shield</option>
                  <option value="Aetna">Aetna</option>
                  <option value="UnitedHealthcare">UnitedHealthcare</option>
                  <option value="Cigna">Cigna</option>
                  <option value="Self-pay">Self-pay</option>
                  <option value="Other">Other</option>
                </select>
              </div>
            </div>
          </section>

          <hr className="section-divider" />

          {/* ── 3. Slot assignment ──────────────────────────────────────────────────── */}
          <section aria-labelledby="section-slot">
            <h2 id="section-slot" className="section-heading">Appointment slot</h2>

            <div className="form-field">
              <label htmlFor="slot-select" className="field-label">
                Available today <span aria-hidden="true" className="field-required">*</span>
              </label>
              <select
                id="slot-select"
                className={`field-input field-select${fieldErrors.slot ? ' field-input--error' : ''}`}
                value={slotId}
                disabled={noSlotsToday || slotsLoading}
                aria-required="true"
                aria-invalid={!!fieldErrors.slot}
                aria-describedby={noSlotsToday ? 'no-slots-msg' : fieldErrors.slot ? 'slot-err' : undefined}
                onChange={e => { setSlotId(e.target.value ? Number(e.target.value) : ''); setFieldErrors(er => ({ ...er, slot: '' })) }}
              >
                {slotsLoading ? (
                  <option value="">Loading slots…</option>
                ) : noSlotsToday ? (
                  <option value="" disabled>No same-day slots available</option>
                ) : (
                  <>
                    <option value="">— Select a slot —</option>
                    {slots.map(s => (
                      <option key={s.id} value={s.id}>{formatSlotLabel(s)}</option>
                    ))}
                  </>
                )}
              </select>

              {/* No-slots informational message (Edge: no slots; UXR-105) */}
              {noSlotsToday && (
                <p id="no-slots-msg" role="status" className="field-info">
                  <InfoIcon />
                  All same-day slots are full. Consider adding the patient to a wait list.
                </p>
              )}
              {fieldErrors.slot && !noSlotsToday && (
                <span id="slot-err" role="alert" className="field-error">
                  <WarningIcon /> {fieldErrors.slot}
                </span>
              )}
            </div>
          </section>

          <hr className="section-divider" />

          {/* ── 4. Visit details ──────────────────────────────────────────────────────────── */}
          <section aria-labelledby="section-visit">
            <h2 id="section-visit" className="section-heading">Visit details</h2>

            {/* Reason for visit */}
            <div className="form-field">
              <label htmlFor="reason-input" className="field-label">
                Reason for visit <span aria-hidden="true" className="field-required">*</span>
              </label>
              <textarea
                id="reason-input"
                className={`field-input field-textarea${fieldErrors.reason ? ' field-input--error' : ''}`}
                rows={3}
                placeholder="Brief description of the reason for visit…"
                value={reason}
                aria-required="true"
                aria-invalid={!!fieldErrors.reason}
                aria-describedby={fieldErrors.reason ? 'reason-err' : undefined}
                onChange={e => { setReason(e.target.value); setFieldErrors(er => ({ ...er, reason: '' })) }}
              />
              {fieldErrors.reason && (
                <span id="reason-err" role="alert" className="field-error">
                  <WarningIcon /> {fieldErrors.reason}
                </span>
              )}
            </div>

            {/* Priority — icon + text label; not colour alone (UXR-105; WCAG 2.1 A) */}
            <fieldset className="priority-fieldset">
              <legend className="field-label">
                Priority <span aria-hidden="true" className="field-required">*</span>
              </legend>
              <div className="priority-options">
                <label className={`priority-option${priority === 'Normal' ? ' priority-option--selected' : ''}`}>
                  <input
                    type="radio"
                    name="priority"
                    value="Normal"
                    checked={priority === 'Normal'}
                    onChange={() => setPriority('Normal')}
                    className="priority-radio"
                  />
                  <NormalIcon />
                  <span>Normal</span>
                </label>

                <label className={`priority-option priority-option--urgent${priority === 'Urgent' ? ' priority-option--selected' : ''}`}>
                  <input
                    type="radio"
                    name="priority"
                    value="Urgent"
                    checked={priority === 'Urgent'}
                    onChange={() => setPriority('Urgent')}
                    className="priority-radio"
                  />
                  <UrgentIcon />
                  <span>Urgent</span>
                </label>
              </div>
            </fieldset>
          </section>

          {/* ── Duplicate booking banner (Edge: duplicate; UXR-105) ───────────────────────── */}
          {duplicateBookingId !== null && (
            <div role="alert" className="alert-banner alert-banner--warning">
              <WarningIcon />
              <div className="alert-banner__body">
                <strong>This patient already has a confirmed booking today.</strong>
                <button
                  type="button"
                  className="btn btn-secondary"
                  onClick={() => { setDuplicateBookingId(null); void handleSubmit(true) }}
                  disabled={submitting}
                >
                  Override and Proceed
                </button>
              </div>
            </div>
          )}

          {/* ── General API error ─────────────────────────────────────────────────────────── */}
          {apiError && (
            <div role="alert" className="alert-banner alert-banner--error">
              <WarningIcon />
              <span>{apiError}</span>
            </div>
          )}

          {/* ── Actions ───────────────────────────────────────────────────────────────────── */}
          <div className="form-actions">
            <button
              type="button"
              className="btn btn-secondary"
              onClick={() => navigate('/queue')}
              disabled={submitting}
            >
              Cancel
            </button>
            <button
              type="button"
              className="btn btn-primary"
              disabled={submitting || noSlotsToday}
              aria-disabled={submitting || noSlotsToday}
              onClick={() => void handleSubmit(false)}
            >
              {submitting ? 'Saving…' : 'Create walk-in'}
            </button>
          </div>
        </div>
      </main>

      {/* MOD-004 — Minimal patient creation modal (AC-004) */}
      <WalkinAccountCreationModal
        isOpen={isModalOpen}
        accessToken={accessToken!}
        onClose={() => setIsModalOpen(false)}
        onPatientCreated={p => { handlePatientCreated(p); setIsModalOpen(false) }}
      />
    </div>
  )
}
