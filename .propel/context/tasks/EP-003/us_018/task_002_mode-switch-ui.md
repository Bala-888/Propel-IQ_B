# Task - TASK_002

## Requirement Reference
- **User Story:** us_018
- **Story Location:** .propel/context/tasks/EP-003/us_018/us_018.md
- **Acceptance Criteria:**
  - AC-001: After `POST /intake/mode-switch` AI→Manual returns 200, the manual form (SCR-005) opens with Demographics and Medical History sections pre-populated from the mapped fields
  - AC-002: After `POST /intake/mode-switch` Manual→AI returns 200, the AI chat (SCR-004) resumes with prior data acknowledged and already-completed fields skipped in subsequent dialogue turns
  - AC-003: If `reviewItems[]` is non-empty in the mode-switch response, a "Review items" section is rendered in the target screen with a notification "Some items could not be auto-mapped. Please review them."
  - AC-004: A "Switch to [other mode]" button is visible in the toolbar on both SCR-004 and SCR-005; it meets the 44×44px minimum touch target and is focusable via keyboard Tab
- **Edge Cases:**
  - No data entered: if the mode-switch response contains empty `mappedFields` and empty `reviewItems[]`, the target form opens in its default empty state — no pre-population attempt, no error
  - Concurrent auto-save during mode switch: any pending auto-save debounce timer must be cancelled before the mode-switch API call is dispatched; subsequent auto-saves from the target screen include the `cacheVersion` returned by the mode-switch response

---

## Design References
| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | SCR-004 (AI Intake — switch control in toolbar), SCR-005 (Manual Intake — switch control in toolbar) |
| **Wireframe Status** | AVAILABLE |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | .propel/context/wireframes/Hi-Fi/wireframe-SCR-004-ai-conversational-intake.html |
| **Screen Spec** | SCR-004, SCR-005 |
| **UXR Requirements** | UXR-105 — switch button and review notification use icon + text, never colour alone; button enabled/disabled state must not rely on colour change only |
| **Design Tokens** | Refer to project design system tokens for toolbar layout, switch button sizing (min 44×44px), review buffer card, and notification banner |

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
| Frontend | React | 18.x | TR-001 (switch button state; review buffer section; pre-population via reset()) |
| Frontend | TypeScript | 5.x | TR-001 (typed ModeSwitchRequest, ModeSwitchResponse interfaces) |
| Frontend | Vite | 5.x | TR-001 (build tooling) |
| Frontend | React Router | v6 | TR-001 (`useNavigate` with router state for passing mappedFields/reviewItems to target screen; `useLocation` to read state in target screen) |

---

## Task Overview

Add the "Switch to [other mode]" button to both SCR-004 (`AiIntakePage.tsx`) and SCR-005 (`ManualIntakeForm.tsx`), wire it to the `POST /intake/mode-switch` API, and handle the response in each target screen. On a successful switch, any pending auto-save debounce is cancelled, the mapped fields are passed via React Router state to the destination screen, and the review buffer is rendered if `reviewItems[]` is non-empty. The target screen reads `location.state` on mount and pre-populates form fields (for SCR-005) or injects prior-data context (for SCR-004).

---

## Dependent Tasks
- task_001 (us_018) — `POST /intake/mode-switch` endpoint must return `{mappedFields, reviewItems, cacheVersion}`
- task_002 (us_017) — `ManualIntakeForm.tsx` must exist with `reset()` form hydration support (AI→Manual pre-population path)
- task_002 (us_016-I) — `AiIntakePage.tsx` must exist and accept prior-data context for the Manual→AI path

---

## Impacted Components
- `src/web/src/features/intake/AiIntakePage.tsx` — modified: add switch button to toolbar; read `location.state.mappedFields` on mount; render review buffer if `location.state.reviewItems` non-empty
- `src/web/src/features/intake/ManualIntakeForm.tsx` — modified: add switch button to toolbar; cancel auto-save debounce on switch; pass `cacheVersion` in subsequent `postDraft` calls
- `src/web/src/api/intakeModeSwitchApi.ts` — new: `switchMode(req: ModeSwitchRequest): Promise<ModeSwitchResponse>` typed wrapper
- `src/web/src/features/intake/IntakeSwitchButton.tsx` — new: shared "Switch to [other mode]" button component (44×44px, icon + label, keyboard accessible)
- `src/web/src/features/intake/IntakeReviewBuffer.tsx` — new: review items notification card rendered on target screen when reviewItems[] is non-empty

---

