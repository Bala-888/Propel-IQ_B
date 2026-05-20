# Task - TASK_002

## Requirement Reference
- **User Story:** us_043
- **Story Location:** .propel/context/tasks/EP-007-II/us_043/us_043.md
- **Acceptance Criteria:**
  - AC-001: SCR-015 page calls `GET /patients/{id}/code-suggestions` and displays up to 10 suggestion cards, each showing `code`, `description`, `confidence`, and codeType
  - AC-004: Each suggestion card shows a "View Evidence" link that expands to display the referenced chunk text — clinician can verify AI reasoning
- **Edge Cases:**
  - Patient has fewer than 3 document chunks: render informational panel with `response.message` text — distinct from an error state
  - Confidence < 0.3: render a "⚠ Low confidence" text + icon badge on the suggestion card (not colour alone per UXR-105)

---

## Design References
| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | N/A |
| **Wireframe Status** | AVAILABLE |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | .propel/context/wireframes/Hi-Fi/wireframe-SCR-015-medical-code-review.html |
| **Screen Spec** | SCR-015 (Medical Code Review) |
| **UXR Requirements** | UXR-105 (confidence level indicator uses text + icon, not color alone); UXR-402 (AI label badge for AI-generated content); UXR-404 (confirm dialog for destructive actions — reserved for future Accept/Reject story) |
| **Design Tokens** | `--font-mono: 'IBM Plex Mono','Courier New',monospace`, `--color-ai-accent: #6366F1`, `--color-ai-bg: #EEF2FF`, `--color-status-success: #16A34A`, `--shadow-1: 0 1px 3px rgba(15,23,42,0.08)` |

---

## AI References
| Reference Type | Value |
|----------------|-------|
| **AI Impact** | Yes |
| **AIR Requirements** | AIR-006 — display AI-generated code suggestions; render "✦ AI suggested" label in page header; render "✦ AI" badge on each suggestion card to indicate AI provenance |
| **AI Pattern** | Display only — no Ollama calls in the frontend; AI label pattern consistent with SCR-014 conflict cards (us_041) |
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
| Frontend | React + TypeScript | 18.x / 5.x | TR-001 — `<MedicalCodePage>` functional component with hooks; `useParams` for patientId; `useState` for suggestions/loading/error; `useEffect` for initial fetch (AC-001) |
| Frontend build | Vite | 5.x | TR-002 — hot module reload for component iteration |
| Frontend routing | React Router v6 | v6 | TR-003 — `/patients/:id/codes` route added to app router; `useParams` for `id` (AC-001) |
| HTTP client | apiClient (project standard) | Project standard | TR-004 — `apiClient.get('/patients/${id}/code-suggestions')`; AbortController for cleanup on unmount; handles 403 → redirect to 403 error page (AC-001, AC-005) |
| Accessibility | WCAG 2.1 AA | Standard | TR-010 — `role="list"` + `role="listitem"` for suggestion cards; `role="meter"` for confidence bar; `role="region"` for evidence expand panel; `role="status"` for insufficient-data message (UXR-105; WCAG SC 1.4.1, SC 4.1.3) |

---

## Task Overview

Implement the SCR-015 Medical Code Review page (`<MedicalCodePage>`) mounted at `/patients/:id/codes`. The page fetches code suggestions on mount, renders AI-labelled suggestion cards with a confidence meter and low-confidence badge, and provides a "View Evidence" expand button per card that reveals the referenced chunk texts and source filenames. An informational empty state is rendered when the API returns the insufficient-data message. Accept/Reject/Correct actions (shown in the wireframe) are reserved for a subsequent story.

---

## Dependent Tasks
- task_001 (this story) — `GET /patients/{id}/code-suggestions` API endpoint must be available; `CodeSuggestionsResponseDto` shape defines the frontend contract

---

## Impacted Components
- `src/web/src/pages/MedicalCodePage.tsx` — new: full SCR-015 page component
- `src/web/src/components/codes/SuggestionCard.tsx` — new: individual suggestion card with confidence, evidence expand
- `src/web/src/router.tsx` — modified: add `/patients/:id/codes` route pointing to `<MedicalCodePage>`

---

