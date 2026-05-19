# Task - TASK_002

## Requirement Reference
- **User Story:** us_010
- **Story Location:** .propel/context/tasks/EP-001/us_010/us_010.md
- **Acceptance Criteria:**
  - AC-001: After 14 minutes of user inactivity, the session timeout warning modal (MOD-001) appears with a 60-second countdown and options "Stay signed in" and "Sign out now"
  - AC-002: At 15 minutes of inactivity (countdown reaches zero or user ignores modal), the access token is removed from client state, the user is navigated to `/login`, and the login page displays "Your session has expired. Please sign in again."
  - AC-003: Clicking "Stay signed in" calls `POST /auth/refresh`, updates the access token in `AuthContext`, closes the modal, and resets the inactivity timer to zero
- **Edge Cases:**
  - User activity detection: the inactivity timer must reset on `mousemove`, `keydown`, `mousedown`, `touchstart`, and `scroll` events — not only on page navigation; a user actively filling a form must not be timed out
  - Multiple tabs: when "Stay signed in" is clicked in one tab, all other tabs of the same app must also reset their inactivity timers (implemented via `BroadcastChannel`)

---

## Design References
| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | MOD-001 (Session Timeout Warning) |
| **Wireframe Status** | AVAILABLE |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | .propel/context/wireframes/Hi-Fi/wireframe-MOD-001-session-timeout-warning.html |
| **Screen Spec** | MOD-001 |
| **UXR Requirements** | UXR-202 — session timeout warning displayed at exactly 14 minutes of inactivity with a 60-second countdown visible to the user |
| **Design Tokens** | Refer to project design system tokens for modal overlay color, countdown typography, and button hierarchy (primary = "Stay signed in", secondary = "Sign out now") |

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
| Frontend | React | 18.x | TR-001 (SPA framework; `useRef`, `useEffect`, `useState` for timer and modal state) |
| Frontend | TypeScript | 5.x | TR-001 (typed hook interface for `useInactivityTimer` config and `BroadcastChannel` message types) |
| Frontend | Vite | 5.x | TR-001 (build tooling) |
| Frontend | React Router | v6 | TR-001 (`useNavigate` for the post-timeout redirect to `/login` with state message) |

---

## Task Overview

Implement the `useInactivityTimer` custom hook that listens for DOM activity events and fires configurable `onWarning` and `onTimeout` callbacks. Render the `SessionTimeoutModal` component (MOD-001) when the warning fires, showing a 60-second countdown. Wire "Stay signed in" to `POST /auth/refresh` and "Sign out now" to auth-state clearance. Synchronise timer resets across browser tabs using the `BroadcastChannel` API so the session extension in one tab propagates to all others.

---

## Dependent Tasks
- task_002 (us_009) — `AuthContext` with `accessToken`, `role`, and `setAuth` must exist; `POST /auth/refresh` endpoint must be callable
- task_002 (us_009) — `LoginForm` must accept `location.state.message` for the session-expired banner (AC-002 display dependency)

---

## Impacted Components
- `src/web/src/hooks/useInactivityTimer.ts` — new custom hook
- `src/web/src/features/session/SessionTimeoutModal.tsx` — new modal component (MOD-001)
- `src/web/src/App.tsx` — mount `useInactivityTimer` and `SessionTimeoutModal` at the root level so the timer is active across all authenticated routes
- `src/web/src/features/auth/LoginForm.tsx` — modified to read and display `location.state?.message` as a session-expired info banner

---

## Implementation Plan
1. Create `src/web/src/hooks/useInactivityTimer.ts` with signature `useInactivityTimer({ warningAtMs, timeoutAtMs, onWarning, onTimeout }: InactivityTimerOptions): { reset: () => void }` — internally uses two `useRef<ReturnType<typeof setTimeout>>` refs (warning timer and timeout timer) and a `resetTimer()` function that clears and re-starts both; `warningAtMs = 14 * 60 * 1000`, `timeoutAtMs = 15 * 60 * 1000`
2. Attach activity event listeners in the hook's `useEffect`: `['mousemove', 'keydown', 'mousedown', 'touchstart', 'scroll']` on `window` with `{ passive: true }`; each event calls `resetTimer()`; return the cleanup function that removes all listeners and clears both timers (Edge: activity detection — not just page navigation)
3. Create `src/web/src/features/session/SessionTimeoutModal.tsx` rendering when `isOpen` prop is true: overlay with modal body, message text, 60-second countdown (driven by a `setInterval` decrementing a local `secondsLeft` state), primary button "Stay signed in", secondary button "Sign out now" (AC-001; UXR-202 — 60-second countdown visible)
4. "Stay signed in" button handler in `SessionTimeoutModal`: call `POST /api/auth/refresh` with the stored refresh token from `sessionStorage`; on success, update `AuthContext.setAuth(newToken, role)`, call `onExtend()` to close modal, call `reset()` from `useInactivityTimer` to restart the 14-minute warning timer; also post `{ type: 'SESSION_EXTENDED' }` to `BroadcastChannel('upacip-session')` (AC-003; Edge: multiple tabs)
5. `onTimeout` callback wired in `App.tsx`: clear `AuthContext` (set `accessToken = null, role = null`), clear `sessionStorage`, call `navigate('/login', { state: { message: "Your session has expired. Please sign in again." } })` (AC-002)
6. In `App.tsx`, mount `useInactivityTimer` and `<SessionTimeoutModal />` inside the authenticated layout wrapper so the timer only runs when a user is authenticated; pass `onWarning={() => setShowModal(true)}` and `onTimeout` callback (AC-001, AC-002)
7. In `useInactivityTimer`, subscribe to `BroadcastChannel('upacip-session')`; on receiving `{ type: 'SESSION_EXTENDED' }`, call `resetTimer()` to reset the inactivity clock in this tab; close the channel in the cleanup function (Edge: multiple tabs — session extension propagates across tabs)

