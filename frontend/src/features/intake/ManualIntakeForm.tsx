import { useState, useEffect, useCallback, useRef } from 'react'
import { useForm, FormProvider } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { useNavigate, useBlocker, useLocation } from 'react-router-dom'
import {
  manualIntakeSchema,
  defaultFormValues,
  FORM_SECTIONS,
  type ManualIntakeFormValues,
  type SectionKey,
} from './manualIntakeSchema'
import { getDraft, postDraft, postManualIntake, type ManualIntakeDraft, type ManualIntakeData } from '../../api/intakeManualApi'
import { switchMode, type ModeSwitchResponse } from '../../api/intakeModeSwitchApi'
import { useAuth } from '../../context/AuthContext'
import { DemographicsSection }  from './sections/DemographicsSection'
import { MedicalHistorySection } from './sections/MedicalHistorySection'
import { MedicationsSection }    from './sections/MedicationsSection'
import { AllergiesSection }      from './sections/AllergiesSection'
import { ChiefComplaintSection } from './sections/ChiefComplaintSection'
import { ErrorIcon }             from './sections/shared'
import { IntakeSwitchButton }   from './IntakeSwitchButton'
import { IntakeReviewBuffer }   from './IntakeReviewBuffer'

// ── Helper: map mode-switch mappedFields → ManualIntakeFormValues ───────────────────────────────

/**
 * Maps mode-switch `mappedFields` (AI→Manual; shape: ManualIntakeDraft) to `ManualIntakeFormValues`
 * so `reset()` can pre-populate the form (AC-001; us_018).
 * Falls back to empty defaults for fields not present in the mapped result
 * (Edge: no data entered; AC-001).
 */
function mappedFieldsToFormValues(fields: ModeSwitchResponse['mappedFields']): ManualIntakeFormValues {
  // mappedFields from AI→Manual has a ManualIntakeDraft-compatible shape
  const f = (fields ?? {}) as ManualIntakeDraft
  return draftToFormValues(f)
}

// ── Helper: map ManualIntakeDraft → ManualIntakeFormValues ────────────────────────────────────────

function draftToFormValues(draft: ManualIntakeDraft): ManualIntakeFormValues {
  return {
    demographics: {
      firstName:   draft.demographics?.firstName   ?? '',
      lastName:    draft.demographics?.lastName    ?? '',
      dateOfBirth: draft.demographics?.dateOfBirth ?? '',
      gender:      draft.demographics?.gender      ?? '',
      phone:       draft.demographics?.phone       ?? '',
      address:     draft.demographics?.address     ?? '',
    },
    chiefComplaint: {
      description: draft.chiefComplaint?.description ?? '',
    },
    medicalHistory: {
      conditions:       (draft.medicalHistory?.conditions ?? []).map(c => ({
        name:          c.name          ?? '',
        diagnosedDate: c.diagnosedDate ?? '',
      })),
      surgeries:        (draft.medicalHistory?.surgeries ?? []).map(s => ({
        name: s.name ?? '',
        date: s.date ?? '',
      })),
      lastPhysicalExam: draft.medicalHistory?.lastPhysicalExam ?? '',
    },
    medications: {
      items: (draft.medications?.items ?? []).map(m => ({
        name:      m.name      ?? '',
        dosage:    m.dosage    ?? '',
        frequency: m.frequency ?? '',
      })),
    },
    allergies: {
      items: (draft.allergies?.items ?? []).map(a => ({
        allergen: a.allergen ?? '',
        reaction: a.reaction ?? '',
      })),
    },
  }
}

// ── Tab error badge ───────────────────────────────────────────────────────────────────────────────

/**
 * Shown on a tab label when the corresponding section has one or more validation errors.
 * Renders icon + text — never colour-only (AC-003; UXR-105; WCAG 1.4.1).
 */
function ErrorBadge() {
  return (
    <span
      aria-label="section has errors"
      style={{
        display:      'inline-flex',
        alignItems:   'center',
        gap:          '2px',
        marginLeft:   'var(--space-1)',
        background:   'var(--color-error-surface)',
        color:        'var(--color-status-error)',
        fontSize:     '10px',
        fontWeight:   600,
        padding:      '1px 5px',
        borderRadius: 'var(--radius-full)',
        border:       '1px solid var(--color-error-border)',
      }}
    >
      <ErrorIcon />
      <span>(errors)</span>
    </span>
  )
}

