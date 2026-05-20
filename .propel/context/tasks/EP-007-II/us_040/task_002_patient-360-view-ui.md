# Task - TASK_002

## Requirement Reference
- **User Story:** us_040
- **Story Location:** .propel/context/tasks/EP-007-II/us_040/us_040.md
- **Acceptance Criteria:**
  - AC-001: SCR-013 patient search field calls `GET /patients/search?q=<term>` when input is ≥3 characters and displays results with full name, DOB, and patient ID within 500ms
  - AC-002: Navigating to `/patients/{id}/view` (SCR-014) fetches `GET /patients/{id}/summary` and renders the 360° view with demographics, entities, active bookings, and documents tabs within 2 seconds
  - AC-003: The summary payload shape (`demographics`, `entities`, `activeBookings`, `documents`) is rendered across the appropriate tabs; entity confidence and low-confidence flag are visually indicated with text + icon (not color alone)
  - AC-004: The UI respects role — the search and view pages are only accessible to authenticated Staff/Admin/Clinician users; attempting to access these routes as a Patient redirects to the patient dashboard
- **Edge Cases:**
  - No extracted entities: Extracted data tab shows "No clinical entities extracted yet." inline text (not a blank panel) when `entities` is an empty array
  - 100+ documents: Documents tab uses page controls (Prev/Next + "Page X of Y") calling `GET /patients/{id}/summary?page=N&pageSize=20` — only current page's 20 documents are held in state

---

## Design References
| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes — SCR-013 (Patient Search), SCR-014 (360° Patient View) |
| **Figma URL** | N/A |
| **Wireframe Status** | AVAILABLE |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | .propel/context/wireframes/Hi-Fi/wireframe-SCR-014-360-patient-view.html |
| **Screen Spec** | SCR-013 (Patient Search), SCR-014 (360° Patient View) |
| **UXR Requirements** | UXR-105 (entity confidence shown with icon + text, not color alone); UXR-402 (AI label on extracted entities — "✦ AI extracted" badge); UXR-206 (aria-live for search results announcements) |
| **Design Tokens** | `--color-ai-accent: #6366F1`, `--color-ai-bg: #EEF2FF`, `--font-mono: 'IBM Plex Mono'`, `--color-primary: #1A56DB`, `--color-status-success: #16A34A`, `--color-status-error: #DC2626`, `--color-bg-subtle: #F1F5F9`, `--color-border: #E2E8F0` |

---

## AI References
| Reference Type | Value |
|----------------|-------|
| **AI Impact** | No |
| **AIR Requirements** | UXR-402 — all entities in the Extracted data tab must be labelled as AI-extracted using the `ai-label` badge (`--color-ai-accent` + "✦ AI extracted" text), signalling to staff that these are model outputs requiring clinical verification |
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
| Frontend | React + TypeScript | 18.x / 5.x | TR-001 — `PatientSearchPage` (SCR-013) and `PatientViewPage` (SCR-014) components; 5-tab navigation; entity confidence rendering (AC-001–004) |
| Routing | React Router v6 | v6 | TR-001 — `/patients/search` (SCR-013) and `/patients/:id/view` (SCR-014) routes; role-gated `<ProtectedRoute>` wrapper redirecting Patient role to dashboard (AC-004) |
| HTTP Client | apiClient (Axios/Fetch wrapper) | project standard | TR-001 — `GET /patients/search`, `GET /patients/{id}/summary` with pagination params; JWT bearer from auth context; `AbortController` for debounced search cleanup (AC-001, AC-002; OWASP A01) |
| Styling | CSS custom properties | project standard | TR-002 — SCR-014 design tokens from wireframe; AI label badge with `--color-ai-accent`; entity cards; tab underline active style (AC-003; UXR-402, UXR-105) |

---

## Task Overview

Implement SCR-013 patient search with debounced input (300ms) and result navigation. Implement SCR-014 tabbed 360° patient view: five tabs (Demographics, Intake, Documents, Extracted data, Medical codes) rendered from `PatientSummaryDto`. The Extracted data tab renders entity cards with AI label badges and confidence text + icon (UXR-105, UXR-402). The Documents tab supports pagination. Both pages are protected by role-aware routing. TypeScript types provide exhaustive coverage of the DTO shape.

---

## Dependent Tasks
- task_001 (us_040) — `GET /patients/search` and `GET /patients/{id}/summary` API endpoints must exist before the UI can function
- task_002 (us_029) — auth context providing JWT and user role must exist for `ProtectedRoute` to evaluate role

---

## Impacted Components
- `src/web/src/pages/PatientSearchPage.tsx` — new: SCR-013 debounced search + results list
- `src/web/src/pages/PatientViewPage.tsx` — new: SCR-014 tabbed 360° view with all five panels
- `src/web/src/components/PipelineStepper.tsx` — no change (referenced by SCR-010, not SCR-014)
- `src/web/src/components/AiLabel.tsx` — new: reusable `<AiLabel />` badge (`--color-ai-accent`, `--color-ai-bg`, "✦ AI extracted" text)
- `src/web/src/types/patient.ts` — new: `PatientSearchResultDto`, `PatientSummaryDto`, `EntityDto`, `BookingSummaryDto`, `DocumentSummaryDto`, `DemographicsDto`, `getConfidenceLabel` helper
- `src/web/src/pages/PatientViewPage.module.css` — new: tab styles, entity card, AI label, confidence badge, patient header

