# Task - TASK_002

## Requirement Reference
- **User Story:** us_044
- **Story Location:** .propel/context/tasks/EP-007-II/us_044/us_044.md
- **Acceptance Criteria:**
  - AC-001: Each suggestion card on SCR-015 displays "Accept", "Reject", and "Correct" inline action buttons — all with icon + text label and accessible `aria-label` per code
  - AC-002: Accept → calls `POST /patients/{id}/medical-codes`; on 201 the card updates to "Accepted" visual state; on 409 inline "already reviewed" message
  - AC-003: Reject → UXR-404 confirm dialog → calls `PATCH /code-suggestions/{id}`; on 200 card updates to "Rejected" state (strikethrough + dashed border); on 409 inline error
  - AC-004: Correct → inline edit field pre-filled with AI-suggested code → submit calls `POST /patients/{id}/medical-codes` with `source: "AI-Corrected"`; card updates to "Corrected" state
  - AC-005: Inline validation on the correction input — if code fails ICD-10/CPT pattern, show error without clearing the entered value
- **Edge Cases:**
  - 409 from either endpoint: display inline `role="alert"` "This suggestion has already been reviewed." inside the card; keep card visible
  - All suggestions rejected: render `<div role="status">All suggestions have been reviewed. No codes were added.</div>` — not a blank empty state

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
| **UXR Requirements** | UXR-105 (review status uses icon + text label, not color alone); UXR-404 (destructive confirm dialog for Reject action) |
| **Design Tokens** | `--color-status-success: #16A34A`, `--color-status-error: #DC2626`, `--color-destructive: #DC2626`, `--color-destructive-hover: #B91C1C`, `--font-mono: 'IBM Plex Mono','Courier New',monospace`, `--shadow-3: 0 8px 24px rgba(15,23,42,0.14)` |

---

## AI References
| Reference Type | Value |
|----------------|-------|
| **AI Impact** | Yes |
| **AIR Requirements** | AIR-007 — human-in-loop display: "✦ AI" badge on each card indicates AI provenance; Accept/Reject/Correct actions record the clinician's human decision on the AI-generated suggestion; `source = "AI"` vs `source = "AI-Corrected"` distinction displayed post-action |
| **AI Pattern** | Human-in-loop review — no Ollama calls from the frontend; the AI label on the card communicates that the suggestion was AI-generated (established pattern from SCR-014 conflict cards in us_041) |
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
| Frontend | React + TypeScript | 18.x / 5.x | TR-001 — `<SuggestionCard>` updated with `onAccept/onReject/onCorrect` props; `useState` per card for `isEditing`, `editValue`, `inlineError`, `loading`; `MedicalCodePage` manages `cardStates` record (AC-001–005) |
| Frontend build | Vite | 5.x | TR-002 — hot module reload for iterative development |
| Frontend routing | React Router v6 | v6 | TR-003 — existing `/patients/:id/codes` route from us_043 |
| HTTP client | apiClient (project standard) | Project standard | TR-004 — `apiClient.post('/patients/${id}/medical-codes', ...)` for Accept/Correct; `apiClient.patch('/code-suggestions/${suggestionId}', ...)` for Reject; per-request loading state (AC-002, AC-003, AC-004) |
| Accessibility | WCAG 2.1 AA | Standard | TR-010 — `role="dialog"` + `aria-modal` for reject confirm; `role="alert"` for inline 409 error; `aria-expanded` on Correct toggle; `role="status"` for all-rejected empty state (UXR-404; WCAG SC 2.1.1, 4.1.3) |

---

## Task Overview

Update `<SuggestionCard>` (us_043) to activate the Accept, Reject, and Correct action buttons. Accept calls `POST /patients/{id}/medical-codes` and transitions the card to an accepted visual state. Reject opens a UXR-404 confirm dialog before calling `PATCH /code-suggestions/{id}` and transitions the card to a struck-through rejected state. Correct reveals the wireframe's inline edit field with client-side ICD-10/CPT validation. `MedicalCodePage` manages `cardStates` keyed by suggestion ID and detects the all-rejected empty state. All three actions handle 409 with inline error messages.

---

## Dependent Tasks
- task_001 (this story) — `POST /patients/{id}/medical-codes` and `PATCH /code-suggestions/{id}` must be available; `CodeSuggestionDto` must include `id` and `reviewStatus`
- task_002 (us_043) — `<SuggestionCard>` and `<MedicalCodePage>` must exist with placeholder action buttons and `cardStates` scaffolding

