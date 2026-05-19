# Task - TASK_002

## Requirement Reference
- **User Story:** us_029
- **Story Location:** .propel/context/tasks/EP-005/us_029/us_029.md
- **Acceptance Criteria:**
  - AC-001: SCR-008 Notifications section renders 5 toggle switches: Email Reminders, SMS Reminders, Slot Swap Notifications, Google Calendar Sync, Outlook Calendar Sync — each reflecting the current saved preference loaded from the API
  - AC-002: Toggle change fires `PATCH /patients/{id}/preferences` with the single changed field; toggle reflects the saved state on 200; no page reload required
- **Edge Cases:**
  - PATCH failure (network error or non-200): revert toggle to its pre-change state; display a toast "Could not save your preference. Please try again." with a warning icon — toggle must not remain in the new position
  - All channels disabled: no UI warning, no forced minimum — user may freely disable all 5 toggles

---

## Design References
| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | SCR-008 (Patient Profile & Settings) |
| **Wireframe Status** | AVAILABLE |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | .propel/context/wireframes/Hi-Fi/wireframe-SCR-008-patient-profile-settings.html |
| **Screen Spec** | SCR-008 — Notifications section: 5 labelled toggle switches with "On"/"Off" text labels |
| **UXR Requirements** | UXR-105 (toggle state must display "On" or "Off" text label alongside the visual switch — not color alone) |
| **Design Tokens** | Toggle active/inactive colors, label text style (from project design token set) |

---

## AI References
| Reference Type | Value |
|----------------|-------|
| **AI Impact** | No |
| **AIR Requirements** | N/A |
| **AI Pattern** | N/A |
| **Prompt Template Path** | N/A |
| **Guardrails Config** | N/A |
| **Model Provider** | N/A |

---

## Mobile References
| Reference Type | Value |
|----------------|-------|
| **Mobile Impact** | No |
| **Platform Target** | N/A |
| **Min OS Version** | N/A |
| **Mobile Framework** | N/A |

---

## Applicable Technology Stack

| Layer | Technology | Version | Justification |
|-------|------------|---------|---------------|
| Frontend | React + TypeScript | 18.x / 5.x | TR-001 — `PatientSettingsPage` (SCR-008); controlled `ToggleSwitch` component with per-field loading state (AC-001, AC-002) |
| HTTP Client | Fetch API (browser built-in) | Web platform | `GET /api/patients/{id}/preferences` on mount; `PATCH /api/patients/{id}/preferences` on toggle change (AC-001, AC-002) |
| Routing | React Router | v6 | SCR-008 patient settings route; authenticated patient route guard |

---

## Task Overview

Build the Notifications section of SCR-008 (`PatientSettingsPage`). On mount, the page fetches current preferences and renders 5 labelled `ToggleSwitch` components. Each toggle manages its own loading state. On change, the component takes a snapshot of the current value, applies the optimistic update, calls PATCH, and on failure reverts the snapshot and fires a toast. No UI constraint limits the minimum enabled channels. All toggle state is labelled "On"/"Off" in text alongside the visual indicator.

---

## Dependent Tasks
- task_001 (us_029) — `PATCH /api/patients/{id}/preferences` and `GET /api/patients/{id}/preferences` endpoints must be available before this component can compile and integrate

---

## Impacted Components
- `src/web/src/pages/PatientSettingsPage.tsx` (SCR-008) — new or modified: add Notifications section with preference state and 5 ToggleSwitch instances
- `src/web/src/components/settings/ToggleSwitch.tsx` — new: reusable labelled toggle with "On"/"Off" text, loading state, and accessible aria attributes
- `src/web/src/api/patientPreferencesApi.ts` — new: typed fetch wrappers for GET and PATCH preferences

---

## Implementation Plan
1. Create `patientPreferencesApi.ts`: `getPreferences(patientId: string): Promise<PatientPreferences>` — `GET /api/patients/{patientId}/preferences`; `patchPreference(patientId: string, field: keyof PatientPreferences, value: boolean): Promise<PatientPreferences>` — `PATCH /api/patients/{patientId}/preferences` with `{[field]: value}`; throws typed `ApiError` on non-200 responses (AC-002; Edge: PATCH failure detection)
2. `PatientSettingsPage` loads preferences via `getPreferences(patientId)` on mount using `useEffect`; stores result in `preferences: PatientPreferences` state; renders the Notifications section with 5 `ToggleSwitch` instances bound to the corresponding preference field (AC-001)
3. Create `ToggleSwitch` component: props `label: string`, `checked: boolean`, `onChange: (value: boolean) => void`, `isLoading: boolean`, `id: string`; renders `<label htmlFor={id}>{label}</label>`, a `<button role="switch" aria-checked={checked} aria-label={label} id={id}>` toggle, and a `<span aria-hidden="true">{checked ? "On" : "Off"}</span>` text label; `disabled` when `isLoading === true` (AC-001; UXR-105 — "On"/"Off" text; WCAG 2.1 — `role="switch"`)
4. Per-toggle change handler: (1) capture previous value in `const prev = preferences[field]`; (2) call `setPreferences(p => ({ ...p, [field]: newValue }))` (optimistic update); (3) set per-field loading state `setLoadingField(field)`; (4) call `await patchPreference(patientId, field, newValue)` (AC-002)
5. On successful PATCH: update `preferences` from the response body (confirmed server value); clear loading state; no toast shown (AC-002 — silent success)
6. On PATCH failure (`ApiError` or network error): revert `setPreferences(p => ({ ...p, [field]: prev }))` back to the pre-change snapshot; clear loading state; fire toast "Could not save your preference. Please try again." using `<div role="status" aria-live="polite">` with a warning icon; no forced minimum channel validation shown (Edge: PATCH fails; UXR-105; Edge: all channels disabled — no warning)

