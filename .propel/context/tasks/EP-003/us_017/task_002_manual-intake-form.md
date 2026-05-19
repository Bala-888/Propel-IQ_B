# Task - TASK_002

## Requirement Reference
- **User Story:** us_017
- **Story Location:** .propel/context/tasks/EP-003/us_017/us_017.md
- **Acceptance Criteria:**
  - AC-001: Five tab sections are visible on SCR-005 (Demographics, Medical History, Medications, Allergies, Chief Complaint); each section is accessible by tab click without a page reload
  - AC-002: When all mandatory fields are filled and the patient clicks Submit, `POST /intake/manual` is called; on HTTP 201, the patient is navigated to the confirmation screen
  - AC-003: If the Chief Complaint section is empty when Submit is clicked, submission is blocked, the "Chief Complaint" tab label shows an error indicator, and the message "Chief complaint is required" appears in that section without clearing other entered data
  - AC-004: When the patient navigates away from SCR-005 with unsaved changes, `POST /intake/draft` is called automatically and a toast "Draft saved" is displayed
  - AC-005: If a Draft `IntakeRecord` exists when the patient opens SCR-005, form fields are pre-populated from the draft and a banner "Resuming your saved draft" is displayed at the top of the form
- **Edge Cases:**
  - Invalid date format: if a Medical History date field contains a value that does not match `YYYY-MM-DD` or `MM/DD/YYYY`, the field shows "Please enter a valid date" with icon + text and submission is blocked; other fields are not cleared (UXR-105)
  - Brand-only medication: if a medication name is filled but dosage is absent, a soft advisory "Consider adding dosage for clarity" appears with icon + text; submission is not blocked (UXR-105)

---

## Design References
| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | SCR-005 (Manual Intake Form) |
| **Wireframe Status** | AVAILABLE |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | .propel/context/wireframes/Hi-Fi/wireframe-SCR-005-manual-intake-form.html |
| **Screen Spec** | SCR-005 |
| **UXR Requirements** | UXR-105 — all validation and advisory states use icon + text, never colour alone; UXR-302 — tab bar has exactly 5 tabs (max per UXR-302 mobile bottom tab bar constraint) |
| **Design Tokens** | Refer to project design system tokens for tab bar layout, tab error badge, section field layout, draft banner, and toast component |

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
| Frontend | React | 18.x | TR-001 (SCR-005 tabbed form; tab state; section component tree; form state via react-hook-form) |
| Frontend | TypeScript | 5.x | TR-001 (typed ManualIntakeDraft, ManualIntakeData, ManualIntakeResponse interfaces) |
| Frontend | Vite | 5.x | TR-001 (build tooling) |
| Frontend | React Router | v6 | TR-001 (`useNavigate` for post-submit redirect; `useBlocker` for navigation-away auto-save) |
| Frontend | react-hook-form + zod + @hookform/resolvers | latest stable | Form state management; per-section zod schema validation; `trigger()` for programmatic section-level validation before submit |

---

## Task Overview

Build the manual intake form for SCR-005 as a 5-section tabbed container. On mount, `GET /intake/draft` is called; if a draft is found, form fields are pre-populated and a "Resuming your saved draft" banner is shown. Each section registers its fields in a shared react-hook-form context with per-section zod sub-schemas. Tab labels show an icon + text error indicator for any section with active errors. Navigation-away is intercepted by React Router `useBlocker` to auto-save a draft. Submit validates all sections with `trigger()`; on success calls `POST /intake/manual` and navigates to confirmation. UXR-105 compliance throughout.

---

## Dependent Tasks
- task_001 (us_017) — `GET /intake/draft`, `POST /intake/draft`, `POST /intake/manual` endpoints must be available
- task_002 (us_016-I) — `AiIntakePage.tsx` and the intake feature folder structure must exist (this task adds `ManualIntakeForm.tsx` alongside it)

---

## Impacted Components
- `src/web/src/features/intake/ManualIntakeForm.tsx` — new: 5-section tabbed form container, draft load, useBlocker, submit handler
- `src/web/src/features/intake/sections/DemographicsSection.tsx` — new: Demographics form fields
- `src/web/src/features/intake/sections/MedicalHistorySection.tsx` — new: Medical History fields with date format validation
- `src/web/src/features/intake/sections/MedicationsSection.tsx` — new: Medications fields with dosage soft advisory
- `src/web/src/features/intake/sections/AllergiesSection.tsx` — new: Allergies form fields
- `src/web/src/features/intake/sections/ChiefComplaintSection.tsx` — new: Chief Complaint mandatory textarea
- `src/web/src/features/intake/manualIntakeSchema.ts` — new: consolidated zod schemas for all 5 sections
- `src/web/src/api/intakeManualApi.ts` — new: `getDraft`, `postDraft`, `postManualIntake` typed API wrappers

