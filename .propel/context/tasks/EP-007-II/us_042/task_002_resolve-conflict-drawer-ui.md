# Task - TASK_002

## Requirement Reference
- **User Story:** us_042
- **Story Location:** .propel/context/tasks/EP-007-II/us_042/us_042.md
- **Acceptance Criteria:**
  - AC-001: Resolve Conflict drawer (MOD-005) opens from the conflict row on SCR-014; drawer is pre-populated with conflict entity A, entity B, conflict type, and description; drawer shows "Mark Resolved" and "Dismiss" action buttons; resolution note textarea is visible
  - AC-002: Clicking "Mark Resolved" calls `PATCH /clinical-conflicts/{id}/resolve` with `{"resolution": "Resolved", "note": "<text>"}`; on HTTP 200 the drawer closes and the conflict row disappears from SCR-014 without a full page reload
  - AC-003: Clicking "Dismiss" calls `PATCH /clinical-conflicts/{id}/resolve` with `{"resolution": "Dismissed", "note": "<optional text>"}`; on HTTP 200 the drawer closes and the conflict row disappears from SCR-014
  - AC-004: After successful resolution or dismissal the conflict is removed from the SCR-014 open conflicts list immediately via local state update (no re-fetch required)
- **Edge Cases:**
  - 409 from PATCH: display inline `role="alert"` message "This conflict has already been resolved or dismissed." inside the drawer; keep drawer open
  - 400 from PATCH: display the server-returned validation error message inline inside the drawer
  - Network/5xx error: display toast "Unable to submit. Please try again." and keep the drawer open

---

## Design References
| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | N/A |
| **Wireframe Status** | AVAILABLE |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | .propel/context/wireframes/Hi-Fi/wireframe-MOD-005-resolve-conflict-drawer.html |
| **Screen Spec** | SCR-014 (Patient 360° View — Clinical Intelligence), MOD-005 (Resolve Conflict Drawer) |
| **UXR Requirements** | UXR-105 (resolution status uses icon + text, never colour alone), UXR-202 (drawer focus trap — Escape closes, Tab cycles within drawer), UXR-601 (conflict resolution form — rationale textarea + accessible conflict cards) |
| **Design Tokens** | `--color-bg-overlay: rgba(15,23,42,0.48)`, `--shadow-3: 0 8px 24px rgba(15,23,42,0.14)`, `--color-primary`, `--color-primary-subtle`, header background `#FEF2F2` |

---

## AI References
| Reference Type | Value |
|----------------|-------|
| **AI Impact** | No |
| **AIR Requirements** | Display only — the drawer shows "✦ AI flagged this conflict" label (AiLabel badge) to indicate the conflict was AI-detected by the ConflictDetectionWorker; no Ollama calls are made in this story |
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
| Frontend | React + TypeScript | 18.x / 5.x | TR-001 — `<ResolveConflictDrawer>` functional component with hooks; `useState` for note/loading/error; `useEffect` for focus-on-open; `useRef` for focus trap boundary elements (AC-001; UXR-202) |
| Frontend build | Vite | 5.x | TR-002 — hot module reload during drawer development |
| Frontend routing | React Router v6 | v6 | TR-003 — `PatientViewPage` manages drawer state; no route change needed — drawer is in-page overlay |
| HTTP client | apiClient (axios / fetch wrapper) | Project standard | TR-004 — `apiClient.patch('/clinical-conflicts/${id}/resolve', { resolution, note })` via named instance; AbortController for in-flight cancellation if drawer is closed during request (AC-002, AC-003) |
| Accessibility | WCAG 2.1 AA | Standard | TR-010 — `role="dialog"` + `aria-modal="true"` + `aria-labelledby="drawer-title"`; focus trap (SC 2.1.2); Escape key closes; `role="alert"` for inline errors (UXR-202; OWASP A11y) |

---

## Task Overview

Implement the MOD-005 Resolve Conflict drawer as a `<ResolveConflictDrawer>` component with focus trap, Escape-to-close, and keyboard accessibility. Wire the "Resolve conflict →" stub button in `<ConflictBanner>` (us_041) to open the drawer. The drawer displays the conflicting entity cards with the AI-flagged label, a resolution note textarea with live character counter, and "Mark Resolved" / "Dismiss" / "Cancel" footer buttons. On success the resolved conflict is removed from SCR-014 state without a re-fetch.

---

## Dependent Tasks
- task_001 (this story) — `PATCH /clinical-conflicts/{id}/resolve` endpoint must be available
- task_002 (us_041) — `<ConflictBanner>` and `ConflictDto` type must exist; "Resolve conflict →" button stub is the integration point
- task_002 (us_040) — `PatientViewPage` and `PatientSummaryDto` (with `conflicts` array) must exist

---

## Impacted Components
- `src/web/src/components/conflicts/ResolveConflictDrawer.tsx` — new: full drawer component
- `src/web/src/components/conflicts/ConflictBanner.tsx` — modified (us_041): wire `onResolve` prop; replace `console.warn` stub
- `src/web/src/pages/PatientViewPage.tsx` — modified (us_040): add drawer state; add `handleConflictResolved` callback; render `<ResolveConflictDrawer>`

