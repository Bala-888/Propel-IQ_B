# Task - TASK_002

## Requirement Reference
- **User Story:** us_039
- **Story Location:** .propel/context/tasks/EP-007-I/us_039/us_039.md
- **Acceptance Criteria:**
  - AC-001: SCR-010 polls `GET /documents/{id}/status` every 5 seconds and reflects the current status in a progress stepper showing `Uploaded → TextExtracted → Chunked → EmbeddingsComplete → EntitiesExtracted`; polling stops when status reaches `"EntitiesExtracted"`
  - AC-003: When status is `TimedOut`, the stepper shows an error state at the current step, an error icon + text message "Document processing failed. Please try again.", and a visible "Retry" button — meets UXR-603 (icon + text, not color alone)
  - AC-004: Clicking "Retry" calls `POST /documents/{id}/retry`; on 200, the stepper resets to the first step and polling resumes
  - AC-005: `ExtractionFailed` status shows the same Retry CTA as `TimedOut`
- **Edge Cases:**
  - `POST /documents/{id}/retry` returns 409: display inline message "Document processing is already complete." without clearing the stepper or navigating away
  - Network error on retry: display toast "Unable to retry. Please check your connection." — retry button remains enabled

---

## Design References
| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes — SCR-010 Document Processing Status |
| **Figma URL** | N/A |
| **Wireframe Status** | AVAILABLE |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | .propel/context/wireframes/Hi-Fi/wireframe-SCR-010-document-processing-status.html |
| **Screen Spec** | SCR-010 (Document Processing Status) |
| **UXR Requirements** | UXR-603 (error state shows icon + text + Retry CTA, never color alone); UXR-503 (polled status with aria-live indicator); UXR-206 (aria-live for status changes) |
| **Design Tokens** | `--color-status-success: #16A34A`, `--color-status-error: #DC2626`, `--color-primary: #1A56DB`, `--color-text-secondary: #475569`, `--color-bg-subtle: #F1F5F9`, `--color-border: #E2E8F0`, font: `IBM Plex Sans` |

---

## AI References
| Reference Type | Value |
|----------------|-------|
| **AI Impact** | No |
| **AIR Requirements** | AIR-008 — SCR-010 surfaces the outcome of the AI extraction pipeline to the patient; the "TimedOut" and "ExtractionFailed" states reflect AI pipeline failure modes |
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
| Frontend | React + TypeScript | 18.x / 5.x | TR-001 — `DocumentStatusPage` component; `<PipelineStepper>` component; `useEffect` + `setInterval` polling pattern (AC-001, AC-003–005) |
| Routing | React Router v6 | v6 | TR-001 — `<Route path="/documents" element={<DocumentStatusPage />} />` (SCR-010 route) |
| HTTP Client | apiClient (Axios/Fetch wrapper) | project standard | TR-001 — `GET /documents`, `GET /documents/{id}/status`, `POST /documents/{id}/retry`; JWT bearer from auth context (AC-001, AC-004; OWASP A01) |
| Styling | CSS custom properties | project standard | TR-002 — design tokens from wireframe: `--color-status-error`, `--color-status-success`, `--color-primary`; IBM Plex Sans font; step dot and connector styles from SCR-010 wireframe (AC-001, AC-003; UXR-603) |

---

## Task Overview

Implement the SCR-010 Document Processing Status page as a React component. On mount, it fetches all patient documents (`GET /documents`). For each document in an in-progress state, a 5-second polling interval calls `GET /documents/{id}/status` and updates the `DocumentStatusDto` in local state. A `<PipelineStepper>` component maps the current `DocumentStatus` to a visual step sequence with done/active/pending/error dot states. For `TimedOut` or `ExtractionFailed` states, a `role="alert"` region renders an error icon, a descriptive text message, and a Retry button, fulfilling UXR-603. The Retry handler calls `POST /documents/{id}/retry`, handles 200/409/network responses, and resets or preserves the stepper state accordingly.

---

## Dependent Tasks
- task_001 (us_039) — `GET /documents`, `GET /documents/{id}/status`, and `POST /documents/{id}/retry` API endpoints must exist and be deployed before this UI can function
- task_002 (us_035) — `DocumentUploadPage` (SCR-009) must exist to provide the navigation entry point that lands on SCR-010 after upload

---

## Impacted Components
- `src/web/src/pages/DocumentStatusPage.tsx` — new: SCR-010 page; polling logic; document list rendering
- `src/web/src/components/PipelineStepper.tsx` — new: 5-step pipeline stepper with done/active/pending/error states; full ARIA labels
- `src/web/src/types/document.ts` — new (or modified if exists): `DocumentStatus` type union; `DocumentStatusDto` interface; `PIPELINE_STEPS` constant; `getStepState` helper
- `src/web/src/pages/DocumentStatusPage.module.css` — new: step dot, connector, status card, and retry CTA styles matching SCR-010 wireframe design tokens