---

## Impacted Components
- `src/web/src/components/codes/SuggestionCard.tsx` — modified (us_043): activate Accept/Reject/Correct; add inline edit field; add reject confirm dialog; add card-level loading + inline error
- `src/web/src/pages/MedicalCodePage.tsx` — modified (us_043): add `handleAccept`, `handleReject`, `handleCorrect` callbacks; detect all-rejected state; render all-reviewed empty state banner
- `src/web/src/types/codes.ts` — modified (us_043, new in this story if not created): add `ReviewStatus` union type; update `CodeSuggestionDto` interface with `id: string` and `reviewStatus: ReviewStatus`

---

## Implementation Plan
1. Activate action buttons in `<SuggestionCard>`: remove `aria-disabled="true"` placeholder from the three buttons (us_043 stub); wire `onClick={onAccept}`, `onClick={onReject}`, `onClick={() => setIsEditing(true)}`; each button labelled `aria-label="Accept {code}"` / `aria-label="Reject {code}"` / `aria-label="Correct {code}" aria-expanded={isEditing}`; buttons rendered only when `reviewStatus === 'Pending'`; the "Correct" button uses `aria-expanded` to signal the inline edit panel state; all three buttons: icon character (✓ / ✗ / ✎) + text label in the same element — neither alone; `min-height: 44px` per wireframe (AC-001; UXR-105 — icon + text; WCAG SC 1.4.1; WCAG SC 2.1.1)
2. Accept action in `MedicalCodePage`: `handleAccept = async (suggestion: CodeSuggestionDto)` → `setCardLoading(suggestion.id, true)` (per-card loading, not page-wide); `apiClient.post('/patients/${id}/medical-codes', { suggestionId: suggestion.id, codeType: suggestion.codeType, code: suggestion.code, description: suggestion.description, source: 'AI', reviewStatus: 'Accepted' })`; on 201 → `setCardStatus(suggestion.id, 'Accepted')`; on 409 → `setCardInlineError(suggestion.id, "This suggestion has already been reviewed.")`; on network/5xx → toast "Unable to submit. Please try again."; `finally` → `setCardLoading(suggestion.id, false)`; once `reviewStatus === 'Accepted'` the card applies `suggestion-card accepted` CSS class (`border-color:#BBF7D0; background:#F0FDF4`) and status badge changes to `<span class="status-badge accepted">✓ Accepted</span>` — text + icon (AC-002; Edge: 409; wireframe `suggestion-card.accepted`)
3. Reject action with UXR-404 confirm dialog: `handleRejectClick(suggestion)` → sets `pendingRejectId = suggestion.id` (lifted state in `MedicalCodePage`); renders `<div role="dialog" aria-modal="true" aria-labelledby="reject-dialog-title" aria-describedby="reject-dialog-body" class="dialog-overlay open">` with `<div class="dialog">`; title `<h2 id="reject-dialog-title" class="dialog-title">Reject this code?</h2>`; body `<p id="reject-dialog-body" class="dialog-body">This code will not be added to the patient record. This action cannot be undone.</p>`; footer: `<button onClick={closeDialog} class="btn btn-secondary">Cancel</button>` + `<button onClick={confirmReject} class="btn btn-destructive">Reject</button>`; `confirmReject` → `apiClient.patch('/code-suggestions/${pendingRejectId}', { reviewStatus: 'Rejected' })`; on 200 → `setCardStatus(pendingRejectId, 'Rejected')` → applies `suggestion-card rejected` CSS class (`opacity: 0.5; border-style: dashed`); status badge `<span class="status-badge rejected">✗ Rejected</span>`; dialog dismissed; Escape closes dialog (refocuses to trigger button); on 409 → dialog closes + inline card error (AC-003; UXR-404 — confirm for destructive reject; wireframe `dialog-overlay` / `dialog`; WCAG SC 2.1.1)
4. Correct action — inline edit field and submission: within `<SuggestionCard>`, `const [isEditing, setIsEditing] = useState(false)`; `const [editValue, setEditValue] = useState(suggestion.code)` — initialised to AI-suggested code; on `onCorrect` prop call → `setIsEditing(true)`; render `<div class="correct-field open" aria-label={`Code correction for ${suggestion.code}`}>` containing `<input type="text" class="correct-input" value={editValue} onChange={e => setEditValue(e.target.value)} aria-label={`Corrected ${suggestion.codeType} code`} aria-describedby={`correct-hint-${suggestion.id}`} />` + `<div id={`correct-hint-${suggestion.id}`} class="correct-hint">{suggestion.codeType === 'ICD10' ? 'Format: [A-Z][0-9]{2}.[0-9]{0,4}' : 'Format: 5 digits'}</div>`; `{correctionError && <span role="alert">{correctionError}</span>}` — always in DOM, conditionally populated; Cancel button → `setIsEditing(false); setEditValue(suggestion.code); setCorrectionError('')` (AC-004, AC-005; wireframe `correct-field.open` / `correct-input` / `correct-hint`)
5. Correction submission with client-side validation: `submitCorrection` in `SuggestionCard` → validate `editValue` against `icd10Pattern = /^[A-Z][0-9]{2}(\.[0-9A-Z]{1,4})?$/` (if codeType ICD10) or `cptPattern = /^\d{5}$/` (if CPT); if invalid → `setCorrectionError("Invalid code format. ICD-10 codes must match [A-Z][0-9]{2}([...]) and CPT codes must be 5 digits.")` — `editValue` is NOT cleared on error (AC-005 requirement); if valid → call `onCorrect(suggestion, editValue)` prop; in `MedicalCodePage`: `handleCorrect(suggestion, correctedCode)` → `apiClient.post('/patients/${id}/medical-codes', { ..., source: 'AI-Corrected', reviewStatus: 'Corrected', correctedCode })`; on 201 → `setCardStatus(suggestion.id, 'Corrected')` + status badge `<span class="status-badge">✎ Corrected: {correctedCode}</span>`; on 400 → `setCardInlineError(suggestion.id, errorFromApi)` (AC-004, AC-005; wireframe `correct-field` pattern)
6. Card state management and all-rejected empty state in `MedicalCodePage`: `type ReviewStatus = 'Pending' | 'Accepted' | 'Rejected' | 'Corrected'`; `const [cardStates, setCardStates] = useState<Record<string, ReviewStatus>>(() => Object.fromEntries(suggestions.map(s => [s.id, s.reviewStatus as ReviewStatus])))` — initialised from API response (re-fetch or page reload preserves prior review states); `const [cardErrors, setCardErrors] = useState<Record<string, string | null>>({})`; `const [cardLoading, setCardLoading] = useState<Record<string, boolean>>({})`; all-rejected detection: `const allReviewed = suggestions.length > 0 && suggestions.every(s => (cardStates[s.id] ?? 'Pending') !== 'Pending')`; `const allRejected = allReviewed && suggestions.every(s => (cardStates[s.id] ?? 'Pending') === 'Rejected')`; when `allReviewed && allRejected` render `<div role="status" aria-live="polite" class="reviewed-all-banner">All suggestions have been reviewed. No codes were added.</div>` below the list — not replacing the list (Edge: all rejected; WCAG SC 4.1.3 — status message; WCAG SC 1.3.1)