---

## Current Project State
```
src/
└── web/
    └── src/
        ├── api/
        │   └── (patientPreferencesApi.ts            — CREATE)
        ├── components/
        │   └── settings/
        │       └── (ToggleSwitch.tsx                 — CREATE)
        └── pages/
            └── (PatientSettingsPage.tsx              — CREATE or MODIFY for SCR-008)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | src/web/src/api/patientPreferencesApi.ts | Typed fetch wrappers for GET and PATCH preferences |
| CREATE | src/web/src/components/settings/ToggleSwitch.tsx | Reusable accessible toggle with "On"/"Off" label and loading state |
| CREATE/MODIFY | src/web/src/pages/PatientSettingsPage.tsx | SCR-008 Notifications section with 5 toggles and preference state |

---

## External References
- .propel/context/wireframes/Hi-Fi/wireframe-SCR-008-patient-profile-settings.html (SCR-008 HTML wireframe — Notifications section layout, toggle switch visual style, label positions)
- https://www.w3.org/WAI/ARIA/apg/patterns/switch/ (ARIA switch pattern — `role="switch"` with `aria-checked`; UXR-105 + WCAG 2.1 accessible toggle implementation)
- https://react.dev/reference/react/useState (React useState — optimistic update pattern with snapshot rollback on failure)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] Navigate to SCR-008; verify all 5 toggles display with their correct current saved state and "On"/"Off" text labels (AC-001; UXR-105)
- [ ] Flip the SMS Reminders toggle; verify `PATCH` is called with `{"sms_notifications_enabled": false}`; toggle shows "Off" label and updated visual state; no page reload (AC-002)
- [ ] Stub the PATCH to return a 500; flip a toggle; verify the toggle reverts to its previous state and the toast "Could not save your preference. Please try again." appears with a warning icon (Edge: PATCH fails; UXR-105)
- [ ] Disable all 5 toggles; verify no warning dialog or validation message is shown and all PATCH calls succeed (Edge: all channels disabled)
- [ ] Verify `ToggleSwitch` uses `role="switch"` with `aria-checked` and displays both the visual toggle and "On"/"Off" text — toggle state is not communicated by colour alone (UXR-105; WCAG 2.1 A)
- [ ] Flip a toggle; verify it enters loading/disabled state during the PATCH in-flight; other toggles remain interactive (AC-002 — per-field loading isolation)

---

## Implementation Checklist
- [ ] `ToggleSwitch` renders `role="switch"` with `aria-checked={checked}` and a visible `<span aria-hidden="true">{checked ? "On" : "Off"}</span>` — toggle state is communicated by text label in addition to the visual switch indicator, never by colour alone (UXR-105; WCAG 2.1 A)
- [ ] Optimistic update applies `setPreferences` immediately on toggle change; the pre-change value is captured in a `const prev` before the state update — the revert path uses `prev` and is called only in the catch block, not in finally (Edge: PATCH fails — clean rollback)
- [ ] Toast on failure uses `<div role="status" aria-live="polite">` with a warning icon and message text "Could not save your preference. Please try again." — announced to screen readers without requiring focus (Edge: PATCH fails; UXR-105; WCAG 2.1 A)
- [ ] Per-field `loadingField` state (`string | null`) ensures only the toggle whose PATCH is in-flight is disabled; the remaining 4 toggles are fully interactive during the in-flight window (AC-002 — per-field loading isolation)
- [ ] No minimum-channel enforcement exists in the component: disabling all toggles produces no modal, tooltip, or validation feedback — the component treats the fully-disabled state identically to any other valid preference combination (Edge: all channels disabled)
- [ ] `preferences` state is updated from the PATCH response body on success, not from the local optimistic value — the server-confirmed value is the source of truth after each successful PATCH (AC-002 — contract reliability)