## Implementation Plan
1. Create `intakeModeSwitchApi.ts` with a single typed wrapper `switchMode(req: ModeSwitchRequest): Promise<ModeSwitchResponse>`; `ModeSwitchRequest` is `{from: "AI"|"Manual", to: "AI"|"Manual", sessionId?: string}`; `ModeSwitchResponse` is `{mappedFields: ManualIntakeData | AiSessionFields | null, reviewItems: string[], cacheVersion: string}`; attach `Authorization: Bearer <accessToken>` (AC-001, AC-002, AC-003)
2. Build `IntakeSwitchButton.tsx`: renders a `<button>` element (not a `<div>`) with `aria-label="Switch to Manual Form"` or `"Switch to AI Chat"` based on `targetMode` prop; minimum inline style or CSS class ensuring 44×44px touch target (`min-height: 44px; min-width: 44px`); renders a named icon component + visible text label; `onClick` triggers the switch handler passed as prop; `disabled` prop disables the button during in-flight API call — disabled state uses a visual indicator beyond colour (e.g., spinner icon replaces the switch icon) (AC-004; UXR-105; WCAG 2.5.5; WCAG 4.1.2)
3. Implement switch handler (used by both screens): cancel the pending auto-save debounce ref via `clearTimeout(autoSaveDebouncRef.current)`; call `switchMode({from, to, sessionId})`; on success, `navigate(targetPath, { state: { mappedFields: res.mappedFields, reviewItems: res.reviewItems, cacheVersion: res.cacheVersion } })`; on API error, render `<span role="alert">` with icon + text error message — do not navigate (AC-001, AC-002; Edge: concurrent auto-save; UXR-105)
4. Modify `AiIntakePage.tsx`: mount the `<IntakeSwitchButton targetMode="Manual">` in the page toolbar; in `useEffect` on mount, read `location.state?.mappedFields` — if present, pass to the chat context so the Ollama dialogue initialises with pre-filled field knowledge (the dialogue engine reads `filledFields` from the session state set by the backend in task_001); read `location.state?.reviewItems` and pass to `<IntakeReviewBuffer>` (AC-002, AC-003)
5. Modify `ManualIntakeForm.tsx`: mount the `<IntakeSwitchButton targetMode="AI">` in the page toolbar; expose `autoSaveDebounceRef` so the switch handler can cancel it before navigating; in `useEffect` on mount, read `location.state?.mappedFields` — if non-null and non-empty, call `reset(mappedFieldsToFormValues(mappedFields))` to pre-populate all mapped sections; read `location.state?.cacheVersion` and store in a `useRef` to include in subsequent `postDraft` calls (AC-001; Edge: no data entered; Edge: concurrent auto-save)
6. Build `IntakeReviewBuffer.tsx`: accepts `reviewItems: string[]` prop; if empty, renders nothing; if non-empty, renders a `<section aria-label="Review items">` containing a `<div role="alert">` notification "Some items could not be auto-mapped. Please review them." (with icon + text) and an unordered list of the review items below it; rendered above the tab bar in SCR-005 and above the chat window in SCR-004 (AC-003; UXR-105; WCAG 4.1.3)
7. Empty-state handling: if `location.state?.mappedFields` is null or empty on mount in either target screen, skip all `reset()` / pre-population calls and render the screen in its default empty state — no error displayed, no fallback toast (Edge: no data entered)
8. `cacheVersion` propagation: `ManualIntakeForm` stores `location.state?.cacheVersion` in a `useRef`; the `postDraft` call in `intakeManualApi.ts` accepts an optional `cacheVersion` parameter and includes it as `X-Cache-Version` request header, allowing the backend to discard stale auto-saves (Edge: concurrent auto-save)

---

