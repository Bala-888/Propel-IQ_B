# Task - TASK_002

## Requirement Reference
- **User Story:** us_032
- **Story Location:** .propel/context/tasks/EP-006/us_032/us_032.md
- **Acceptance Criteria:**
  - AC-001: "Mark Arrived" button (or accessible icon button) is visible inline in the queue row when `entry.status === "Waiting"` — no screen navigation required; button is hidden when `status === "Arrived"`
  - AC-002: Clicking "Mark Arrived" calls `PATCH /api/queue/{id}/arrived`; on 200, the row's Status cell updates to "Arrived" in place and the button is hidden — total user actions ≤ 2; no page reload
- **Edge Cases:**
  - Network failure / 5xx: row remains "Waiting"; toast appears "Could not mark as arrived. Please try again."; "Mark Arrived" button reappears for retry
  - Race condition (two staff, same row): second PATCH returns 200 from idempotent API; frontend treats it as a normal success — row shows "Arrived" with no error displayed

---

## Design References
| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | SCR-011 (Staff Queue Dashboard — arrived action) |
| **Wireframe Status** | AVAILABLE |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | .propel/context/wireframes/Hi-Fi/wireframe-SCR-011-staff-queue-dashboard.html |
| **Screen Spec** | SCR-011 — queue row inline action; Status column with arrived/waiting icon+text |
| **UXR Requirements** | UXR-106 (Mark Arrived inline ≤2 clicks), UXR-105 (status change uses icon + text, not colour alone) |
| **Design Tokens** | Status Arrived: green-600 accent + checkmark icon; Status Waiting: grey-500 + clock icon (icon+text required per UXR-105) |

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
| Frontend | React + TypeScript | 18.x / 5.x | TR-001 — inline queue row action on `QueueDashboardPage` (SCR-011); per-row loading state; error toast; status icon+text cell update (AC-001, AC-002) |
| HTTP Client | Fetch API (browser built-in) | Web platform | `PATCH /api/queue/{id}/arrived` — single request per click; no library needed (AC-002) |

---

## Task Overview

Extend `QueueDashboardPage` (SCR-011) with an inline "Mark Arrived" action per queue row. The action is a single button visible only when `status === "Waiting"`. Clicking it fires `PATCH /api/queue/{id}/arrived` and, on 200, updates the row's status in the local `queue` state to `"Arrived"` and hides the button — all in-place without navigating away. A per-row `loading` state shows a spinner during the in-flight request. On network failure or 5xx, the row reverts to `"Waiting"`, the button reappears, and a toast is shown. The Status column uses icon + text for both states to satisfy UXR-105.

---

## Dependent Tasks
- task_002 (us_031) — `QueueDashboardPage` and its `queue: QueueEntry[]` state must exist before this inline action can be added
- task_001 (us_032) — `PATCH /api/queue/{id}/arrived` endpoint must be available

---

## Impacted Components
- `src/web/src/pages/QueueDashboardPage.tsx` — modified: add per-row loading state; "Mark Arrived" button; `markArrived` handler; status cell icon+text
- `src/web/src/api/queueApi.ts` — modified: add `markArrived(queueEntryId: string): Promise<ArrivedResponse>` fetch wrapper

---

## Implementation Plan
1. Add `markArrived(queueEntryId: string): Promise<ArrivedResponse>` to `queueApi.ts`: issues `PATCH /api/queue/{queueEntryId}/arrived` with no request body; returns `ArrivedResponse { status: "Arrived"; arrivedAt: string }` on 200; throws a typed `ApiError` on non-2xx (AC-002)
2. Per-row loading state in `QueueDashboardPage`: add `loadingRows: Set<string>` state (keyed by `entry.id`); the "Mark Arrived" button for row `entry.id` shows a spinner and is `disabled` while `loadingRows.has(entry.id)` is true; other rows' buttons remain interactive (AC-001; UXR-106)
3. `handleMarkArrived(entry: QueueEntry)` handler: adds `entry.id` to `loadingRows`; calls `markArrived(entry.id)`; on 200, updates `queue` state immutably: `setQueue(q => q.map(r => r.id === entry.id ? { ...r, status: "Arrived", arrivedAt: response.arrivedAt } : r))`; removes `entry.id` from `loadingRows` (AC-002)
4. "Mark Arrived" button renders only when `entry.status === "Waiting"`: `{entry.status === "Waiting" && <button aria-label="Mark arrived" onClick={() => handleMarkArrived(entry)}>…</button>}`; when `status === "Arrived"` the button is absent from the DOM — not just `display:none` or `disabled` (AC-001; UXR-106 ≤2 clicks)
5. Status cell icon+text: `"Waiting"` → clock SVG icon + `<span>Waiting</span>`; `"Arrived"` → checkmark SVG icon + `<span>Arrived</span>` — status is never conveyed by colour change alone; the icon and text must both be present (UXR-105; WCAG 2.1 SC 1.4.1)
6. Error handling on PATCH failure: catch block removes `entry.id` from `loadingRows`; ensures `queue` state is not mutated (row stays `"Waiting"`); shows a toast notification "Could not mark as arrived. Please try again." so the "Mark Arrived" button naturally reappears since `entry.status` is still `"Waiting"` (Edge: network failure; AC-001 — button reappears automatically because it is conditional on status)

