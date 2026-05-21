# Task - TASK_002

## Requirement Reference
- **User Story:** us_033
- **Story Location:** .propel/context/tasks/EP-006/us_033/us_033.md
- **Acceptance Criteria:**
  - AC-001: New queue entry received via `QueueEntryAdded` event appears in SCR-011 within 2 seconds with a highlight animation; summary count increments — no page refresh
  - AC-002: `QueueEntryUpdated` event received from SignalR updates the matching row's status in-place across all open SCR-011 instances — no stale "Waiting" status after arrived
  - AC-003: New row uses `--color-highlight` CSS variable background, then transitions to default colour over 1.5 seconds (UXR-502)
  - AC-004: On SignalR reconnection, client calls `GET /api/queue?since=<lastEventTimestamp>` to recover missed events; reconciles without creating duplicate rows

---

## Design References
| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | SCR-011 (Staff Queue Dashboard — SignalR live updates) |
| **Wireframe Status** | AVAILABLE |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | .propel/context/wireframes/Hi-Fi/wireframe-SCR-011-staff-queue-dashboard.html |
| **Screen Spec** | SCR-011 — new row highlight animation on push event; live status cell update without reload |
| **UXR Requirements** | UXR-502 (new row highlight using --color-highlight CSS variable, 1.5s fade transition) |
| **Design Tokens** | --color-highlight (new row flash background), transition: background-color 1.5s ease-out |

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
| Frontend | React + TypeScript | 18.x / 5.x | TR-001 — `QueueDashboardPage` extended with SignalR connection lifecycle, event handlers, and highlight animation state (AC-001–004) |
| SignalR Client | @microsoft/signalr | latest stable | TR-013 (client) — `HubConnectionBuilder` with `withAutomaticReconnect` and `accessTokenFactory`; `onreconnected` callback for fallback fetch (AC-001, AC-002, AC-004) |
| HTTP Client | Fetch API (browser built-in) | Web platform | `GET /api/queue?since=` fallback on reconnection (AC-004) |

---

## Task Overview

Extend `QueueDashboardPage` (SCR-011) with a SignalR `HubConnection` that connects to `/hubs/queue` using the in-memory access token. Event handlers for `QueueEntryAdded` and `QueueEntryUpdated` update React state immutably. New rows receive a `queue-row--new` CSS class that triggers a `--color-highlight` fade animation per UXR-502, cleared after 1.6 seconds. A `lastEventTimestamp` ref is updated on every received event. On `onreconnected`, the client fetches `GET /api/queue?since=<lastEventTimestamp>` and reconciles the state without duplicates. The connection is stopped on component unmount.

---

## Dependent Tasks
- task_002 (us_031) — `QueueDashboardPage`, `queue: QueueEntry[]` state, and `queueApi.ts` must exist before SignalR event handlers can be layered in
- task_001 (us_033) — SignalR hub and `GET /api/queue?since=` endpoint must be available

---

## Impacted Components
- `src/web/src/pages/QueueDashboardPage.tsx` — modified: add `HubConnection` lifecycle; `QueueEntryAdded` / `QueueEntryUpdated` handlers; `newRowId` state; `lastEventTimestamp` ref; `onreconnected` fallback
- `src/web/src/api/queueApi.ts` — modified: extend `getQueue` to accept optional `{ since?: string }` parameter and append `?since=` to the URL when provided
- `src/web/src/styles/queue.css` (or inline CSS module) — modified/new: `.queue-row--new` class with `--color-highlight` background and `transition: background-color 1.5s ease-out`

---

## Implementation Plan
1. Install `@microsoft/signalr` as a project dependency (`npm install @microsoft/signalr`); build `HubConnection` in `QueueDashboardPage` `useEffect`: `new HubConnectionBuilder().withUrl("/hubs/queue", { accessTokenFactory: () => getAccessToken() }).withAutomaticReconnect().build()`; call `connection.start()`; return `() => connection.stop()` as cleanup on unmount (AC-001; OWASP A01 — access token from in-memory auth context, never from localStorage)
2. `QueueEntryAdded` handler: `connection.on("QueueEntryAdded", (entry: QueueEntry) => { setQueue(q => [entry, ...q]); setNewRowId(entry.id); lastEventTimestamp.current = new Date().toISOString(); })` — prepends the new entry to the top of the queue and flags it for the highlight animation; the summary bar and sort `useMemo` recalculate automatically (AC-001; AC-003)
3. CSS highlight animation: `<tr className={entry.id === newRowId ? "queue-row--new" : ""}>` on each row; in CSS: `.queue-row--new { background-color: var(--color-highlight); transition: background-color 1.5s ease-out; }` — `setNewRowId(null)` is called via `setTimeout(1600)` after the transition ends so the class is removed and the row settles at default background (AC-003; UXR-502)
4. `QueueEntryUpdated` handler: `connection.on("QueueEntryUpdated", (entry: QueueEntry) => { setQueue(q => q.map(r => r.id === entry.id ? { ...r, ...entry } : r)); lastEventTimestamp.current = new Date().toISOString(); })` — merges the incoming entry with the existing row object; summary bar and sort recalculate via `useMemo` automatically (AC-002)
5. `lastEventTimestamp` tracking: `const lastEventTimestamp = useRef<string>(new Date().toISOString())` initialised to mount time; updated inside both `QueueEntryAdded` and `QueueEntryUpdated` handlers to the moment the event arrives; this value is the `since` parameter sent to the fallback fetch on reconnection (AC-004)
6. Reconnection fallback: `connection.onreconnected(async () => { const missed = await getQueue({ since: lastEventTimestamp.current }); setQueue(q => reconcile(q, missed)); })` where `reconcile(existing, incoming)` returns a new array: for each incoming entry, update the existing entry if `id` matches, otherwise append — no duplicates possible because the matching branch replaces in-place (AC-004)
7. Extend `queueApi.ts` `getQueue`: add optional `opts?: { since?: string }` parameter; if `opts?.since` is set, append `&since=${encodeURIComponent(opts.since)}` to the query string — the `since` value is the ISO-8601 string from `lastEventTimestamp.current` (AC-004; OWASP A03 — `encodeURIComponent` prevents query string injection)

