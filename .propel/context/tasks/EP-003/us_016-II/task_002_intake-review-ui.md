# Task - TASK_002

## Requirement Reference
- **User Story:** us_016-II
- **Story Location:** .propel/context/tasks/EP-003/us_016-II/us_016-II.md
- **Acceptance Criteria:**
  - AC-001: The summary review panel on SCR-004 displays all 5 collected field groups fetched from `GET /intake/ai/summary`; each field shows its value and a UXR-101 confidence indicator
  - AC-002: Each field value has an inline Edit control; clicking it opens an input pre-populated with the current value; saving calls `PATCH /intake/ai/field` and updates the displayed value without a page reload
  - AC-004: After `POST /intake/ai/confirm` returns 201, the patient is navigated to the intake confirmation screen or home dashboard (SCR-003) and a success toast "Your intake has been saved" is displayed
- **Edge Cases:**
  - Session expired (410): if `POST /intake/ai/confirm` returns 410, display the message "Session expired. Your draft has been saved." with a "Resume from draft" button — the patient must not be navigated away
  - Empty required field (400): if `PATCH /intake/ai/field` returns 400, display the inline error (e.g., "Chief complaint cannot be empty") with icon + text alongside the field; the field reverts to its previous value (UXR-105)

---

## Design References
| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | SCR-004 (AI Conversational Intake — summary review panel) |
| **Wireframe Status** | AVAILABLE |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | .propel/context/wireframes/Hi-Fi/wireframe-SCR-004-ai-conversational-intake.html |
| **Screen Spec** | SCR-004 |
| **UXR Requirements** | UXR-101 — confidence indicator (icon + text label) on each collected field; UXR-105 — all error and validation states use icon + text, never colour alone |
| **Design Tokens** | Refer to project design system tokens for summary card layout, field row edit mode, confidence badge variants, inline error text, and success toast |

---

## AI References
| Reference Type | Value |
|----------------|-------|
| **AI Impact** | Yes |
| **AIR Requirements** | AIR-001 (UI renders the structured field summary produced by the Ollama dialogue engine), AIR-002 (confidence indicators reflect field extraction certainty) |
| **AI Pattern** | Summary display — no direct model interaction; frontend renders extraction results from the backend |
| **Prompt Template Path** | N/A |
| **Guardrails Config** | Corrected field values entered by the patient must not be stored in browser localStorage or sessionStorage — in-flight edits live only in React component state |
| **Model Provider** | N/A (display-only; model ran in us_016-I) |

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
| Frontend | React | 18.x | TR-001 (SCR-004 summary panel; inline edit state per field; `useState` for edit mode and pending values) |
| Frontend | TypeScript | 5.x | TR-001 (typed `IntakeSummary`, `IntakeField`, `FieldEditState` interfaces) |
| Frontend | Vite | 5.x | TR-001 (build tooling) |
| Frontend | React Router | v6 | TR-001 (`useNavigate` for post-confirm redirect to SCR-003 / `/intake`) |

---

## Task Overview

Build the summary review panel within SCR-004 as the second phase of the AI intake flow. On mount, `GET /intake/ai/summary` is called and all 5 field group sections are rendered with values and UXR-101 confidence badges. Each field row has an inline edit toggle. Saving a correction calls `PATCH /intake/ai/field` and updates local state on success. A "Confirm & Submit" button triggers `POST /intake/ai/confirm`; success navigates to SCR-003 with a toast. Session-expiry (410) and empty-required-field (400) errors are presented with icon + text per UXR-105.

---

## Dependent Tasks
- task_001 (us_016-II) — `GET /intake/ai/summary`, `PATCH /intake/ai/field`, `POST /intake/ai/confirm` endpoints must be available
- task_002 (us_016-I) — `AiIntakePage.tsx` and `IntakeSummaryPanel.tsx` scaffolding from Part I must exist; this task populates the review panel with live data and interactive controls

---

## Impacted Components
- `src/web/src/features/intake/IntakeSummaryPanel.tsx` — modified: fetch from API on mount; render editable field rows; handle confirm action
- `src/web/src/features/intake/IntakeFieldRow.tsx` — new component: single field display + inline edit toggle + confidence badge
- `src/web/src/api/intakeAiApi.ts` — modified: add `getSummary(sessionId)`, `patchField(sessionId, fieldPath, value)`, `confirmIntake(sessionId)` typed wrappers

---