---

## Implementation Plan
1. Create `intakeManualApi.ts` with three typed wrappers: `getDraft(): Promise<ManualIntakeDraft | null>` (GET /api/intake/draft — return null on 404); `postDraft(data: Partial<ManualIntakeData>): Promise<void>` (POST /api/intake/draft — fire-and-forget on navigation-away); `postManualIntake(data: ManualIntakeData): Promise<ManualIntakeResponse>` (POST /api/intake/manual — `ManualIntakeResponse` includes optional `warnings: string[]`); all wrappers attach `Authorization: Bearer <accessToken>` (AC-002, AC-004, AC-005)
2. Create `manualIntakeSchema.ts` defining a zod object per section: Demographics (`firstName`, `lastName`, `dateOfBirth` — required); MedicalHistory (date strings validated via `.refine(v => /^(\d{4}-\d{2}-\d{2}|\d{2}\/\d{2}\/\d{4})$/.test(v), "Please enter a valid date")`); Medications (`name` required, `dosage` optional — not a zod error when absent); Allergies (all optional); ChiefComplaint (`description` required with message "Chief complaint is required"); combine into `manualIntakeSchema` for full-form validation (AC-003; Edge: invalid date)
3. Build `ManualIntakeForm.tsx`: render 5 `<TabPanel>` tabs with labels `["Demographics", "Medical History", "Medications", "Allergies", "Chief Complaint"]`; active tab managed by `useState<number>`; each tab panel renders the corresponding section component; tab label renders `<ErrorBadge sectionHasErrors={...} />` (icon + text "(errors)") when `formState.errors` contains entries for that section's field paths — never colour-only (AC-001; AC-003; UXR-105; UXR-302 — 5 tabs)
4. On mount, `useEffect` calls `getDraft()`; if draft returned, call `reset(draftToFormValues(draft))` to populate all fields; render `<div role="status" aria-live="polite">Resuming your saved draft</div>` as a banner at the top of the form; if no draft (null / 404), render form in empty state (AC-005; WCAG 4.1.3)
5. Build each section component (`DemographicsSection`, `MedicalHistorySection`, `MedicationsSection`, `AllergiesSection`, `ChiefComplaintSection`) as react-hook-form `Controller`-wired field groups; `MedicalHistorySection` renders `<span role="alert">` with error icon + text for invalid dates; `MedicationsSection` renders `<span role="status">` advisory "Consider adding dosage for clarity" (with icon + text) when `watch('medications.name')` is non-empty and `watch('medications.dosage')` is empty — advisory does not set a `formState.errors` entry (AC-003; Edge: invalid date; Edge: brand-only medication; UXR-105; WCAG 1.4.1)
6. Navigation-away guard: `useBlocker` from React Router v6; block condition is `formState.isDirty && !formState.isSubmitSuccessful`; in the blocker `proceed` hook, await `postDraft(getValues())`; show `<Toast>` "Draft saved"; then release the blocker to allow navigation (AC-004; WCAG 4.1.3)
7. Submit handler: call `trigger()` to validate all sections; if `isValid === false` → identify sections with errors from `formState.errors`; set the active tab to the first section with errors; do not clear values from valid sections; if `isValid === true` → call `postManualIntake(getValues())`; on 201 → `navigate('/intake/confirmation')`; on non-201 error → show `<span role="alert">` generic error with icon + text (AC-002; AC-003)
8. All error and advisory text rendered throughout the form uses a named icon component + visible text label alongside the message; no state is communicated by background or text colour change alone; error messages use `role="alert"` and advisories use `role="status"` (UXR-105; WCAG 1.4.1; WCAG 4.1.3; OWASP A02 — no PHI in error messages)

---