---

## Implementation Plan
1. `PatientSearchPage` (SCR-013): controlled `<input type="search" aria-label="Search patients" aria-autocomplete="list" aria-controls="search-results">` with a `useDebounce(300ms)` hook; when debounced value is `≥3` characters, call `apiClient.get<PatientSearchResultDto[]>('/patients/search?q=' + encodeURIComponent(q))` with an `AbortController` cancelling the previous in-flight request; render results in `<ul id="search-results" role="listbox" aria-live="polite">` with each result as `<li role="option" onClick={() => navigate('/patients/${r.id}/view')}>`; each item shows `r.fullName` + `r.dateOfBirth` formatted `DD MMM YYYY` + `r.patientCode` in `--font-mono`; `aria-live="polite"` on result count `<span>` announcing "{N} results found" (AC-001; UXR-206 — screen reader result announcement)
2. `PatientViewPage` (SCR-014) skeleton + patient header + tab bar: on mount, call `GET /patients/${id}/summary?page=1&pageSize=20`; set `summary: PatientSummaryDto | null` state; render patient header — `avatar-lg` initials from `firstName[0]+lastName[0]`, patient name (bold), DOB formatted, insurance, patient code in `--font-mono`; "← Back" link to `/patients/search`; 5 tabs using `role="tablist"` with each tab as `<button role="tab" aria-selected={activeTab===id} aria-controls="panel-{id}" id="tab-{id}">`; tab panel as `<div role="tabpanel" id="panel-{id}" aria-labelledby="tab-{id}" hidden={activeTab!==id}>`; ArrowLeft/ArrowRight keyboard navigation on tab bar (AC-002; WCAG 2.1 AA — 2.1.1 Keyboard access)
3. Extracted data tab: renders `summary.entities` — if `entities.length === 0` → `<p className={styles.emptyState}>No clinical entities extracted yet.</p>` (Edge: no entities; not a blank panel); else renders entity cards: type badge (uppercase) + entity value (bold) + `<AiLabel />` badge + confidence indicator `<ConfidenceChip confidence={entity.confidence} lowConfidence={entity.lowConfidence} />`; `<ConfidenceChip>` renders `getConfidenceLabel(confidence)` text + `<ConfidenceIcon aria-hidden="true" />` SVG — text and icon always together, never color alone (AC-003; UXR-105); `ai-label` disclaimer "All entities extracted by AI from uploaded documents. Verify before clinical use." below the badge (UXR-402)
4. `getConfidenceLabel(confidence: number): ConfidenceLevel` helper: returns `'High'` when `confidence >= 0.8`, `'Medium'` when `confidence >= 0.5`, `'Low – unverified'` when `confidence < 0.5`; `<ConfidenceChip>` maps level to icon: High → ✓ (CheckCircle), Medium → ⚠ (Warning), Low → ⚠ with `lowConfidence=true` flag adding `aria-label="Low confidence — verify before clinical use"`; confidence value itself (e.g., "0.72") is shown as the accessible text alternative to the icon (AC-003; UXR-105 — text + icon; no color-only distinction)
5. Documents tab + pagination: renders `summary.documents` as `<table aria-label="Patient documents">`; columns: Document, Uploaded, Status; "Showing {from}–{to} of {totalDocumentCount} documents" caption; Prev/Next `<button>` controls; clicking Prev/Next calls `GET /patients/${id}/summary?page=${page±1}&pageSize=20` and replaces only `summary.documents` and pagination metadata in state (not the entire summary); buttons `disabled` when at first/last page; `aria-label="Previous page of documents"` / `aria-label="Next page of documents"` (Edge: 100+ documents; AC-002 — only current page in memory)
6. Demographics, Intake, and Medical codes tabs: Demographics tab renders `summary.demographics` as a two-column definition table (Full name, DOB, Email, Phone, Insurance) matching wireframe SCR-014; Intake tab renders intake summary fields (Smoking, Alcohol, Exercise, Chief complaint, Submitted) from `summary.demographics.intake` if present, else `<p>No intake form submitted.</p>`; Medical codes tab renders a stub `<p>Medical code review is available in the next section.</p>` with a `<Link to="/medical-codes/${summary.demographics.id}">Review codes →</Link>` matching the wireframe `review-codes-link` target (SCR-015) (AC-002 — all tabs present; wireframe fidelity)

---