## Implementation Plan
1. Add three typed fetch wrappers to `intakeAiApi.ts`: `getSummary(sessionId: string): Promise<IntakeSummary>` (GET /api/intake/ai/summary), `patchField(sessionId, fieldPath, value): Promise<IntakeSummary>` (PATCH /api/intake/ai/field — throws `RequiredFieldError` on 400), `confirmIntake(sessionId): Promise<void>` (POST /api/intake/ai/confirm — throws `SessionExpiredError` on 410); all calls include `Authorization: Bearer <accessToken>` (AC-001, AC-002, AC-004)
2. Build `IntakeFieldRow.tsx`: renders `fieldLabel`, `fieldValue`, and a `<ConfidenceBadge confidence={field.confidence}>` sub-component (icon + text label per UXR-101); "Edit" pencil button toggles `isEditing` state; in edit mode renders a text `<input>` pre-populated with `fieldValue` and Save / Cancel buttons (AC-001, AC-002; UXR-101; UXR-105)
3. `IntakeFieldRow` save handler: call `patchField(sessionId, fieldPath, newValue)`; on success, call `onFieldUpdated(fieldPath, newValue)` to update parent state — no page reload (AC-002); on `RequiredFieldError` (400), set `fieldError` state and render `<span role="alert">` with error icon + API error message; revert input to previous value (Edge: empty required field; UXR-105)
4. Update `IntakeSummaryPanel.tsx`: call `getSummary(sessionId)` on mount; render a loading skeleton while pending; map over the 5 field group sections and render `<IntakeFieldRow>` for each field; maintain `summary` state that is updated on each successful `patchField` call via `onFieldUpdated` callback (AC-001, AC-002)
5. Add "Confirm & Submit" button to `IntakeSummaryPanel.tsx`: disabled while any field row has `isEditing=true` (prevents confirming with unsaved edits); on click, call `confirmIntake(sessionId)`; on success, call `navigate('/intake', { state: { message: "Your intake has been saved" } })` so the destination screen shows the toast (AC-003, AC-004)
6. `confirmIntake` error handling in `IntakeSummaryPanel.tsx`: catch `SessionExpiredError` (410) → render a `<div role="alert">` message "Session expired. Your draft has been saved." with a "Resume from draft" `<Link>` button pointing to the draft intake resume route — do not navigate away (Edge: session expired; UXR-105)

---

## Current Project State
```
src/
└── web/
    └── src/
        ├── features/
        │   └── intake/
        │       ├── IntakeSummaryPanel.tsx           (MODIFY — fetch API, editable rows, confirm action)
        │       └── IntakeFieldRow.tsx               (CREATE — field display + inline edit + confidence badge)
        └── api/
            └── intakeAiApi.ts                       (MODIFY — add getSummary, patchField, confirmIntake)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | src/web/src/features/intake/IntakeSummaryPanel.tsx | API fetch on mount; field rows; confirm button; 410 session-expired state |
| CREATE | src/web/src/features/intake/IntakeFieldRow.tsx | Editable field row with UXR-101 confidence badge and inline error on 400 |
| MODIFY | src/web/src/api/intakeAiApi.ts | Add getSummary, patchField (RequiredFieldError on 400), confirmIntake (SessionExpiredError on 410) |

---

## External References
- https://react.dev/reference/react/useState (React 18 useState — inline edit state per field row)
- https://reactrouter.com/en/main/hooks/use-navigate (React Router v6 useNavigate with state for post-confirm toast message)
- https://www.w3.org/WAI/ARIA/apg/patterns/alert/ (WAI-ARIA alert pattern — `role="alert"` for session-expired and field error messages)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] After completing the AI dialogue (us_016-I), verify the summary panel loads automatically with all 5 field groups and each field shows a confidence badge with icon + text (AC-001; UXR-101)
- [ ] Click "Edit" on a field; change the value; click Save; verify the field updates immediately in the panel without a page reload (AC-002)
- [ ] Clear a required field (e.g., chiefComplaint) and save; verify the inline error with icon + text appears and the field reverts to its previous value (Edge: empty required field; UXR-105)
- [ ] Click "Confirm & Submit"; verify navigation to `/intake` and the success toast "Your intake has been saved" is visible (AC-004)
- [ ] While a field is in edit mode, verify the "Confirm & Submit" button is disabled (AC-003 — unsaved edit guard)
- [ ] Simulate a 410 response from confirm; verify the "Session expired" message with "Resume from draft" link appears and the patient remains on the summary screen (Edge: session expired; UXR-105)
- [ ] Verify that corrected field values entered via the inline edit form are not written to `localStorage` or `sessionStorage` (AIR guardrails — PHI browser storage)

---

## Implementation Checklist
- [ ] `IntakeFieldRow` renders both a named icon component and a text label in `<ConfidenceBadge>` — confidence level is never conveyed by colour alone; the badge is present on every AI-collected field (UXR-101; UXR-105; WCAG 1.4.1)
- [ ] `IntakeSummaryPanel` "Confirm & Submit" button has `disabled={fields.some(f => f.isEditing)}` — unsaved field edits block confirmation to prevent data loss (AC-003 — data integrity)
- [ ] `patchField` on 400 (`RequiredFieldError`) renders `<span role="alert">` inside `IntakeFieldRow` and reverts the input `value` to `originalValue` — the patient's previous valid entry is preserved (Edge: empty required field; UXR-105; WCAG 4.1.3)
- [ ] Session-expired error in `IntakeSummaryPanel` uses `<div role="alert">` and does NOT navigate away from SCR-004 — the patient stays on the review screen and can choose to resume from draft (Edge: session expired; UXR-105)
- [ ] Field values held in inline edit `useState` are scoped to the `IntakeFieldRow` component and are never written to browser-persistent storage (AIR guardrails; OWASP A02; HIPAA minimum-necessary)
- [ ] Post-confirm navigation uses `navigate('/intake', { state: { message: "Your intake has been saved" } })` — the toast message is passed via router state, not a query parameter or localStorage, so it does not persist across page reloads (AC-004; clean state handoff)