---

## Current Project State
```
src/
└── web/
    └── src/
        ├── api/
        │   └── queueApi.ts                (MODIFY — add markArrived)
        └── pages/
            └── QueueDashboardPage.tsx     (MODIFY — add per-row loading + button + status cell)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | src/web/src/api/queueApi.ts | Add markArrived PATCH wrapper |
| MODIFY | src/web/src/pages/QueueDashboardPage.tsx | Per-row loading state, Mark Arrived button, status icon+text cell |

---

## External References
- .propel/context/wireframes/Hi-Fi/wireframe-SCR-011-staff-queue-dashboard.html (SCR-011 HTML wireframe — position of "Mark Arrived" button in row, Status column icon+text layout, button loading spinner treatment)
- https://www.w3.org/WAI/ARIA/apg/patterns/button/ (ARIA button pattern — `aria-label="Mark arrived"` for icon-only button variant; `aria-busy="true"` during loading state; UXR-106 accessibility)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] Render SCR-011 with a "Waiting" queue row; verify "Mark Arrived" button is visible; click once; verify PATCH is called and the row status updates to "Arrived" in-place with checkmark icon + "Arrived" text; verify button is gone (AC-001, AC-002; UXR-105)
- [ ] Verify the status cell for a "Waiting" row shows clock icon + "Waiting" text — not colour only (UXR-105)
- [ ] Verify the status cell for an "Arrived" row shows checkmark icon + "Arrived" text — not colour only (UXR-105)
- [ ] Stub PATCH to return 500; click "Mark Arrived"; verify row stays "Waiting", button reappears, toast "Could not mark as arrived. Please try again." is shown (Edge: network failure)
- [ ] Simulate two rapid clicks on "Mark Arrived" for the same row (second click while in-flight); verify second PATCH still returns 200 and the row settles on "Arrived" without an error toast (Edge: race condition; AC-004)
- [ ] Verify that while one row's PATCH is in-flight (spinner shown), other rows' "Mark Arrived" buttons remain clickable (AC-001 — per-row loading, not global)
- [ ] Navigate to `/queue` as Patient role; verify redirect before page renders (OWASP A01 — route guard from us_031 still enforced)

---

## Implementation Checklist
- [x] "Mark Arrived" button is conditionally rendered with `{entry.status === 'Confirmed' && <button>…</button>}` — it is absent from the DOM entirely when `status === 'CheckedIn'`, not hidden with CSS; this ensures the button reappears naturally on error revert without additional state management (AC-001; UXR-106; decision logged: F009)
- [x] `loadingRows` uses `Set<number>` so the loading state is keyed per row; setting one row as loading does not affect the `disabled` state of other rows' buttons (AC-001 — inline action, not global block)
- [x] `queue` state is updated immutably on success via `setQueue(q => q.map(...))` — the spread `{ ...r, status: response.status, arrivalTime: response.arrivedAt }` creates a new object reference, triggering a re-render only for the affected row (AC-002 — in-place update)
- [x] On PATCH failure the catch block must not call `setQueue` — the `queue` state remains unchanged so `entry.status` stays `'Confirmed'` and the "Mark Arrived" button reappears automatically on the next render (Edge: network failure; AC-001)
- [x] Status cells for both "Confirmed" and "CheckedIn" states render an SVG icon alongside the visible text label; the colour accent is supplementary — never the sole indicator of status (UXR-105; WCAG 2.1 SC 1.4.1)
- [x] The `markArrived` fetch wrapper sends no request body (PATCH with empty body); it does not attach a client-side timestamp to the request in any field or header (AC-003 — server-side timestamp enforced at API layer, frontend must not attempt to override)
