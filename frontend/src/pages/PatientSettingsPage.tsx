/**
 * SCR-008 — Patient Profile & Settings: Notifications section (us_029; AC-001, AC-002).
 *
 * Loads current preferences on mount via GET /api/patients/{id}/preferences.
 * Renders 5 ToggleSwitch components bound to the five PatientPreferences fields.
 * Each toggle fires PATCH immediately on change (optimistic update with snapshot rollback).
 * Per-field `loadingField` ensures only the in-flight toggle is disabled; the other 4 remain
 * fully interactive (AC-002 — per-field loading isolation).
 * No minimum-channel enforcement — all channels disabled is a valid persisted state
 * (Edge: all channels disabled; task spec).
 */

import { useEffect, useState, type CSSProperties } from 'react'
import { useAuth } from '../context/AuthContext'
import { Header } from '../components/layout/Header'
import { ToggleSwitch } from '../components/settings/ToggleSwitch'
import {
  getPreferences,
  patchPreference,
  type PatientPreferences,
} from '../api/patientPreferencesApi'

// ── Toast state type ──────────────────────────────────────────────────────────────────────────────

interface ToastState {
  visible:  boolean
  message:  string
}

// ── Styles ───────────────────────────────────────────────────────────────────────────────────────

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

const sectionTitleStyle: CSSProperties = {
  fontSize:     '16px',
  fontWeight:   600,
  marginBottom: 'var(--space-5)',
}

const errorBannerStyle: CSSProperties = {
  background:   '#FEF2F2',
  border:       '1px solid #FECACA',
  borderRadius: 'var(--radius-sm)',
  padding:      'var(--space-3) var(--space-4)',
  fontSize:     '14px',
  color:        '#DC2626',
  marginBottom: 'var(--space-5)',
}

const toastStyle = (visible: boolean): CSSProperties => ({
  position:   'fixed',
  bottom:     '24px',
  right:      '24px',
  background: 'var(--color-bg-surface)',
  border:     '1px solid var(--color-border)',
  borderLeft: '3px solid #F59E0B', // warning amber
  borderRadius: 'var(--radius-sm)',
  padding:    'var(--space-3) var(--space-4)',
  display:    'flex',
  alignItems: 'center',
  gap:        'var(--space-3)',
  fontSize:   '14px',
  boxShadow:  '0 4px 12px rgba(15,23,42,0.10)',
  zIndex:     100,
  opacity:    visible ? 1 : 0,
  pointerEvents: visible ? 'auto' : 'none',
  transition: 'opacity 0.2s',
})

// ── Warning icon ─────────────────────────────────────────────────────────────────────────────────

function WarningIcon() {
  return (
    <svg
      width="16"
      height="16"
      viewBox="0 0 24 24"
      fill="none"
      stroke="#F59E0B"
      strokeWidth="2"
      aria-hidden="true"
      style={{ flexShrink: 0 }}
    >
      <path d="M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z" />
      <line x1="12" y1="9" x2="12" y2="13" />
      <line x1="12" y1="17" x2="12.01" y2="17" />
    </svg>
  )
}

// ── Toggle config ─────────────────────────────────────────────────────────────────────────────────

const TOGGLES: Array<{
  field: keyof PatientPreferences
  label: string
  description: string
}> = [
  {
    field:       'emailNotificationsEnabled',
    label:       'Email Reminders',
    description: 'Appointment reminder emails 24h and 2h before your appointment',
  },
  {
    field:       'smsNotificationsEnabled',
    label:       'SMS Reminders',
    description: 'Appointment reminder text messages 24h and 2h before your appointment',
  },
  {
    field:       'slotSwapNotificationsEnabled',
    label:       'Slot Swap Notifications',
    description: 'Notify me when an earlier slot becomes available for my appointment',
  },
  {
    field:       'googleCalendarSyncEnabled',
    label:       'Google Calendar Sync',
    description: 'Automatically add booked appointments to Google Calendar',
  },
  {
    field:       'outlookCalendarSyncEnabled',
    label:       'Outlook Calendar Sync',
    description: 'Automatically add booked appointments to Outlook Calendar',
  },
]

// ── Component ─────────────────────────────────────────────────────────────────────────────────────