---

## Implementation Plan
1. TypeScript types and constants in `src/web/src/types/document.ts`: `type DocumentStatus = 'Uploaded' | 'TextExtracted' | 'Chunked' | 'EmbeddingsComplete' | 'EntitiesExtracted' | 'TimedOut' | 'ExtractionFailed'`; `interface DocumentStatusDto { id: string; fileName: string; status: DocumentStatus; processingStartedAt: string }`; `const PIPELINE_STEPS: { key: DocumentStatus; label: string }[] = [{ key: 'Uploaded', label: 'Pending' }, { key: 'TextExtracted', label: 'Extracting' }, { key: 'Chunked', label: 'Chunking' }, { key: 'EmbeddingsComplete', label: 'Embedding' }, { key: 'EntitiesExtracted', label: 'Complete' }]`; `const TERMINAL_FAIL_STATUSES: DocumentStatus[] = ['TimedOut', 'ExtractionFailed']`; `function getStepState(status: DocumentStatus, stepIndex: number): 'done' | 'active' | 'pending' | 'error'` — returns `'error'` for the highest completed step when status is a terminal failure, `'done'` for steps before current, `'active'` for current, `'pending'` for future (AC-001 — exhaustive union prevents unknown statuses reaching render)
2. `DocumentStatusPage` component: `useEffect` on mount calls `apiClient.get<DocumentStatusDto[]>('/documents')` to populate `documents` state; for each document where `!['EntitiesExtracted'].includes(doc.status)`, start a `setInterval(5000)` that calls `apiClient.get<DocumentStatusDto>('/documents/${doc.id}/status')` with `AbortController` — on response, update the matching document in `documents` state via `setDocuments(prev => prev.map(d => d.id === doc.id ? updated : d))`; clear interval when status becomes `'EntitiesExtracted'`; `aria-live="polite"` on polling status text in header (AC-001; UXR-503)
3. `<PipelineStepper status={status} />` component: maps `PIPELINE_STEPS` using `getStepState(status, index)` to render step dots; `'done'` → green ✓ dot (`background: var(--color-status-success)`); `'active'` → blue spinner dot (`background: var(--color-primary)` + `<span className="spinner" aria-hidden="true" />`); `'pending'` → grey … dot (`background: var(--color-border)`); `'error'` → red ✗ dot (`background: var(--color-status-error)`); connectors styled green when predecessor is `'done'`; each dot `aria-label="{step.label} — {stepState}"` for screen readers; stepper container `role="img"` + `aria-label="Processing pipeline"` (wireframe SCR-010; UXR-206 — aria-live for status changes)
4. Error/retry CTA block: conditionally renders when `TERMINAL_FAIL_STATUSES.includes(doc.status)`: `<div role="alert" aria-live="assertive" className={styles.retryAlert}><svg aria-hidden="true" className={styles.errorIcon}>{/* error icon path */}</svg><p id={`retry-desc-${doc.id}`}>Document processing failed. Please try again.</p><button id="retry-btn" aria-describedby={`retry-desc-${doc.id}`} onClick={() => handleRetry(doc.id)} disabled={retryLoading[doc.id]}>Retry</button></div>` — icon and text always rendered together; error icon `aria-hidden="true"` + visible text ensures UXR-603 is met; `disabled` during in-flight retry to prevent double-submit (AC-003, AC-005; UXR-603)
5. `handleRetry(documentId: string)` async function: sets `retryLoading[documentId] = true`; calls `apiClient.post<void>('/documents/${documentId}/retry')`; on 200 → `setDocuments(prev => prev.map(d => d.id === documentId ? {...d, status: 'Uploaded'} : d))`, clear `role="alert"`, restart poll interval; on 409 → set `retryError[documentId] = "Document processing is already complete."` displayed in `<p role="status" aria-live="polite">` below the Retry button without clearing the stepper; on any `Error` thrown → dispatch toast notification "Unable to retry. Please check your connection."; finally sets `retryLoading[documentId] = false` (AC-004; Edge: 409, network error; OWASP A03 — no internal error details exposed to UI)
6. Status card per document: `role="listitem"` + `aria-label="{doc.fileName}, {statusLabel}"` on card wrapper; badge label mapped from status via `STATUS_LABELS: Record<DocumentStatus, string>` constant; completed documents (`'EntitiesExtracted'`) show "View extracted data →" `<a href={/patients/${patientId}}>` link to SCR-014; file name rendered with `title={doc.fileName}` for overflow and truncated to 40 chars in display; list container `role="list"` + `aria-label="Document processing status list"` on outer `<div>` (wireframe structure; WCAG 2.1 AA — all status information conveyed via text/role, not color alone)