// ── Toast ─────────────────────────────────────────────────────────────────────────────────────────

function Toast({ message, onDismiss }: { message: string; onDismiss: () => void }) {
  useEffect(() => {
    const t = setTimeout(onDismiss, 3500)
    return () => clearTimeout(t)
  }, [onDismiss])

  return (
    <div
      role="status"
      aria-live="polite"
      style={{
        position:     'fixed',
        bottom:       'var(--space-6)',
        right:        'var(--space-6)',
        background:   'var(--color-text-primary)',
        color:        'var(--color-text-inverse)',
        fontSize:     '13px',
        fontWeight:   500,
        padding:      'var(--space-3) var(--space-5)',
        borderRadius: 'var(--radius-md)',
        boxShadow:    'var(--shadow-3)',
        zIndex:       1000,
      }}
    >
      {message}
    </div>
  )
}

// ── Section component map ─────────────────────────────────────────────────────────────────────────

const SECTION_COMPONENTS: Record<SectionKey, React.FC> = {
  demographics:   DemographicsSection,
  medicalHistory: MedicalHistorySection,
  medications:    MedicationsSection,
  allergies:      AllergiesSection,
  chiefComplaint: ChiefComplaintSection,
}

// ── ManualIntakeForm ──────────────────────────────────────────────────────────────────────────────

/**
 * SCR-005 — Manual intake form (us_017).
 *
 * Lifecycle:
 * 1. On mount: `GET /api/intake/draft` — if draft found, pre-populates all fields via `reset()`
 *    and shows "Resuming your saved draft" banner (AC-005; WCAG 4.1.3).
 * 2. Exactly 5 tabs — Demographics, Medical History, Medications, Allergies, Chief Complaint
 *    (AC-001; UXR-302). Tab navigation is client-side `useState<number>` — no page reload.
 * 3. Tab labels show `<ErrorBadge>` (icon + text) when the section has `formState.errors`
 *    entries — removed once resolved (AC-003; UXR-105; WCAG 1.4.1).
 * 4. Navigation-away guard: `useBlocker` fires when `isDirty && !isSubmitSuccessful`;
 *    `postDraft(getValues())` is awaited before the blocker proceeds; "Draft saved" toast is shown
 *    (AC-004; WCAG 4.1.3).
 * 5. Submit: `trigger()` validates all sections; on failure → active tab jumps to first errored
 *    section without clearing any values; on success → `postManualIntake()` → 201 → navigate to
 *    `/intake/confirmation` (AC-002; AC-003).
 *
 * PHI values are held exclusively in React component state — never written to localStorage,
 * sessionStorage, IndexedDB, or any browser-persistent storage
 * (OWASP A02; HIPAA minimum-necessary; checklist).
 */
/** Router-state shape set by AiIntakePage on an AI→Manual mode switch (us_018; AC-001). */
interface ManualPageRouterState {
  mappedFields?: ModeSwitchResponse['mappedFields']
  reviewItems?:  string[]
  cacheVersion?: string
}