## Implementation Plan
1. `/patients/:id/codes` route and `<MedicalCodePage>` component: add `<Route path="/patients/:id/codes" element={<MedicalCodePage />} />` inside the authenticated route guard in `router.tsx`; `<MedicalCodePage>` — `const { id } = useParams<{ id: string }>()`; `const [suggestions, setSuggestions] = useState<CodeSuggestionDto[]>([])`; `const [message, setMessage] = useState<string | null>(null)`; `const [loading, setLoading] = useState(true)`; `const [error, setError] = useState(false)`; `useEffect` on mount: create AbortController, call `apiClient.get('/patients/${id}/code-suggestions', { signal })`, on success set `suggestions` + `message`; on 403 → redirect to `/403`; on network/5xx → `setError(true)`; `finally` → `setLoading(false)`; AbortController cleanup on unmount prevents state updates after component is removed (AC-001; OWASP A01 — 403 handled at API boundary; React 18 stale update prevention)
2. Insufficient-data empty state: render condition — `!loading && !error && suggestions.length === 0 && message !== null`; render `<div role="status" aria-live="polite" class="empty-state-panel">` containing the `message` string from API response, e.g. "Insufficient document data for code suggestions. Please upload clinical documents first."; distinct visual from error state (info icon, neutral colour palette — not red/warning); error state (network/5xx) uses a separate `<div role="alert">` with retry option; both states use `role` attribute for screen reader semantics — the "status" role for the polite message, "alert" for the error (Edge: < 3 chunks; WCAG SC 1.3.1 — meaningful empty state; WCAG SC 4.1.3 — status messages)
3. `<SuggestionList role="list" aria-label="Medical code suggestions">` containing `<SuggestionCard role="listitem" aria-label={`${suggestion.code} ${suggestion.description} — pending`}>` for each suggestion; card structure matching wireframe: code block `<span class="card-code" style={{ fontFamily: 'var(--font-mono)' }}>{code}</span>`; description `<div class="card-description">{description}</div>`; codeType meta `<div class="card-meta">{codeType === 'ICD10' ? 'ICD-10-CM' : 'CPT'} · Source: {supportingChunks[0]?.sourceFilename ?? 'unknown'}</div>`; `<span class="ai-label">✦ AI</span>` badge; `<span class="status-badge pending">Pending</span>` status badge in card header; card receives `accepted` or `rejected` CSS class modifier when those actions are implemented in a future story (AC-001, AC-004; wireframe `suggestion-card` / `card-code` / `card-description` / `card-meta`)
4. Confidence row in each card: `<div class="confidence-row">`: label "Confidence:"; `<div role="meter" aria-valuenow={Math.round(suggestion.confidence * 100)} aria-valuemin={0} aria-valuemax={100} aria-label={`AI confidence ${Math.round(suggestion.confidence * 100)}%`} class="confidence-bar"><div class="confidence-fill conf-high" style={{ width: `${suggestion.confidence * 100}%` }}></div></div>`; numeric value `<span>{suggestion.confidence.toFixed(2)}</span>`; conditional low-confidence badge: `{suggestion.lowConfidence && <span class="status-badge" style={{ background: '#FEF9C3', color: '#854D0E' }}>⚠ Low confidence</span>}` — the `⚠` character (Unicode U+26A0) serves as the icon; the text "Low confidence" provides the semantic label; rendered together, neither element relies on colour alone (UXR-105; Edge: confidence < 0.3; WCAG SC 1.4.1 — no colour-only conveyance)
5. "View Evidence" expand per card: `const [evidenceOpen, setEvidenceOpen] = useState(false)` per `<SuggestionCard>`; `<button class="btn btn-secondary" aria-expanded={evidenceOpen} aria-controls={`evidence-${suggestion.code}`} onClick={() => setEvidenceOpen(o => !o)}>{evidenceOpen ? 'Hide Evidence' : `View Evidence (${suggestion.supportingChunks.length})`}</button>`; expandable `<div id={`evidence-${suggestion.code}`} role="region" aria-label={`Supporting evidence for ${suggestion.code}`} hidden={!evidenceOpen}>` — use CSS `display: none` when hidden (not `visibility: hidden`) so it is fully removed from the accessibility tree when collapsed; for each `supportingChunk`: render `<div class="evidence-chunk">` with `<div class="chunk-source">{chunk.sourceFilename}</div>` + `<p class="chunk-text">{chunk.chunkText.length > 300 ? chunk.chunkText.slice(0,300) + '…' : chunk.chunkText}</p>`; max 300 character excerpt per chunk prevents layout overflow on long clinical texts (AC-004; wireframe evidence section; WCAG SC 2.4.3 — focus order maintained; WCAG SC 1.3.1)
6. Page header + navigation: `<header class="app-header">` with `<a href={`/patients/${id}`} aria-label="Back to patient record" style={{ minHeight: '44px' }}>← Back</a>` and patient name in header; `<main id="main-content" class="content-area">` landmark; `<h1 class="page-title">Medical code review</h1>`; `<div class="page-sub"><span class="ai-label">✦ AI suggested</span> Review AI-suggested medical codes for this patient.</div>`; `<title>` managed via a `<title>Medical code review | UPACIP</title>` — update document title in `useEffect` when patient name is known (wireframe `page-title` + `page-sub`; WCAG SC 2.4.2 — descriptive page title; WCAG SC 2.4.6 — descriptive heading; min-height 44px on the Back link per wireframe)