## Current Project State
```
src/
└── web/
    └── src/
        ├── pages/
        │   ├── (PatientSearchPage.tsx          — CREATE)
        │   └── (PatientViewPage.tsx            — CREATE)
        ├── components/
        │   └── (AiLabel.tsx                    — CREATE)
        ├── types/
        │   └── (patient.ts                     — CREATE)
        └── pages/
            └── (PatientViewPage.module.css     — CREATE)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | src/web/src/pages/PatientSearchPage.tsx | SCR-013: debounced search input + result list navigation |
| CREATE | src/web/src/pages/PatientViewPage.tsx | SCR-014: patient header + 5-tab 360° view |
| CREATE | src/web/src/components/AiLabel.tsx | Reusable AI extracted badge (--color-ai-accent, UXR-402) |
| CREATE | src/web/src/types/patient.ts | PatientSummaryDto, EntityDto, getConfidenceLabel, ConfidenceLevel |
| CREATE | src/web/src/pages/PatientViewPage.module.css | Tab bar, entity card, AI label, confidence chip, patient header styles |

---

## External References
- .propel/context/wireframes/Hi-Fi/wireframe-SCR-014-360-patient-view.html (SCR-014 — patient header layout; tab bar with `role="tablist"`; `.entity-card` styles; `.ai-label` badge with `--color-ai-accent`/`--color-ai-bg`; conflict banner pattern for UXR-601; `.data-table` for documents/demographics)
- https://www.w3.org/WAI/ARIA/apg/patterns/tabs/ (ARIA tabs pattern — `role="tablist"`, `role="tab"`, `role="tabpanel"`, `aria-selected`, `aria-controls`/`id` association, ArrowLeft/ArrowRight keyboard navigation; WCAG 2.1 AA SC 2.1.1)
- https://react.dev/learn/you-might-not-need-an-effect (React debounce without useEffect — using `useRef` + `clearTimeout` pattern for debounced search; AC-001 — 300ms debounce)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] Type 3 characters in the search field on SCR-013; verify the API call fires after 300ms debounce; verify results list appears with name, DOB, and patient code; click a result and verify navigation to `/patients/{id}/view` (AC-001)
- [ ] Load SCR-014 for a patient with 3 entities; verify the Extracted data tab shows all 3 entity cards each with `<AiLabel>` badge and a confidence chip showing text label + icon (AC-003; UXR-105, UXR-402)
- [ ] Load SCR-014 for a patient with 0 extracted entities; verify Extracted data tab shows "No clinical entities extracted yet." (Edge: empty entities)
- [ ] Load SCR-014 for a patient with 25 documents; verify Documents tab shows page 1 (20 docs), "Showing 1–20 of 25 documents", enabled Next and disabled Prev; click Next; verify page 2 loads (5 docs) and Prev is enabled (Edge: 100+ docs pagination)
- [ ] Navigate to `/patients/{id}/view` as a Patient-role user; verify redirect to patient dashboard without rendering any patient PHI (AC-004 — role-gated routing)
- [ ] Run keyboard navigation test: focus on tab bar, press ArrowRight; verify next tab receives focus and panel changes; verify Enter activates tab (WCAG 2.1 AA 2.1.1 — keyboard access to tabs)
- [ ] Run axe accessibility audit on SCR-013 and SCR-014; verify no violations for missing `aria-label`, tab/tabpanel association, or color-only status indicators (UXR-105, UXR-206, UXR-402; WCAG 2.1 AA)

---

## Implementation Checklist
- [ ] The debounced search uses `AbortController` — each new debounce fire creates a new controller; the previous controller's `abort()` is called before starting the new request so stale results from a slower prior request cannot overwrite newer results (AC-001 — stale response prevention)
- [ ] ARIA tab pattern is fully implemented: each `<button role="tab">` has `aria-selected`, `aria-controls` pointing to its panel `id`, and `tabindex={activeTab===id ? 0 : -1}` (roving tabindex); each panel has `role="tabpanel"`, `aria-labelledby` pointing to its tab `id`, and `hidden` when not active (WCAG 2.1 AA SC 2.1.1, SC 4.1.2 — keyboard and screen reader access)
- [ ] `<ConfidenceChip>` renders confidence level as text (e.g., "High", "Medium", "Low – unverified") in addition to the icon — the icon is `aria-hidden="true"` and the text is always visible; no confidence information is conveyed only through color or icon shape (UXR-105; WCAG 2.1 AA SC 1.4.1)
- [ ] The `<AiLabel>` component renders the literal text "✦ AI extracted" as visible text (not a tooltip or title attribute) — it is not hidden with `aria-hidden`; its purpose is to signal AI provenance, which must be perceivable by screen readers (UXR-402; WCAG 2.1 AA SC 1.3.1)
- [ ] Document page navigation only re-fetches and replaces `documents`, `totalDocumentCount`, `currentPage` in component state — the `demographics`, `entities`, and `activeBookings` are not re-requested on page change (Edge: 100+ documents; performance — no unnecessary re-fetching of stable data)
- [ ] Routes `/patients/search` and `/patients/:id/view` are wrapped in a `<ProtectedRoute allowedRoles={['Staff','Admin','Clinician']}>` component that redirects Patient-role users to their own dashboard without rendering any patient data — PHI is never rendered even momentarily for unauthorised roles (AC-004; OWASP A01)