export function ManualIntakeForm() {
  const navigate         = useNavigate()
  const location         = useLocation()
  const { accessToken }  = useAuth()

  const [activeTab,    setActiveTab]    = useState(0)
  const [toast,        setToast]        = useState<string | null>(null)
  const [resumingDraft, setResumingDraft] = useState(false)
  const [submitError,  setSubmitError]  = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  // Mode-switch state (us_018; AC-004)
  const [isSwitching,  setIsSwitching]  = useState(false)
  const [switchError,  setSwitchError]  = useState<string | null>(null)
  const [reviewItems,  setReviewItems]  = useState<string[]>([])

  // cacheVersion from mode-switch response — included in subsequent postDraft headers
  // (Edge: concurrent auto-save; OWASP A04; us_018)
  const cacheVersionRef = useRef<string | null>(null)

  // Debounce timer ref: cleared before mode-switch navigation to prevent a concurrent
  // POST /api/intake/draft auto-save from firing during the switch (Edge: concurrent auto-save)
  const autoSaveDebounceRef = useRef<ReturnType<typeof setTimeout> | null>(null)

  const methods = useForm<ManualIntakeFormValues>({
    resolver:      zodResolver(manualIntakeSchema),
    defaultValues: defaultFormValues,
    mode:          'onTouched',   // show per-field errors on blur, not on every keystroke
  })

  const { handleSubmit, trigger, getValues, reset, formState } = methods

  // ── Read router state on mount (AI→Manual switch pre-population; AC-001; AC-003) ────────────
  // Runs after the draft load effect so that mode-switch data is applied last and wins.
  useEffect(() => {
    const state = location.state as ManualPageRouterState | undefined
    if (!state) return

    // cacheVersion token — stored in ref for inclusion in subsequent draft auto-saves
    // (Edge: concurrent auto-save; OWASP A04)
    if (state.cacheVersion) {
      cacheVersionRef.current = state.cacheVersion
    }

    // reviewItems[] from unmapped AI content — rendered above the tab bar (AC-003)
    if (Array.isArray(state.reviewItems) && state.reviewItems.length > 0) {
      setReviewItems(state.reviewItems)
    }

    // mappedFields pre-population — only if non-null and non-empty (Edge: no data entered)
    if (state.mappedFields != null && typeof state.mappedFields === 'object' &&
        Object.keys(state.mappedFields).length > 0) {
      reset(mappedFieldsToFormValues(state.mappedFields))
    }
  }, []) // eslint-disable-line react-hooks/exhaustive-deps

  // ── Load draft on mount (AC-005) ─────────────────────────────────────────────────────────
  useEffect(() => {
    if (!accessToken) return
    let cancelled = false

    getDraft(accessToken)
      .then(draft => {
        if (cancelled || !draft) return
        reset(draftToFormValues(draft))
        setResumingDraft(true)
      })
      .catch(() => { /* no draft or network error — render empty form */ })

    return () => { cancelled = true }
  }, [accessToken, reset])

  // ── Navigation-away draft auto-save (AC-004) ─────────────────────────────────────────────
  // Blocker is skipped when isSwitching is true — mode-switch navigation proceeds without
  // triggering a draft auto-save (Edge: concurrent auto-save; us_018)
  const blocker = useBlocker(
    () => formState.isDirty && !formState.isSubmitSuccessful && !isSwitching
  )

  const dismissToast = useCallback(() => setToast(null), [])

  useEffect(() => {
    if (blocker.state !== 'blocked' || !accessToken) return

    let cancelled = false

    void (async () => {
      try {
        await postDraft(accessToken, getValues() as Partial<ManualIntakeData>, cacheVersionRef.current ?? undefined)
        if (!cancelled) setToast('Draft saved')
      } catch {
        // Best-effort save — proceed even if draft save fails
      } finally {
        if (!cancelled) blocker.proceed?.()
      }
    })()

    return () => { cancelled = true }
  }, [blocker.state]) // eslint-disable-line react-hooks/exhaustive-deps

  // ── Mode switch: Manual → AI (us_018; AC-002) ────────────────────────────────────────────
  async function handleSwitchToAi() {
    if (!accessToken || isSwitching) return
    setSwitchError(null)

    // Cancel any pending debounce auto-save before dispatching the mode-switch call
    // (Edge: concurrent auto-save; us_018)
    if (autoSaveDebounceRef.current !== null) {
      clearTimeout(autoSaveDebounceRef.current)
      autoSaveDebounceRef.current = null
    }

    setIsSwitching(true)
    try {
      const res = await switchMode(accessToken, { from: 'Manual', to: 'AI' })
      // Navigate to AI chat — pass mapped fields and review items via router state
      // (no sessionStorage; PHI stays in React state only; OWASP A02; AIR guardrails)
      navigate('/intake/ai', {
        state: {
          mappedFields: res.mappedFields,
          reviewItems:  res.reviewItems,
          cacheVersion: res.cacheVersion,
          newSessionId: res.newSessionId,
        },
      })
    } catch (err) {
      setSwitchError(err instanceof Error ? err.message : 'Mode switch failed. Please try again.')
      setIsSwitching(false)
    }
    // Note: setIsSwitching(false) is NOT called on success — navigation unmounts the component
  }

  // ── Submit handler (AC-002; AC-003) ──────────────────────────────────────────────────────
  const onSubmit = handleSubmit(async (data) => {
    if (!accessToken) return
    setSubmitError(null)
    setIsSubmitting(true)
    try {
      await postManualIntake(accessToken, data as ManualIntakeData)
      navigate('/intake/confirmation')
    } catch (err) {
      setSubmitError(err instanceof Error ? err.message : 'An unexpected error occurred.')
    } finally {
      setIsSubmitting(false)
    }
  })

  // Called when user clicks Submit but form is invalid — jump to first errored section (AC-003)
  async function handleSubmitClick() {
    const isValid = await trigger()
    if (!isValid) {
      const firstErrorIndex = FORM_SECTIONS.findIndex(s => !!formState.errors[s.key])
      if (firstErrorIndex >= 0) setActiveTab(firstErrorIndex)
    } else {
      void onSubmit()
    }
  }

  // ── Render ────────────────────────────────────────────────────────────────────────────────
  return (
    <FormProvider {...methods}>
      <div
        style={{
          maxWidth:  680,
          margin:    '0 auto',
          padding:   'var(--space-6)',
        }}
      >
        {/* ── "Resuming your saved draft" banner (AC-005; WCAG 4.1.3) ─────── */}
        {resumingDraft && (
          <div
            role="status"
            aria-live="polite"
            style={{
              display:       'flex',
              alignItems:    'center',
              gap:           'var(--space-2)',
              background:    'var(--color-primary-subtle)',
              color:         'var(--color-primary)',
              fontSize:      '13px',
              fontWeight:    500,
              padding:       'var(--space-3) var(--space-4)',
              borderRadius:  'var(--radius-sm)',
              border:        '1px solid #BFDBFE',
              marginBottom:  'var(--space-5)',
            }}
          >
            <InfoIcon />
            Resuming your saved draft
          </div>
        )}

        {/* ── Mode-switch error (icon + text; role="alert"; UXR-105; WCAG 4.1.3) ─────── */}
        {switchError && (
          <div
            role="alert"
            style={{
              display:       'flex',
              alignItems:    'center',
              gap:           'var(--space-2)',
              background:    'var(--color-error-surface)',
              color:         'var(--color-status-error)',
              fontSize:      '13px',
              fontWeight:    500,
              padding:       'var(--space-3) var(--space-4)',
              borderRadius:  'var(--radius-sm)',
              border:        '1px solid var(--color-error-border)',
              marginBottom:  'var(--space-4)',
            }}
          >
            <ErrorIcon />
            {switchError}
          </div>
        )}

        {/* ── Review buffer — unmapped items from AI→Manual switch (AC-003; UXR-105) ─── */}
        <IntakeReviewBuffer reviewItems={reviewItems} />

        {/* ── Page heading + Switch button row ─────────────────────────────── */}
        <div
          style={{
            display:        'flex',
            alignItems:     'center',
            justifyContent: 'space-between',
            marginBottom:   'var(--space-5)',
          }}
        >
          <h1
            style={{
              fontSize:   '20px',
              fontWeight: 700,
              color:      'var(--color-text-primary)',
              margin:     0,
            }}
          >
            Manual Intake Form
          </h1>
          {/* AC-004: switch button — 44×44px, icon+text, keyboard focusable (IntakeSwitchButton) */}
          <IntakeSwitchButton
            targetMode="AI"
            onClick={handleSwitchToAi}
            isLoading={isSwitching}
          />
        </div>

        {/* ── Tab bar — exactly 5 tabs (AC-001; UXR-302) ───────────────────── */}
        <div
          role="tablist"
          aria-label="Intake form sections"
          style={{
            display:          'flex',
            borderBottom:     '2px solid var(--color-border)',
            marginBottom:     'var(--space-6)',
            overflowX:        'auto',
            gap:              0,
          }}
        >
          {FORM_SECTIONS.map((section, i) => {
            const isActive    = i === activeTab
            const hasErrors   = !!formState.errors[section.key]
            return (
              <button
                key={section.key}
                role="tab"
                aria-selected={isActive}
                aria-controls={`panel-${section.key}`}
                id={`tab-${section.key}`}
                type="button"
                onClick={() => setActiveTab(i)}
                style={{
                  flexShrink:      0,
                  padding:         'var(--space-3) var(--space-4)',
                  fontFamily:      'var(--font-sans)',
                  fontSize:        '13px',
                  fontWeight:      isActive ? 600 : 500,
                  color:           isActive ? 'var(--color-primary)' : 'var(--color-text-secondary)',
                  background:      'none',
                  border:          'none',
                  borderBottom:    isActive ? '2px solid var(--color-primary)' : '2px solid transparent',
                  marginBottom:    '-2px',
                  cursor:          'pointer',
                  display:         'flex',
                  alignItems:      'center',
                  whiteSpace:      'nowrap',
                }}
              >
                {section.label}
                {hasErrors && <ErrorBadge />}
              </button>
            )
          })}
        </div>

        {/* ── Active section panel ──────────────────────────────────────────── */}
        {FORM_SECTIONS.map((section, i) => {
          const SectionComponent = SECTION_COMPONENTS[section.key]
          return (
            <div
              key={section.key}
              role="tabpanel"
              id={`panel-${section.key}`}
              aria-labelledby={`tab-${section.key}`}
              hidden={i !== activeTab}
              style={i === activeTab ? { minHeight: 200 } : undefined}
            >
              {i === activeTab && <SectionComponent />}
            </div>
          )
        })}

        {/* ── Submit error (icon + text; role="alert") ─────────────────────── */}
        {submitError && (
          <div
            role="alert"
            style={{
              display:       'flex',
              alignItems:    'center',
              gap:           'var(--space-2)',
              fontSize:      '13px',
              color:         'var(--color-status-error)',
              marginTop:     'var(--space-4)',
              fontWeight:    500,
            }}
          >
            <ErrorIcon />
            {submitError}
          </div>
        )}

        {/* ── Submit button ─────────────────────────────────────────────────── */}
        <div
          style={{
            display:       'flex',
            justifyContent: 'flex-end',
            marginTop:     'var(--space-6)',
            paddingTop:    'var(--space-4)',
            borderTop:     '1px solid var(--color-border)',
          }}
        >
          <button
            type="button"
            onClick={handleSubmitClick}
            disabled={isSubmitting}
            style={{
              background:   isSubmitting ? 'var(--color-bg-subtle)' : 'var(--color-primary)',
              color:        isSubmitting ? 'var(--color-text-disabled)' : 'var(--color-text-inverse)',
              fontFamily:   'var(--font-sans)',
              fontSize:     '15px',
              fontWeight:   600,
              padding:      'var(--space-3) var(--space-8)',
              borderRadius: 'var(--radius-sm)',
              border:       isSubmitting ? '1px solid var(--color-border)' : 'none',
              cursor:       isSubmitting ? 'default' : 'pointer',
              minHeight:    44,
              transition:   'background 0.15s',
            }}
          >
            {isSubmitting ? 'Submitting…' : 'Submit Intake'}
          </button>
        </div>
      </div>

      {/* ── Draft saved toast (AC-004; WCAG 4.1.3) ───────────────────────────── */}
      {toast && <Toast message={toast} onDismiss={dismissToast} />}
    </FormProvider>
  )
}

// ── Inline icons ──────────────────────────────────────────────────────────────────────────────────

function InfoIcon() {
  return (
    <svg
      aria-hidden="true"
      width="14"
      height="14"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      style={{ flexShrink: 0 }}
    >
      <circle cx="12" cy="12" r="10" />
      <line x1="12" y1="16" x2="12" y2="12" />
      <line x1="12" y1="8" x2="12.01" y2="8" />
    </svg>
  )
}