export function PatientSettingsPage() {
  const { accessToken, userId } = useAuth()

  const [preferences, setPreferences] = useState<PatientPreferences | null>(null)
  // loadingField: the specific preference field whose PATCH is currently in-flight.
  // null means no PATCH is in flight; any other 4 toggles remain fully interactive (AC-002).
  const [loadingField, setLoadingField] = useState<keyof PatientPreferences | null>(null)
  const [loadError, setLoadError]       = useState<string | null>(null)
  const [toast, setToast]               = useState<ToastState>({ visible: false, message: '' })

  // ── Load preferences on mount ────────────────────────────────────────────────────────────────
  useEffect(() => {
    if (!userId || !accessToken) return

    let cancelled = false

    getPreferences(userId, accessToken)
      .then(prefs => { if (!cancelled) setPreferences(prefs) })
      .catch(() => {
        if (!cancelled)
          setLoadError('Could not load your preferences. Please refresh the page.')
      })

    return () => { cancelled = true }
  }, [userId, accessToken])

  // ── Per-toggle change handler ────────────────────────────────────────────────────────────────
  async function handleToggle(
    field:    keyof PatientPreferences,
    newValue: boolean,
  ) {
    if (!userId || !accessToken || !preferences) return

    // (1) Capture snapshot for rollback (Edge: PATCH fails)
    const prev = preferences[field]

    // (2) Optimistic update — reflect new value immediately (AC-002)
    setPreferences(p => p ? { ...p, [field]: newValue } : p)

    // (3) Mark only this field as loading — other toggles remain interactive (AC-002)
    setLoadingField(field)

    try {
      // (4) PATCH with the single changed field (AC-002; OWASP A03 — minimal payload)
      const confirmed = await patchPreference(userId, field, newValue, accessToken)

      // (5) Update from server-confirmed body — source of truth after successful PATCH (AC-002)
      setPreferences(confirmed)
    } catch {
      // (6) Revert to snapshot on failure (Edge: PATCH fails)
      setPreferences(p => p ? { ...p, [field]: prev } : p)

      // Show failure toast (Edge: PATCH fails; UXR-105; WCAG 2.1 A — role="status" polite)
      showToast('Could not save your preference. Please try again.')
    } finally {
      setLoadingField(null)
    }
  }

  // ── Toast helper ─────────────────────────────────────────────────────────────────────────────
  function showToast(message: string) {
    setToast({ visible: true, message })
    setTimeout(() => setToast({ visible: false, message: '' }), 4000)
  }

  // ── Render ───────────────────────────────────────────────────────────────────────────────────
  return (
    <div style={pageStyle}>
      <Header />

      <main style={contentStyle} id="main-content">
        <h1 style={{ fontSize: '28px', fontWeight: 700, marginBottom: 'var(--space-8)' }}>
          Profile &amp; settings
        </h1>

        <div style={cardStyle}>
          {/* ── Section heading ── */}
          <h2 style={{ fontSize: '20px', fontWeight: 700, marginBottom: 'var(--space-2)' }}>
            Notifications
          </h2>
          <p style={{ fontSize: '14px', color: 'var(--color-text-secondary)', marginBottom: 'var(--space-6)' }}>
            Choose how you'd like to be notified about your appointments and available time slots.
            You can disable all channels — no minimum is required.
          </p>

          {/* ── Load error banner ── */}
          {loadError && (
            <div role="alert" style={errorBannerStyle}>
              {loadError}
            </div>
          )}

          {/* ── Loading skeleton ── */}
          {!preferences && !loadError && (
            <div
              role="status"
              aria-label="Loading preferences…"
              style={{ color: 'var(--color-text-secondary)', fontSize: '14px', padding: 'var(--space-4) 0' }}
            >
              Loading preferences…
            </div>
          )}

          {/* ── 5 toggle switches ─────────────────────────────────────────────────────────────
              AC-001: each reflects the current saved preference loaded from the API.
              AC-002: change fires PATCH with the single changed field; no page reload required.
              Edge: all channels disabled — no warning shown; no minimum enforced.
          ─────────────────────────────────────────────────────────────────────────────────── */}
          {preferences && (
            <div
              role="list"
              aria-label="Notification preferences"
              style={{ borderTop: '1px solid var(--color-border)' }}
            >
              <h3 style={{ ...sectionTitleStyle, paddingTop: 'var(--space-5)', borderBottom: 'none' }}>
                Reminders &amp; alerts
              </h3>

              {TOGGLES.slice(0, 3).map(t => (
                <div key={t.field} role="listitem">
                  <ToggleSwitch
                    id={`pref-${t.field}`}
                    label={t.label}
                    description={t.description}
                    checked={preferences[t.field]}
                    onChange={val => handleToggle(t.field, val)}
                    isLoading={loadingField === t.field}
                  />
                </div>
              ))}

              <h3 style={{ ...sectionTitleStyle, paddingTop: 'var(--space-5)', borderBottom: 'none' }}>
                Calendar sync
              </h3>

              {TOGGLES.slice(3).map(t => (
                <div key={t.field} role="listitem">
                  <ToggleSwitch
                    id={`pref-${t.field}`}
                    label={t.label}
                    description={t.description}
                    checked={preferences[t.field]}
                    onChange={val => handleToggle(t.field, val)}
                    isLoading={loadingField === t.field}
                  />
                </div>
              ))}
            </div>
          )}
        </div>
      </main>

      {/* ── Failure toast ──────────────────────────────────────────────────────────────────────
          role="status" + aria-live="polite": announced to screen readers without focus shift
          (Edge: PATCH fails; UXR-105; WCAG 2.1 A — not communicated by colour alone).
      ─────────────────────────────────────────────────────────────────────────────────────── */}
      <div
        role="status"
        aria-live="polite"
        aria-atomic="true"
        style={toastStyle(toast.visible)}
      >
        <WarningIcon />
        <span>{toast.message}</span>
      </div>
    </div>
  )
}
