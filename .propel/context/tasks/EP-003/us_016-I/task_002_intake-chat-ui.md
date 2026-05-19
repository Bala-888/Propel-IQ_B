# Task - TASK_002

## Requirement Reference
- **User Story:** us_016-I
- **Story Location:** .propel/context/tasks/EP-003/us_016-I/us_016-I.md
- **Acceptance Criteria:**
  - AC-001: The patient navigates to SCR-004 and taps "Start AI Intake"; the API call completes and the first AI question appears in the chat interface within 3 seconds
  - AC-002: The patient sends successive messages and the chat advances turn-by-turn; when all 5 fields are collected, the UI shows an intake summary panel with a "Review & Submit" action
- **Edge Cases:**
  - Ollama 503 (model not loaded): a banner "AI intake is temporarily unavailable. You can use the manual form instead." is displayed with a link to the manual intake form; the patient is not left on a blank screen
  - Inference timeout (30s): an inline message "The AI took too long to respond. Please try your message again." is shown alongside the last AI message; the input field is re-enabled so the patient can retry; no session data is lost

---

## Design References
| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | SCR-004 (AI Conversational Intake) |
| **Wireframe Status** | AVAILABLE |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | .propel/context/wireframes/Hi-Fi/wireframe-SCR-004-ai-conversational-intake.html |
| **Screen Spec** | SCR-004 |
| **UXR Requirements** | UXR-101 — AI confidence indicator displayed alongside each AI message bubble; UXR-105 — no colour-only error/status states; icon + text required for all states |
| **Design Tokens** | Refer to project design system tokens for chat bubble variants (AI vs patient), confidence badge, banner variants (warning/error), loading skeleton |

---

## AI References
| Reference Type | Value |
|----------------|-------|
| **AI Impact** | Yes |
| **AIR Requirements** | AIR-001 (UI renders multi-turn AI dialogue), AIR-002 (AI-extracted field summary displayed when allFieldsCollected=true) |
| **AI Pattern** | Conversational dialogue — the frontend renders model responses as chat messages; field extraction is fully server-side |
| **Prompt Template Path** | N/A (prompt lives on the backend; frontend is display-only) |
| **Guardrails Config** | Patient messages displayed in the chat UI must not be stored in browser localStorage or sessionStorage — only in React component state scoped to the session |
| **Model Provider** | Ollama + Llama 3.1 8B (response rendered via API; frontend has no direct Ollama connection) |

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
| Frontend | React | 18.x | TR-001 (SCR-004 chat component; `useRef` for auto-scroll; `useState` for session and message list) |
| Frontend | TypeScript | 5.x | TR-001 (typed `ChatMessage`, `IntakeSessionResponse`, `MessageResponse` interfaces) |
| Frontend | Vite | 5.x | TR-001 (build tooling) |
| Frontend | React Router | v6 | TR-001 (`useNavigate` for switch-to-manual fallback route) |

---

## Task Overview

Build the SCR-004 AI conversational intake chat UI. The screen renders a scrollable message list with AI and patient bubbles. "Start AI Intake" triggers `POST /intake/ai/start` and renders the first AI message. A text input and Send button drive successive `POST /intake/ai/message` calls. Each AI bubble shows a UXR-101 confidence indicator. When `allFieldsCollected = true`, the summary panel appears. A 503 response triggers the manual-form fallback banner. A timeout response re-enables the input with a retry message (UXR-105 icon + text).

---

## Dependent Tasks
- task_001 (us_016-I) — `POST /intake/ai/start` and `POST /intake/ai/message` endpoints must be available
- task_002 (us_009) — `AuthContext` with `accessToken` must be available for the `Authorization` header

---

## Impacted Components
- `src/web/src/features/intake/AiIntakePage.tsx` — new page component (SCR-004); mounts at `/intake/ai`
- `src/web/src/features/intake/ChatMessageList.tsx` — new component: scrollable message bubble list
- `src/web/src/features/intake/ChatMessageBubble.tsx` — new component: single AI or patient message with UXR-101 confidence badge (AI messages only)
- `src/web/src/features/intake/IntakeSummaryPanel.tsx` — new component: displayed when allFieldsCollected=true
- `src/web/src/api/intakeAiApi.ts` — new API client: `startSession`, `sendMessage`

---