---

## Current Project State
```
src/
└── web/
    └── src/
        ├── pages/
        │   └── (DocumentStatusPage.tsx          — CREATE)
        ├── components/
        │   └── (PipelineStepper.tsx             — CREATE)
        ├── types/
        │   └── (document.ts                     — CREATE)
        └── pages/
            └── (DocumentStatusPage.module.css   — CREATE)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | src/web/src/pages/DocumentStatusPage.tsx | SCR-010 page: document list, per-document polling, stepper, retry CTA |
| CREATE | src/web/src/components/PipelineStepper.tsx | 5-step pipeline stepper with done/active/pending/error states and full ARIA |
| CREATE | src/web/src/types/document.ts | DocumentStatus type, DocumentStatusDto interface, PIPELINE_STEPS, getStepState |
| CREATE | src/web/src/pages/DocumentStatusPage.module.css | Step dot, connector, status card, retry alert styles using SCR-010 design tokens |

---

## External References
- .propel/context/wireframes/Hi-Fi/wireframe-SCR-010-document-processing-status.html (SCR-010 — step dot classes: `.step-dot.done`, `.step-dot.active`, `.step-dot.pending`, `.step-dot.error`; connector `.step-connector.done`; retry button `#retry-btn`; `aria-live="polite"` on polling indicator; design tokens in `:root` block)
- https://react.dev/reference/react/useEffect#fetching-data-with-effects (React useEffect with AbortController for cleanup — prevents stale state on unmount; AC-001)
- https://www.w3.org/WAI/WCAG21/Techniques/aria/ARIA19 (ARIA live regions — `role="alert"` for error state, `aria-live="polite"` for status updates; UXR-206, UXR-603)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] Load SCR-010 with a document in `'TextExtracted'` state; verify the stepper shows step 1 (Pending) as done, step 2 (Extracting) as active, steps 3–5 as pending; verify `GET /documents/{id}/status` is called at 5-second intervals (AC-001)
- [ ] Simulate `GET /documents/{id}/status` returning `'EntitiesExtracted'`; verify polling interval is cleared and no further requests are made (AC-001 — polling stops)
- [ ] Set document status to `'TimedOut'`; verify the stepper shows an error dot; verify `<div role="alert">` is present with error icon, error text "Document processing failed. Please try again.", and Retry button visible; verify no color-only indication of failure (AC-003; UXR-603)
- [ ] Click Retry when status is `'TimedOut'`; verify `POST /documents/{id}/retry` is called; on mocked 200 response, verify stepper resets to `'Uploaded'` state and poll resumes (AC-004)
- [ ] Mock `POST /documents/{id}/retry` to return 409; verify inline message "Document processing is already complete." appears without navigation or stepper reset (Edge: 409)
- [ ] Set document status to `'ExtractionFailed'`; verify same Retry CTA as `'TimedOut'` is displayed (AC-005)
- [ ] Run axe or Lighthouse accessibility audit on SCR-010; verify no missing `aria-label` on stepper dots, no color-only error states, `role="list"` + `role="listitem"` on document list (WCAG 2.1 AA; UXR-206, UXR-603)

---

## Implementation Checklist
- [x] The `type DocumentStatus` union is exhaustive and used for all status comparisons — no raw string comparisons like `status === "TimedOut"` without importing the type; TypeScript `never` fallthrough in `getStepState` switch/if-else catches unhandled future status values at compile time (AC-001 — type safety)
- [x] The polling `setInterval` uses `useRef` to store the interval ID per document; the cleanup function in `useEffect` clears all active intervals via `clearInterval(intervalRef.current[id])` on component unmount — no interval continues after the component is removed from the DOM (AC-001 — memory leak prevention)
- [x] Each polling request is created with a `new AbortController()`; the `AbortController.signal` is passed to the fetch/apiClient call; the `useEffect` cleanup calls `controller.abort()` — stale responses that arrive after abort do not update state (AC-001 — stale closure protection)
- [x] The error/retry CTA block uses `role="alert"` with `aria-live="assertive"` so screen readers announce the failure immediately when it appears; the success/polling indicator uses `aria-live="polite"` to avoid interrupting screen reader flow (UXR-206, UXR-603; WCAG 2.1 AA SC 4.1.3)
- [x] The Retry button is `disabled` during an in-flight retry request (`retryLoading[doc.id] === true`) — the button cannot be double-clicked to submit two concurrent retry requests; `disabled` attribute is removed once the request resolves (AC-004 — prevents client-side double-submit)
- [x] `STATUS_LABELS` provides a human-readable label for every `DocumentStatus` value including `'TimedOut'` and `'ExtractionFailed'` — the badge rendered on the status card is never the raw API status string (WCAG 2.1 AA SC 3.1.1 — comprehensible labels; UXR-603)