## Current Project State
```
src/
└── web/
    └── src/
        ├── features/
        │   └── intake/
        │       ├── AiIntakePage.tsx             (existing — AI intake; do not modify)
        │       ├── IntakeSummaryPanel.tsx        (existing — AI intake review; do not modify)
        │       └── (ManualIntakeForm.tsx         — CREATE)
        └── api/
            ├── intakeAiApi.ts                   (existing — AI intake)
            └── (intakeManualApi.ts              — CREATE)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | src/web/src/features/intake/ManualIntakeForm.tsx | 5-section tabbed form container with draft load, useBlocker, and submit handler |
| CREATE | src/web/src/features/intake/sections/DemographicsSection.tsx | Demographics form fields |
| CREATE | src/web/src/features/intake/sections/MedicalHistorySection.tsx | Medical History fields with date refine validation |
| CREATE | src/web/src/features/intake/sections/MedicationsSection.tsx | Medications fields with non-blocking dosage advisory |
| CREATE | src/web/src/features/intake/sections/AllergiesSection.tsx | Allergies form fields |
| CREATE | src/web/src/features/intake/sections/ChiefComplaintSection.tsx | Chief Complaint mandatory textarea |
| CREATE | src/web/src/features/intake/manualIntakeSchema.ts | Zod schemas for all 5 sections |
| CREATE | src/web/src/api/intakeManualApi.ts | getDraft, postDraft, postManualIntake typed API wrappers |

---

## External References
- https://react-hook-form.com/docs/useform/trigger (react-hook-form trigger — programmatic full-form validation before submit)
- https://reactrouter.com/en/main/hooks/use-blocker (React Router v6 useBlocker — navigation-away interception for draft auto-save)
- https://zod.dev/?id=refine (Zod .refine — custom date format validation in MedicalHistorySection)
- https://www.w3.org/WAI/ARIA/apg/patterns/tabpanel/ (WAI-ARIA tabpanel pattern — accessible 5-section tab navigation for AC-001)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] Load SCR-005; verify exactly 5 tabs are rendered in order: Demographics, Medical History, Medications, Allergies, Chief Complaint; clicking each tab reveals that section without a page reload (AC-001; UXR-302)
- [ ] With an existing draft record, load SCR-005; verify all fields from the draft are pre-populated and the "Resuming your saved draft" banner is visible at the top (AC-005)
- [ ] Submit form with empty Chief Complaint; verify submission is blocked; the "Chief Complaint" tab label shows an error icon + "(errors)" text; the message "Chief complaint is required" appears in the Chief Complaint section; data in other sections is retained (AC-003; UXR-105)
- [ ] Enter `"99/99/2025"` in a Medical History date field; verify "Please enter a valid date" appears inline with icon + text; other fields on that section are not cleared; form cannot be submitted (Edge: invalid date; UXR-105)
- [ ] Enter a medication name ("Tylenol") with no dosage; verify the advisory "Consider adding dosage for clarity" appears with icon + text; verify the form submits successfully (no 422 error) (Edge: brand-only medication; UXR-105)
- [ ] Fill 3 sections, then click browser back; verify toast "Draft saved" appears and `POST /api/intake/draft` is called; navigation proceeds after save (AC-004)
- [ ] Fill all 5 sections with valid data and submit; verify `POST /api/intake/manual` is called, returns 201, and the user is navigated to the confirmation screen (AC-002)
- [ ] Verify no validation state (error, advisory, success) is communicated by background colour or text colour change alone — all states use a visible named icon component + text label (UXR-105; WCAG 1.4.1)

---

## Implementation Checklist
- [ ] `ManualIntakeForm` renders exactly 5 tab sections in the order defined by AC-001; tab navigation is client-side state only (`useState<number>`) — no page reload occurs on tab switch; tab bar contains no more than 5 tabs (AC-001; UXR-302)
- [ ] On mount, `getDraft()` is awaited; if a non-null draft is returned, `reset(draftToFormValues(draft))` is called to hydrate all 5 section fields; a `<div role="status" aria-live="polite">Resuming your saved draft</div>` banner is rendered at the top of the form (AC-005; WCAG 4.1.3)
- [ ] Date fields in `MedicalHistorySection` use `z.string().refine(v => /^(\d{4}-\d{2}-\d{2}|\d{2}\/\d{2}\/\d{4})$/.test(v), "Please enter a valid date")`; on error, renders `<span role="alert">` with a named icon component + message text; other fields in the section are not cleared (Edge: invalid date; UXR-105; WCAG 1.4.1)
- [ ] `MedicationsSection` shows `<span role="status">` advisory "Consider adding dosage for clarity" (with icon + text) when medication name is non-empty and dosage is empty; this advisory does NOT set a `formState.errors` entry and does NOT prevent form submission (Edge: brand-only medication; UXR-105)
- [ ] Tab labels use a reactive `ErrorBadge` sub-component that renders a named icon + "(errors)" text when `formState.errors` contains at least one entry for that section's field namespace; the badge is removed once errors are resolved — no colour-only change (AC-003; UXR-105; WCAG 1.4.1)
- [ ] React Router `useBlocker` fires when `formState.isDirty && !formState.isSubmitSuccessful`; the blocker's `proceed` path awaits `postDraft(getValues())` before releasing navigation; `<Toast>` "Draft saved" is shown to the patient (AC-004)
- [ ] Submit handler calls `trigger()` across all sections; on `isValid === false`, sets active tab to the index of the first section with errors and does not clear any field value; on `isValid === true`, calls `postManualIntake(getValues())`; on 201 response, calls `navigate('/intake/confirmation')` (AC-002; AC-003)
- [ ] Every error uses `<span role="alert">` and every advisory uses `<span role="status">`; all messages include a named icon component alongside visible text — no state is communicated by colour change alone; no PHI field values are included in visible or logged error messages (UXR-105; WCAG 1.4.1; WCAG 4.1.3; OWASP A02)