## Implementation Plan
1. Create `intakeAiApi.ts` with two typed fetch wrappers: `startSession(): Promise<{sessionId: string; message: string}>` (POST /api/intake/ai/start) and `sendMessage(sessionId: string, text: string): Promise<{message: string; allFieldsCollected: boolean; summary?: IntakeSummary}>` (POST /api/intake/ai/message); both include `Authorization: Bearer <accessToken>` from `AuthContext`; throw `AiUnavailableError` on 503 and `AiTimeoutError` on 504/timeout (AC-001, AC-002)
2. Build `ChatMessageBubble.tsx`: renders a single message as a `<div>` with alignment based on `role: 'ai' | 'patient'`; for AI messages, renders a `<ConfidenceBadge>` sub-component showing a confidence level label + icon (e.g., "High confidence" with a checkmark icon) — icon and text always present together (UXR-101; UXR-105)
3. Build `ChatMessageList.tsx`: maps over `messages: ChatMessage[]` and renders `<ChatMessageBubble>`; uses `useRef<HTMLDivElement>` on a sentinel `<div>` at the bottom and calls `ref.current.scrollIntoView({ behavior: 'smooth' })` after every new message push (AC-001, AC-002 — scroll UX)
4. Build `AiIntakePage.tsx` (SCR-004): `sessionId` state (string | null); `messages: ChatMessage[]` state; `isLoading: boolean` state; on "Start AI Intake" button click, call `startSession()`, set `sessionId`, push first AI message to list; display loading skeleton while awaiting API responses (AC-001)
5. Wire text input + Send button in `AiIntakePage.tsx`: on submit, push patient message immediately to `messages` (optimistic), set `isLoading=true`, call `sendMessage(sessionId, text)`, push AI reply to `messages`, set `isLoading=false`; disable input while `isLoading=true` (AC-002)
6. When `sendMessage` response has `allFieldsCollected=true`: render `<IntakeSummaryPanel summary={response.summary} />` below the message list with a "Review & Submit" button — session is considered complete from the UI perspective (AC-002; AIR-002)
7. Error handling in `AiIntakePage.tsx`: catch `AiUnavailableError` (503) → display `<div role="alert">` banner with icon + text "AI intake is temporarily unavailable. You can use the manual form instead." + a `<Link to="/intake/manual">` button (Edge: 503; UXR-105); catch `AiTimeoutError` → display inline message below the last message bubble: icon + text "The AI took too long to respond. Please try your message again." and re-enable input (Edge: timeout; UXR-105)

---

## Current Project State
```
src/
└── web/
    └── src/
        ├── features/
        │   └── intake/
        │       ├── AiIntakePage.tsx                 (CREATE — SCR-004)
        │       ├── ChatMessageList.tsx              (CREATE)
        │       ├── ChatMessageBubble.tsx            (CREATE — UXR-101 confidence badge)
        │       └── IntakeSummaryPanel.tsx           (CREATE)
        └── api/
            └── intakeAiApi.ts                       (CREATE)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | src/web/src/features/intake/AiIntakePage.tsx | SCR-004: session state, message list, start/send handlers, error banners |
| CREATE | src/web/src/features/intake/ChatMessageList.tsx | Scrollable bubble list with auto-scroll to bottom |
| CREATE | src/web/src/features/intake/ChatMessageBubble.tsx | Single message bubble with UXR-101 confidence badge for AI messages |
| CREATE | src/web/src/features/intake/IntakeSummaryPanel.tsx | Summary display when allFieldsCollected=true |
| CREATE | src/web/src/api/intakeAiApi.ts | Typed API wrappers: startSession + sendMessage with error typing |

---

## External References
- https://react.dev/reference/react/useRef (React 18 useRef — scroll sentinel for chat auto-scroll)
- https://www.w3.org/WAI/ARIA/apg/patterns/chatlog/ (WAI-ARIA chat log pattern — live region for screen reader announcement of new AI messages)
- https://reactrouter.com/en/main/components/link (React Router v6 Link — switch-to-manual fallback navigation)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] Navigate to SCR-004 as an authenticated Patient; click "Start AI Intake"; verify the first AI message bubble appears within 3 seconds (AC-001)
- [ ] Submit 5 successive responses covering all field groups; verify each patient message appears immediately (optimistic) and each AI reply renders with a confidence badge (AC-002; UXR-101)
- [ ] Verify the intake summary panel appears automatically when the response includes `allFieldsCollected: true` (AC-002)
- [ ] Simulate a 503 response; verify the error banner with icon + text appears and a "Manual form" link is rendered (Edge: 503; UXR-105 — icon+text present)
- [ ] Simulate a timeout response; verify the inline retry message with icon + text appears below the last bubble and the input field is re-enabled (Edge: timeout; UXR-105)
- [ ] Inspect React DevTools or component state; verify no patient messages are stored in `localStorage` or `sessionStorage` (AIR guardrails — PHI browser storage)

---

## Implementation Checklist
- [ ] `ChatMessageList` uses a bottom-sentinel `<div ref={bottomRef}>` with `scrollIntoView({ behavior: 'smooth' })` called inside a `useEffect([messages])` — ensures auto-scroll fires after every React render cycle that adds a new message (AC-001, AC-002 — scroll UX)
- [ ] `ChatMessageBubble` renders a `<ConfidenceBadge>` for `role='ai'` messages that contains both a named icon component and a text label — colour is supplementary, never the sole indicator of confidence level (UXR-101; UXR-105; WCAG 1.4.1)
- [ ] Patient messages are pushed to `messages` state **before** the API call returns (optimistic update) — the UI feels responsive even when Ollama is slow (AC-002; perceived latency)
- [ ] Error banners and inline error messages use `<div role="alert">` or `<span role="alert">` — screen readers announce them automatically without requiring focus to move (Edge: 503, timeout; UXR-105; WCAG 4.1.3)
- [ ] Patient chat messages are stored exclusively in React component state — they are never written to `localStorage`, `sessionStorage`, `IndexedDB`, or any browser-persistent storage (AIR guardrails; OWASP A02; HIPAA minimum-necessary)
- [ ] The `isLoading` guard disables the text input and Send button while an API call is in flight — prevents double-submit which could corrupt session turn order (AC-002; AC-001 — session state integrity)
- [ ] `intakeAiApi.ts` attaches `Authorization: Bearer <accessToken>` from `AuthContext` on every request; a 401 triggers the existing session-expiry flow so a patient mid-session is redirected to login rather than seeing a confusing error (AC-001; OWASP A01)