---

## Implementation Plan
1. `<ResolveConflictDrawer>` component skeleton with focus trap: props — `open: boolean`, `conflict: ConflictDto | null`, `onClose: () => void`, `onResolved: (conflictId: string) => void`; render a `<div role="dialog" aria-modal="true" aria-labelledby="drawer-title" aria-describedby="drawer-desc">` at 360px width sliding in from right (CSS transition `transform: translateX(0)` when open); implement focus trap via `useRef` for `firstFocusable` and `lastFocusable` — `useEffect` on `open: true` sets `firstFocusable.current?.focus()`; `onKeyDown` handler: if `Escape` → call `onClose()`; if `Tab` without Shift and focus is on last focusable → move focus to first; if `Shift+Tab` and focus is on first focusable → move focus to last; semi-transparent overlay `rgba(15,23,42,0.48)` behind drawer — click overlay calls `onClose()` (UXR-202; WCAG 2.1 AA SC 2.1.2 — no keyboard trap)
2. Wire `<ConflictBanner>` (us_041 component): add prop `onResolve: (conflict: ConflictDto) => void` to `ConflictBanner`; replace the `console.warn("TODO: open resolver")` stub inside the "Resolve conflict →" `<button>` onClick with `props.onResolve(conflict)`; in `PatientViewPage` (us_040): add state `const [drawerConflict, setDrawerConflict] = useState<ConflictDto | null>(null)`; pass `onResolve={(c) => setDrawerConflict(c)}` to each `<ConflictBanner conflict={c}`; render `<ResolveConflictDrawer open={drawerConflict !== null} conflict={drawerConflict} onClose={() => setDrawerConflict(null)} onResolved={handleConflictResolved} />` at the bottom of the page component tree (AC-001; completes the us_041 stub)
3. Drawer body — conflict detail section: header bar with `background: #FEF2F2` — title `<h2 id="drawer-title">Resolve conflict</h2>` with error icon; subtitle `<p id="drawer-desc">{conflict.entityA.type} conflict — {conflict.description}</p>`; close `<button aria-label="Close conflict resolution drawer" onClick={onClose}>×</button>`; below header: `<span class="ai-label">✦ AI flagged this conflict</span>`; two side-by-side entity cards styled with `border: 1px solid #FECACA; background: #FEF2F2` each wrapped in `<div role="group" aria-label="Conflicting {entityType}: {entityValue}">`; separator `<div aria-hidden="true">↑ conflicts with ↓</div>` between cards (AC-001; wireframe MOD-005 `conflict-entities` section; UXR-105 — icon + text not colour alone; WCAG SC 1.4.1 — no colour-only distinction)
4. Resolution note textarea with live character counter: `<label htmlFor="resolution-notes">Resolution note{resolution === 'Resolved' ? ' (required)' : ' (optional)'}</label>`; `<textarea id="resolution-notes" aria-label="Resolution note" aria-describedby="note-hint note-required-hint" maxLength={1000} value={note} onChange={e => setNote(e.target.value)} rows={4} style={{ minHeight: '80px' }} />`; `<span id="note-hint">{note.length}/1,000 characters</span>`; `<span id="note-required-hint" aria-live="polite">{resolution === 'Resolved' && 'Required for Mark Resolved'}</span>`; counter updates on every keystroke; `maxLength={1000}` provides client-side enforcement; server enforces the same 1,000 character limit via `[MaxLength(1000)]` on the DTO (AC-001; Edge: note > 1000 — client-side maxLength + server validation; WCAG SC 1.3.1)
5. Footer buttons with loading + error states: `const [loading, setLoading] = useState(false)`; `const [inlineError, setInlineError] = useState<string | null>(null)`; `<div role="alert" aria-live="assertive">{inlineError}</div>` — initially empty, shown with error text on 409/400 (aria-live assertive ensures screen reader announces error immediately); `<button onClick={onClose} disabled={loading}>Cancel</button>`; `<button onClick={() => handleSubmit('Dismissed')} disabled={loading}>Dismiss</button>`; `<button onClick={() => handleSubmit('Resolved')} disabled={loading || !note.trim()}>Mark Resolved</button>` — "Mark Resolved" disabled when note is empty; `handleSubmit(resolution)` sets `loading = true`, clears `inlineError`; calls `apiClient.patch('/clinical-conflicts/${conflict!.id}/resolve', { resolution, note })`; on 200 → `onResolved(conflict!.id)` + `setDrawerConflict(null)` (closes via parent); on 409 → `setInlineError("This conflict has already been resolved or dismissed.")`; on 400 → `setInlineError(err.response.data.error)`; on network/5xx → `showToast("Unable to submit. Please try again.")`; `finally` → `setLoading(false)` (AC-002, AC-003; Edge: 409, 400, network error; min-height 44px on all buttons per wireframe)
6. `handleConflictResolved(conflictId)` in `PatientViewPage`: `setSummary(prev => prev ? { ...prev, conflicts: prev.conflicts.filter((c: ConflictDto) => c.id !== conflictId) } : prev)` — removes the resolved/dismissed conflict from the in-memory `PatientSummaryDto.conflicts` array; `<ConflictBanner>` for that conflict unmounts immediately; no API re-fetch needed since the state update is local and the next full reload of the page will naturally return only Open conflicts from the API (AC-004; UXR-105 — conflict disappears visually and from accessible DOM tree)