---

## Current Project State
```
src/
└── web/
    └── src/
        ├── App.tsx                                      (MODIFY — mount timer hook + modal in auth layout)
        ├── hooks/
        │   └── useInactivityTimer.ts                    (CREATE)
        ├── features/
        │   ├── session/
        │   │   └── SessionTimeoutModal.tsx              (CREATE)
        │   └── auth/
        │       └── LoginForm.tsx                        (MODIFY — display location.state.message)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | src/web/src/hooks/useInactivityTimer.ts | Custom hook: warning timer (14 min) + timeout timer (15 min); DOM activity events reset both; exposes `reset()` function; BroadcastChannel subscriber |
| CREATE | src/web/src/features/session/SessionTimeoutModal.tsx | Modal component (MOD-001): 60-second countdown, "Stay signed in" (refresh + reset), "Sign out now" (clear auth + navigate) |
| MODIFY | src/web/src/App.tsx | Mount `useInactivityTimer` and `<SessionTimeoutModal />` inside authenticated layout wrapper |
| MODIFY | src/web/src/features/auth/LoginForm.tsx | Read `location.state?.message` and render as an info banner above the form if present |

---

## External References
- https://developer.mozilla.org/en-US/docs/Web/API/BroadcastChannel (BroadcastChannel API — cross-tab message passing without a server)
- https://reactrouter.com/en/main/hooks/use-navigate (React Router v6 `useNavigate` with `state` for session-expired message)
- https://react.dev/reference/react/useRef (React 18 `useRef` for mutable timer ID references across renders)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] Set `warningAtMs` to a short value (e.g., 5s) in dev config; verify modal appears with a 60-second countdown after 5 seconds of no activity (AC-001; UXR-202)
- [ ] Let the countdown reach zero; verify `accessToken` is removed from `AuthContext` and the browser navigates to `/login` with the session-expired message visible (AC-002)
- [ ] Click "Stay signed in" while the modal is showing; verify modal closes, `AuthContext.accessToken` is updated with the new token, and no second modal appears for another 14 minutes (AC-003)
- [ ] Move the mouse continuously while the timer is running; verify the 14-minute warning does not fire (Edge: activity detection)
- [ ] Open the app in two tabs; trigger the warning modal in tab A; click "Stay signed in" in tab A; verify tab B's warning modal closes (or does not appear) and tab B's timer resets (Edge: multiple tabs)

---

## Implementation Checklist
- [ ] `useInactivityTimer` attaches `passive: true` event listeners to avoid blocking the scroll thread — critical for usability on scroll-heavy pages (Edge: activity detection; browser performance)
- [ ] Both timer refs (`warningTimerRef` and `timeoutTimerRef`) are cleared in the cleanup function returned from `useEffect` — prevents stale timers firing after component unmount or re-render (AC-001, AC-002 — timer hygiene)
- [ ] `SessionTimeoutModal` countdown is driven by `setInterval(1000)` inside a `useEffect` that stops the interval when `secondsLeft` reaches 0 and calls `onTimeout()` — the 60-second countdown is the last line of defence before forced sign-out (AC-001; UXR-202)
- [ ] "Stay signed in" broadcasts `{ type: 'SESSION_EXTENDED' }` via `BroadcastChannel('upacip-session')` before closing the modal — ensures other tabs receive the signal before the originating tab's timer resets (Edge: multiple tabs — message must fire before local state changes)
- [ ] `BroadcastChannel` is instantiated once per hook instance and closed in the `useEffect` cleanup — prevents memory leaks from orphaned channel listeners (Edge: multiple tabs; resource management)
- [ ] `LoginForm.tsx` renders `location.state?.message` as a visible info/warning element (not a toast that auto-dismisses) so a returning user sees the session-expired message when they arrive at the login page (AC-002)
- [ ] `useInactivityTimer` is only mounted inside the authenticated layout wrapper in `App.tsx` — unauthenticated users visiting `/login` or `/register` are not affected by the inactivity timeout (AC-001 — timer scope)