---

## Current Project State
```
src/
└── web/
    └── src/
        ├── api/
        │   └── queueApi.ts                     (MODIFY — add since param to getQueue)
        ├── pages/
        │   └── QueueDashboardPage.tsx           (MODIFY — SignalR connection + event handlers + reconnection)
        └── styles/
            └── queue.css                        (MODIFY/CREATE — .queue-row--new highlight class)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | src/web/src/api/queueApi.ts | Add since param to getQueue |
| MODIFY | src/web/src/pages/QueueDashboardPage.tsx | HubConnection lifecycle, event handlers, newRowId, lastEventTimestamp, onreconnected |
| MODIFY | src/web/src/styles/queue.css | .queue-row--new CSS class with --color-highlight and 1.5s transition |

---

## External References
- .propel/context/wireframes/Hi-Fi/wireframe-SCR-011-staff-queue-dashboard.html (SCR-011 HTML wireframe — highlight animation row treatment, new entry position in table, summary bar update timing)
- https://learn.microsoft.com/en-us/aspnet/core/signalr/javascript-client?view=aspnetcore-8.0 (SignalR JavaScript client — `HubConnectionBuilder`, `withAutomaticReconnect`, `onreconnected`, `accessTokenFactory`; AC-001, AC-004)
- https://learn.microsoft.com/en-us/aspnet/core/signalr/authn-and-authz?view=aspnetcore-8.0#bearer-token-authentication (SignalR bearer token — `accessTokenFactory` pattern for WebSocket JWT; AC-001; OWASP A01)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] Open SCR-011 as Staff; trigger a walk-in booking; verify new row appears at top within 2 seconds with `--color-highlight` background; wait 1.6s; verify row fades to default colour (AC-001; AC-003; UXR-502)
- [ ] Mark a patient as arrived via a second browser tab; verify the first tab's row status updates to "Arrived" within 2 seconds without a page reload (AC-002)
- [ ] Disconnect the SignalR client (network offline); trigger 2 booking changes; reconnect; verify the 2 missed rows appear in the table without duplicating existing rows (AC-004)
- [ ] Verify `lastEventTimestamp.current` is updated each time a `QueueEntryAdded` or `QueueEntryUpdated` event is received (AC-004 — reconnection accuracy)
- [ ] Verify the `reconcile` function does not create duplicate rows when a `getQueue?since=` response includes entries already present in `queue` state (AC-004 — deduplication)
- [ ] Add 5 rows via SignalR; verify summary bar "N Waiting" count increments correctly for each `QueueEntryAdded` event (AC-001 — summary count)
- [ ] Verify that on component unmount (navigate away from `/queue`), `connection.stop()` is called and no further SignalR handlers fire (memory leak prevention)

---

## Implementation Checklist
- [x] `accessTokenFactory` reads the access token from the in-memory React auth context — it does not read from `localStorage` or `sessionStorage` directly; the token is the same credential used for HTTP API calls (OWASP A01; AC-001)
- [x] `connection.stop()` is called in the `useEffect` cleanup function so the WebSocket is closed when the component unmounts; event handlers registered with `connection.on(...)` are not duplicated if `useEffect` runs more than once (React strict mode) because `connection.off(...)` clears them (AC-001 — no memory leak)
- [x] The `reconcile` function merges by `entry.id` using a `Map` for O(1) lookup — incoming entries that match an existing id are replaced in-place; entries with new ids are appended; the function returns a new array reference to trigger a React re-render (AC-004 — no duplicates)
- [x] `setNewRowId(null)` is scheduled via `setTimeout(1600)` (100ms after the 1.5s CSS transition ends) — not via a `transitionend` event listener, which is unreliable when the component re-renders during the transition (AC-003; UXR-502)
- [x] The `since` parameter appended to the reconnection fetch URL is encoded with `encodeURIComponent` to prevent query string injection from a malformed ISO timestamp (AC-004; OWASP A03)
- [x] `lastEventTimestamp.current` is initialised to `new Date().toISOString()` at mount time so that even the very first reconnection call does not request the entire queue history from epoch zero (AC-004 — scoped missed-event window)
- [x] Both `QueueEntryAdded` and `QueueEntryUpdated` handlers update `queue` state immutably using `setQueue(q => ...)` — they never mutate the existing array; `useMemo` hooks for summary bar and sorted queue recalculate correctly when the state reference changes (AC-001, AC-002)