---

## Current Project State
```
src/
└── web/
    └── src/
        ├── components/
        │   └── conflicts/
        │       ├── ConflictBanner.tsx          (MODIFY — us_041; add onResolve prop, replace stub)
        │       └── (ResolveConflictDrawer.tsx  — CREATE)
        └── pages/
            └── PatientViewPage.tsx             (MODIFY — us_040; add drawer state + handler)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | src/web/src/components/conflicts/ResolveConflictDrawer.tsx | Full drawer component — focus trap, conflict display, note textarea, submit/cancel buttons |
| MODIFY | src/web/src/components/conflicts/ConflictBanner.tsx | Add onResolve prop; replace console.warn stub with props.onResolve(conflict) |
| MODIFY | src/web/src/pages/PatientViewPage.tsx | Add drawerConflict state, handleConflictResolved, and <ResolveConflictDrawer> render |

---

## External References
- https://www.w3.org/WAI/ARIA/apg/patterns/dialog-modal/ (ARIA Dialog Modal Pattern — focus trap implementation; Tab/Shift+Tab cycling; Escape to close; aria-modal; UXR-202; WCAG 2.1 AA SC 2.1.2)
- https://developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/Attributes/aria-live (aria-live="assertive" for inline conflict-already-resolved error banner; WCAG SC 4.1.3)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [x] Click "Resolve conflict →" on a conflict banner in SCR-014; verify MOD-005 drawer opens with conflict entity A and entity B visible, AI-flagged label shown, and resolution note textarea visible (AC-001)
- [x] Verify drawer receives focus on open (first focusable element) and Tab key cycles within the drawer without focus escaping to the page behind (UXR-202; WCAG 2.1 AA SC 2.1.2)
- [x] Press Escape while drawer is open; verify drawer closes and focus returns to the "Resolve conflict →" button that triggered it (UXR-202; WCAG 2.1 AA SC 2.1.2)
- [x] Type 1,001 characters into the note textarea; verify the textarea enforces the 1,000-character `maxLength` client-side and the character counter shows "1,000/1,000 characters" (Edge: note > 1,000)
- [x] Leave the note textarea empty and click "Mark Resolved"; verify the button remains disabled (note is required for Resolved) — the API is not called (AC-002)
- [x] Submit a resolution with note text for "Mark Resolved"; verify HTTP PATCH is called, drawer closes on 200, and the conflict banner disappears from SCR-014 without a full page reload (AC-002, AC-004)
- [x] Submit a dismissal with no note (click "Dismiss" with empty textarea); verify API is called with `{"resolution": "Dismissed", "note": null}`, drawer closes on 200, and conflict disappears from SCR-014 (AC-003, AC-004)
- [x] Simulate a 409 response; verify inline `role="alert"` message "This conflict has already been resolved or dismissed." appears inside the drawer and the drawer stays open (Edge: 409)
- [x] Verify screen reader announces the inline error message immediately after submission failure (role="alert" + aria-live="assertive"; WCAG SC 4.1.3)

---

## Implementation Checklist
- [x] `role="dialog"` + `aria-modal="true"` + `aria-labelledby="drawer-title"` are all present on the drawer root element — these three attributes together are required by the ARIA dialog modal pattern; missing any one of them breaks screen reader interpretation (UXR-202; WCAG 2.1 AA)
- [x] Focus trap is implemented with `useRef` for the first and last focusable elements — the trap fires on `keydown` Tab/Shift+Tab events on the drawer root element, not on individual children; `e.preventDefault()` is called before moving focus manually to prevent the browser default scroll behaviour (WCAG 2.1 AA SC 2.1.2)
- [x] On drawer open (`open` transitions from false to true), a `useEffect` immediately focuses `firstFocusableRef.current`; on close, focus is returned to the element that triggered the drawer open (stored in a `triggerRef` or by capturing `document.activeElement` before opening) (UXR-202)
- [x] The conflict entity cards use `role="group"` + `aria-label="Conflicting {entityType}: {entityValue}"` — these are semantic groups, not interactive elements; the separator "↑ conflicts with ↓" uses `aria-hidden="true"` to prevent redundant screen reader announcement (WCAG SC 1.3.1; UXR-105)
- [x] The inline error `<div role="alert" aria-live="assertive">` is always present in the DOM (rendered empty, not conditionally mounted) so that dynamic content changes are announced; conditionally mounting/unmounting the element resets the aria-live region and announcements may be missed (WCAG SC 4.1.3)
- [x] The `handleSubmit` function uses an AbortController tied to the drawer's open state — if the user closes the drawer while a PATCH is in flight, the request is aborted and `setLoading(false)` is still called in `finally`; this prevents the `onResolved` callback from firing on a stale response (React 18 state-update-after-unmount warning prevention)