---

## Current Project State
```
src/
└── web/
    └── src/
        ├── pages/
        │   └── MedicalCodePage.tsx           (MODIFY — us_043; add handlers + all-rejected state)
        ├── components/
        │   └── codes/
        │       └── SuggestionCard.tsx        (MODIFY — us_043; activate buttons + inline edit + dialog)
        └── types/
            └── codes.ts                      (MODIFY — add ReviewStatus type; update CodeSuggestionDto)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | src/web/src/components/codes/SuggestionCard.tsx | Activate Accept/Reject/Correct buttons; add inline edit field; add reject confirm dialog; per-card loading + inline error |
| MODIFY | src/web/src/pages/MedicalCodePage.tsx | Add handleAccept/Reject/Correct; cardStates + cardErrors + cardLoading maps; all-rejected detection + banner |
| MODIFY | src/web/src/types/codes.ts | Add ReviewStatus union type; update CodeSuggestionDto with id + reviewStatus |

---

## External References
- https://www.w3.org/WAI/ARIA/apg/patterns/alertdialog/ (ARIA Alert Dialog pattern — `role="alertdialog"` variant for the Reject confirm; `aria-labelledby` + `aria-describedby`; focus management; Escape to cancel; UXR-404; WCAG SC 2.1.1)
- https://developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/Attributes/aria-expanded (aria-expanded on the Correct button — communicates expand/collapse state of the inline edit field to screen readers; WCAG SC 4.1.2)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [x] Click "Accept" on a Pending suggestion card; verify the card gains `suggestion-card accepted` class, the status badge shows "✓ Accepted" text (not colour alone), and the three action buttons are replaced by the accepted state (AC-002; UXR-105)
- [x] Click "Reject"; verify the UXR-404 confirm dialog opens with title "Reject this code?"; press Cancel — verify dialog closes, card unchanged; click Reject again, confirm — verify card gains `suggestion-card rejected` class, badge shows "✗ Rejected" text, card opacity 0.5 (AC-003; UXR-404)
- [x] Press Escape while the Reject confirm dialog is open; verify dialog closes and focus returns to the "Reject" button that opened it (WCAG SC 2.1.1; UXR-404)
- [x] Click "Correct"; verify the inline edit field appears pre-filled with the AI-suggested code; enter an invalid code "XYZ" and click Submit; verify the inline error message appears, the field value "XYZ" is preserved, and no API call is made (AC-005 — value not cleared)
- [x] Enter a valid corrected code "D50.0" and submit; verify `POST /patients/{id}/medical-codes` is called with `source:"AI-Corrected"`, card shows "✎ Corrected: D50.0" badge (AC-004)
- [x] Reject all suggestions one by one; after the last rejection verify the "All suggestions have been reviewed. No codes were added." message appears with `role="status"`; verify a screen reader would announce this politely (not assertive) (Edge: all rejected; WCAG SC 4.1.3)
- [x] Simulate a 409 response from the Accept endpoint; verify the inline `role="alert"` message "This suggestion has already been reviewed." appears inside the card and the card remains in Pending visual state until the user reloads (Edge: 409; WCAG SC 4.1.3)
- [x] Verify all three action buttons have icon character + text label in the same element; inspect with a screen reader or accessibility tool — confirm no button relies solely on the icon character for its accessible name (AC-001; UXR-105; WCAG SC 1.4.1)

---

## Implementation Checklist
- [x] `cardStates` in `MedicalCodePage` is initialised from `suggestions.map(s => [s.id, s.reviewStatus])` — this means if the page is re-rendered or suggestions are re-fetched, cards that were already Accepted/Rejected/Corrected remain in their reviewed state; the initial state must NOT default all cards to `'Pending'` regardless of the API-returned `reviewStatus` (OWASP A04 — state consistency; prevents double-acceptance if user re-fetches)
- [x] The `role="alert"` div for inline 409/400 errors is always present in the DOM (rendered empty string when no error); it must NOT be conditionally mounted/unmounted — removing and re-adding the element resets the aria-live region and screen reader announcements may be missed (WCAG SC 4.1.3)
- [x] The correction input `setCorrectionError(msg)` is called WITHOUT clearing `editValue` — the spec AC-005 explicitly requires the entered value to be preserved so the clinician can correct their typo; `setEditValue` is never called on a validation failure path (AC-005 — explicit requirement)
- [x] The Reject confirm dialog focus trap: on open, focus moves to the first button ("Cancel" or "Reject"); Tab/Shift-Tab cycle between the two buttons; Escape calls `closeDialog`; on close, focus returns to the "Reject" card button that triggered the dialog (WCAG SC 2.1.2 — no keyboard trap; UXR-404)
- [x] `handleAccept`, `handleReject`, and `handleCorrect` all check `cardStates[suggestion.id] !== 'Pending'` before making the API call — this is a client-side guard that prevents a second API call if the user double-clicks; the server-side 409 guard is the authoritative check, but the client guard reduces unnecessary round trips (performance; OWASP A04 — defence-in-depth)
- [x] The `ReviewStatus` type is declared as `type ReviewStatus = 'Pending' | 'Accepted' | 'Rejected' | 'Corrected'` in `src/web/src/types/codes.ts` and imported wherever used — never as an inline string literal; this is the single source of truth for the four valid states (DRY — no magic strings; TypeScript type safety)

---

## Evaluation Report

**Date:** 2025-05-22
**Build result:** `npx tsc --noEmit` → 0 errors, 0 warnings

### Files Changed

| File | Action | Notes |
|------|--------|-------|
| `frontend/src/types/codes.ts` | CREATED | `ReviewStatus` union type; single source of truth (DRY) |
| `frontend/src/api/medicalCodesApi.ts` | CREATED | `submitMedicalCode` + `rejectCodeSuggestion`; typed payloads; discriminated union |
| `frontend/src/api/codeSuggestionsApi.ts` | MODIFIED | `CodeSuggestionDto` extended with `id: string` + `reviewStatus: string` |
| `frontend/src/components/codes/SuggestionCard.module.css` | MODIFIED | `.cardAccepted`, `.cardRejected`, `.statusBadge*`, `.btn{Accept,Reject,Primary}`, `.correctField*`, `.inlineAlert`, `.reviewedFooter*` |
| `frontend/src/components/codes/SuggestionCard.tsx` | REWRITTEN | Accept/Reject/Correct props wired; inline edit with ICD-10/CPT validation; card-level inline error always in DOM; reviewed state footers |
| `frontend/src/pages/MedicalCodePage.tsx` | MODIFIED | `cardStates/cardErrors/cardLoading`; `handleAccept/handleRejectClick/handleCorrect`; UXR-404 dialog with focus trap + Escape; all-rejected + all-reviewed banners; initialised from API `reviewStatus` |

### AC Coverage

| AC | Status | Evidence |
|----|--------|----------|
| AC-001 Accept/Reject/Correct buttons with icon+text | ✓ PASS | Buttons rendered only when `reviewStatus === 'Pending'`; icon character + text in single element; `min-height: 44px` |
| AC-002 Accept → POST; 201 → Accepted state; 409 → inline error | ✓ PASS | `handleAccept` → `submitMedicalCode({source:'AI', reviewStatus:'Accepted'})`; `setCardStatus → 'Accepted'`; 409 → `setCardError` |
| AC-003 Reject → dialog → PATCH; 200 → Rejected; 409 → inline error | ✓ PASS | `handleRejectClick` → dialog; `confirmReject` → `rejectCodeSuggestion`; 200 → `setCardStatus → 'Rejected'`; 409 → `setCardError` |
| AC-004 Correct → inline edit pre-filled → POST source AI-Corrected | ✓ PASS | `handleCorrect` → `submitMedicalCode({source:'AI-Corrected', reviewStatus:'Corrected', correctedCode})`; Corrected state |
| AC-005 Inline validation — preserve entered value on failure | ✓ PASS | `handleSubmitCorrection` sets `correctionError` without calling `setEditValue`; value not cleared |
| Edge: 409 — inline role="alert" | ✓ PASS | `inlineError` div always in DOM (`role="alert"`); shown when `inlineError` is non-empty |
| Edge: all rejected banner | ✓ PASS | `allRejected` computed from `cardStates`; `<div role="status" aria-live="polite">All suggestions have been reviewed. No codes were added.</div>` |

### Accessibility Verification

| Criterion | Status | Evidence |
|-----------|--------|----------|
| WCAG SC 2.1.1 — keyboard access | ✓ | All buttons reachable via Tab; Correct field focusable; dialog buttons focusable |
| WCAG SC 2.1.2 — no keyboard trap | ✓ | Dialog: Escape calls `closeRejectDialog`; focus restored to trigger button via `rejectTriggerRef` |
| WCAG SC 4.1.3 — status messages | ✓ | `role="status" aria-live="polite"` for all-reviewed banners; `role="alert"` for errors |
| UXR-105 — icon + text label, not colour alone | ✓ | Each status badge: `✓ Accepted`, `✗ Rejected`, `✎ Corrected`, `Pending` — text + icon |
| UXR-404 — destructive confirm dialog | ✓ | Dialog with `role="dialog"`, `aria-modal`, `aria-labelledby`, `aria-describedby`; Cancel + Reject buttons; focus to Cancel on open |

### Security Verification (OWASP)

| Rule | Status | Evidence |
|------|--------|----------|
| A01 — Role guard | ✓ | `useEffect` redirects Patient/Staff before API calls; 403 → `/403` redirect |
| A02 — No PHI in logs | ✓ | `catch` blocks capture `err.status` and `err.message`; no suggestion content written to console |
| A04 — State consistency | ✓ | `cardStates[id] !== 'Pending'` guard on all three handlers prevents double-submission; server 409 is authoritative |