## Current Project State
```
src/
└── web/
    └── src/
        ├── features/
        │   └── intake/
        │       ├── AiIntakePage.tsx             (existing — MODIFY: add switch button, read location.state)
        │       ├── ManualIntakeForm.tsx          (from us_017 — MODIFY: add switch button, cacheVersion ref)
        │       └── (IntakeSwitchButton.tsx       — CREATE)
        │       └── (IntakeReviewBuffer.tsx       — CREATE)
        └── api/
            ├── intakeAiApi.ts                   (existing — do not modify)
            ├── intakeManualApi.ts               (from us_017 — MODIFY: add optional cacheVersion param)
            └── (intakeModeSwitchApi.ts          — CREATE)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | src/web/src/api/intakeModeSwitchApi.ts | switchMode typed API wrapper |
| CREATE | src/web/src/features/intake/IntakeSwitchButton.tsx | Shared switch button (44×44px, icon + text, aria-label) |
| CREATE | src/web/src/features/intake/IntakeReviewBuffer.tsx | Review items notification card |
| MODIFY | src/web/src/features/intake/AiIntakePage.tsx | Add switch button; read mappedFields and reviewItems from location.state |
| MODIFY | src/web/src/features/intake/ManualIntakeForm.tsx | Add switch button; cancel debounce on switch; cacheVersion ref; postDraft header |
| MODIFY | src/web/src/api/intakeManualApi.ts | Add optional cacheVersion param to postDraft |

---

## External References
- https://reactrouter.com/en/main/hooks/use-location (React Router v6 useLocation — reading router state for mappedFields and reviewItems on target screen mount)
- https://reactrouter.com/en/main/hooks/use-navigate (React Router v6 useNavigate with state — passing mappedFields/reviewItems/cacheVersion to destination screen)
- https://www.w3.org/WAI/WCAG21/Understanding/target-size.html (WCAG 2.5.5 — minimum 44×44px touch target for switch button)
- https://www.w3.org/WAI/ARIA/apg/patterns/button/ (WAI-ARIA button pattern — keyboard accessibility for switch button)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] Load SCR-004 (AI intake); verify the "Switch to Manual Form" button is visible in the toolbar, has a 44×44px touch area, and can be reached via keyboard Tab (AC-004; WCAG 2.5.5)
- [ ] Load SCR-005 (Manual intake); verify the "Switch to AI Chat" button is visible in the toolbar, has a 44×44px touch area, and can be reached via keyboard Tab (AC-004; WCAG 2.5.5)
- [ ] On SCR-004 with AI data collected, click "Switch to Manual Form"; verify SCR-005 loads with Demographics and Medical History pre-populated from AI data (AC-001)
- [ ] On SCR-005 with manual data filled, click "Switch to AI Chat"; verify SCR-004 opens and the chat does not re-ask for already-filled fields (AC-002)
- [ ] Trigger a mode switch that returns non-empty `reviewItems`; verify the "Review items" notification section appears on the target screen with icon + text message and the unmapped items listed (AC-003)
- [ ] Trigger a mode switch from an empty form (no data entered); verify the target screen opens in empty state with no error and no pre-population attempt (Edge: no data entered)
- [ ] Start an auto-save debounce, then immediately click the switch button; verify `POST /api/intake/draft` is NOT called (debounce cancelled); verify only `POST /api/intake/mode-switch` fires (Edge: concurrent auto-save)
- [ ] Verify the switch button's in-flight loading state (disabled + spinner icon) is not communicated by colour change alone — the icon changes from the switch icon to a spinner and the button's `disabled` attribute is set (AC-004; UXR-105; WCAG 1.4.1)

---

## Implementation Checklist
- [ ] `IntakeSwitchButton` renders as a `<button>` element with explicit `aria-label` describing the target mode (`"Switch to Manual Form"` or `"Switch to AI Chat"`); minimum CSS `min-height: 44px; min-width: 44px` is applied; button contains a named icon component + visible text label — not colour alone (AC-004; UXR-105; WCAG 2.5.5; WCAG 4.1.2)
- [ ] Switch handler calls `clearTimeout(autoSaveDebounceRef.current)` before calling `switchMode()`; the debounce ref is stored in `ManualIntakeForm` and passed to the switch handler — no `POST /api/intake/draft` fires during or after the mode switch until the target screen sets up its own debounce (Edge: concurrent auto-save)
- [ ] `ManualIntakeForm` reads `location.state?.mappedFields` on mount; if non-null and non-empty, calls `reset(mappedFieldsToFormValues(mappedFields))`; if null/empty, skips `reset()` and renders the empty form — no error or fallback logic triggered (AC-001; Edge: no data entered)
- [ ] `AiIntakePage` reads `location.state?.mappedFields` on mount and passes it to the chat context initialisation so `filledFields` from the backend session state are reflected; does not re-ask for pre-filled fields in the opening dialogue turn (AC-002)
- [ ] `IntakeReviewBuffer` renders nothing when `reviewItems` is empty (`length === 0`); when non-empty, renders a `<div role="alert">` notification with icon + text + list of review items above the tab bar / chat window (AC-003; UXR-105; WCAG 4.1.3)
- [ ] `ManualIntakeForm` stores `location.state?.cacheVersion` in a `useRef`; subsequent `postDraft` calls pass this value as an `X-Cache-Version` header via `intakeManualApi.ts`; the ref is updated on each successful `postDraft` response if a new `cacheVersion` is returned (Edge: concurrent auto-save)
- [ ] Switch button `disabled` state during in-flight API call renders a spinner icon (replacing the switch icon) alongside the text label — no state is communicated by colour change alone; after the API call resolves (success or error), the button returns to its enabled state (AC-004; UXR-105; WCAG 1.4.1)
- [ ] `navigate(targetPath, { state: { mappedFields, reviewItems, cacheVersion } })` is called only on a 200 response from `switchMode()`; on any non-200 error, a `<span role="alert">` with icon + text error message is rendered in-place and navigation does not occur (AC-001, AC-002; UXR-105; WCAG 4.1.3)