---

## Current Project State
```
src/
└── web/
    └── src/
        ├── pages/
        │   └── (MedicalCodePage.tsx         — CREATE)
        ├── components/
        │   └── codes/
        │       └── (SuggestionCard.tsx      — CREATE)
        └── router.tsx                        (MODIFY — add /patients/:id/codes route)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | src/web/src/pages/MedicalCodePage.tsx | SCR-015 page component with data fetch, loading, error, empty, and list states |
| CREATE | src/web/src/components/codes/SuggestionCard.tsx | Individual suggestion card with confidence meter, low-confidence badge, View Evidence expand |
| MODIFY | src/web/src/router.tsx | Add /patients/:id/codes route inside authenticated route guard |

---

## External References
- https://www.w3.org/WAI/ARIA/apg/patterns/meter/ (ARIA meter role — `role="meter"` + `aria-valuenow/min/max/label` for confidence progress bar; WCAG SC 4.1.2)
- https://www.w3.org/WAI/ARIA/apg/patterns/disclosure/ (ARIA Disclosure pattern — `aria-expanded` + `aria-controls` on "View Evidence" toggle button; WCAG SC 4.1.2)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] Navigate to `/patients/{id}/codes` as Clinician; verify loading spinner appears, then up to 10 suggestion cards render with code, description, codeType, AI badge, and confidence bar (AC-001)
- [ ] Verify a suggestion with `lowConfidence: true` renders the "⚠ Low confidence" badge with both the `⚠` icon character and the text label; verify the badge does not rely solely on colour for its meaning (UXR-105; WCAG SC 1.4.1)
- [ ] Click "View Evidence" on a suggestion card; verify `aria-expanded` changes to `true`, the evidence panel becomes visible, and at least one chunk text excerpt with `sourceFilename` is displayed (AC-004)
- [ ] Press Tab to reach the "View Evidence" button; press Enter to expand; verify focus remains on the button and the evidence panel appears below it (WCAG SC 2.1.1)
- [ ] Verify the confidence progress bar has `role="meter"`, `aria-valuenow` equal to the rounded percentage value, and `aria-label` including the percentage (WCAG SC 4.1.2)
- [ ] Simulate an API response of `{ suggestions: [], message: "Insufficient document data..." }`; verify the informational panel renders the message text and does NOT trigger the error state styling; verify `role="status"` is present (Edge: < 3 chunks; WCAG SC 4.1.3)
- [ ] Click "← Back"; verify navigation returns to `/patients/{id}` (SCR-014) (wireframe back-btn)

---

## Implementation Checklist
- [ ] The `useEffect` data fetch creates an `AbortController` and passes `signal` to `apiClient.get`; the `useEffect` cleanup calls `controller.abort()` — prevents a stale response from setting state if the user navigates away before the 10-second Ollama pipeline completes (React 18 stale state; AC-001 10-second SLA path)
- [ ] The empty-state info panel (item 2) uses `role="status"` and NOT `role="alert"` — the insufficient-data message is an expected, non-urgent state and should not interrupt screen reader announcements with assertive priority; the error state (network/5xx) correctly uses `role="alert"` (WCAG SC 4.1.3 — status messages; level distinction)
- [ ] The evidence expand panel (item 5) uses `hidden={!evidenceOpen}` which maps to the HTML `hidden` attribute — this removes the element from the accessibility tree (display: none equivalent) when collapsed; `visibility: hidden` would not be acceptable as it keeps the element in the tree with `display: block` layout behaviour (WCAG SC 1.3.1)
- [ ] The `⚠ Low confidence` badge (item 4) includes both the `⚠` Unicode character and the text "Low confidence" in the same visible element — neither is `aria-hidden`; the badge conveys its meaning via text, not solely through colour or the warning icon alone; a screen reader will announce "⚠ Low confidence" (UXR-105; WCAG SC 1.4.1)
- [ ] The `<SuggestionCard>` `aria-label` on `role="listitem"` includes code + description + status (e.g. "J18.9 Pneumonia, unspecified organism — pending") — this allows screen readers to summarise the card content without reading every child element when navigating by landmark; the `aria-label` is updated if the card status changes to Accepted or Rejected in a future story (WCAG SC 1.3.1)
- [ ] Accept/Reject/Correct action buttons visible in the wireframe are NOT implemented in this task — these actions are reserved for a subsequent story (us_044); placeholder `aria-disabled="true"` buttons may be rendered if the wireframe requires visual fidelity, but no onClick handlers are connected; this is documented here as a deliberate scope boundary (DRY — no premature feature creep)
