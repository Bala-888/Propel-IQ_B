# Task - TASK_002

## Requirement Reference
- **User Story:** us_031
- **Story Location:** .propel/context/tasks/EP-006/us_031/us_031.md
- **Acceptance Criteria:**
  - AC-001: SCR-011 renders at `/queue` for Staff/Admin; table columns: Position, Patient Name, Arrival Time, Appointment Time, Wait Time, Risk Tier, Status; one row per queue entry
  - AC-002: Risk Tier badge shows coloured background + warning icon + text label ("High Risk", "Medium Risk", "Low Risk", "Unknown") — not colour alone (UXR-403, UXR-105)
  - AC-003: Wait Time cell updates every 30 seconds via `setInterval` without page reload; value = `now - arrivalTime` in whole minutes
  - AC-004: Table is sortable by Position, Wait Time, and Risk Tier; active column header shows `▲`/`▼` sort direction arrow
  - AC-005: Summary bar above table shows `"{n} Waiting | {n} High Risk | {n} Medium Risk | {n} Low Risk"` — counts derived from current queue data
- **Edge Cases:**
  - Empty queue: if `queue.length === 0` after fetch, render `<tr><td colSpan={7}>No patients in queue for today.</td></tr>` — not an empty `<tbody>`
  - Null risk score: if `noShowRiskTier === "Unknown"` (normalised by API), badge shows "Unknown" with neutral grey background and a circle info icon — no blank cell

---

## Design References
| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | SCR-011 (Staff Queue Dashboard) |
| **Wireframe Status** | AVAILABLE |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | .propel/context/wireframes/Hi-Fi/wireframe-SCR-011-staff-queue-dashboard.html |
| **Screen Spec** | SCR-011 — table with Position, Patient Name, Arrival Time, Appointment Time, Wait Time, Risk Tier, Status; summary bar above table |
| **UXR Requirements** | UXR-403 (risk badge shows coloured badge + text label), UXR-105 (no colour-only encoding for any status or risk state) |
| **Design Tokens** | Risk tier colours: High = red-600, Medium = amber-500, Low = green-600, Unknown = grey-400 (icon + text for all; tokens from project design system) |

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
| Frontend | React + TypeScript | 18.x / 5.x | TR-001 — `QueueDashboardPage` (SCR-011) + `RiskBadge` component + `useQueueData` hook; controlled sort state; interval-based live wait time (AC-001–005) |
| HTTP Client | Fetch API (browser built-in) | Web platform | `GET /api/queue?date=today` on mount to load queue data (AC-001) |
| Routing | React Router | v6 | `/queue` route with Staff/Admin role guard; redirect on auth failure (AC-001; OWASP A01) |

---

## Task Overview

Build `QueueDashboardPage` (SCR-011) with a `RiskBadge` sub-component and a live-updating wait time mechanism. The page fetches `GET /api/queue?date=today` on mount. A `setInterval(30_000)` tick forces re-evaluation of the `waitTime` computation per row without re-fetching. Table columns sort client-side; the Risk Tier column uses a numeric comparator (`High=3, Medium=2, Low=1, Unknown=0`). A summary bar above the table derives counts from the raw queue array. Empty-queue and unknown-risk edge states are rendered explicitly.

---

## Dependent Tasks
- task_001 (us_031) — `GET /api/queue?date=today` must be available with the `QueueEntryDto` contract
- task_001 (us_009) — JWT context and role guard utilities must be available for route protection

---

## Impacted Components
- `src/web/src/pages/QueueDashboardPage.tsx` — new: SCR-011 queue table, summary bar, sort state, interval tick
- `src/web/src/components/queue/RiskBadge.tsx` — new: tier-based badge with icon + text label
- `src/web/src/api/queueApi.ts` — new: typed fetch wrapper for `GET /api/queue`

---

## Implementation Plan
1. `queueApi.ts`: `getQueue(date?: string): Promise<QueueEntry[]>` wraps `GET /api/queue?date={date ?? 'today'}`; on non-2xx, throws a typed `ApiError`; `QueueEntry` interface mirrors `QueueEntryDto` — `arrivalTime: string | null` (ISO-8601), `noShowRiskTier: "High" | "Medium" | "Low" | "Unknown"` (AC-001)
2. `QueueDashboardPage` (SCR-011): Staff/Admin route guard; calls `getQueue()` on mount via `useEffect`; stores `queue: QueueEntry[]` and `sortState: { column: SortColumn; direction: 'asc' | 'desc' }` in `useState`; computes `sortedQueue` via `useMemo` based on `queue` and `sortState` (AC-001, AC-004)
3. `RiskBadge` component: `tier: "High" | "Medium" | "Low" | "Unknown"` prop; renders `<span>` with tier-mapped background colour class + a tier-mapped SVG icon (`⚠` warning for High, `△` caution for Medium, `✓` check for Low, `ℹ` info circle for Unknown) + visible text label (`"{tier} Risk"` or `"Unknown"`); never uses colour as the only signal (AC-002; UXR-403; UXR-105; WCAG 2.1)
4. Live wait time: a second `useEffect` sets `const id = setInterval(() => setTick(t => t + 1), 30_000)` where `tick` is a counter state; the Wait Time cell renders `Math.floor((Date.now() - new Date(entry.arrivalTime!).getTime()) / 60_000)` using the counter to trigger re-evaluation each tick; if `entry.arrivalTime` is null, the cell shows `"—"`; `clearInterval(id)` returned from the effect (AC-003)
5. Sortable column headers: each sortable `<th>` renders an `onClick` handler that sets `sortState`; if clicking the same column, direction toggles; if a new column, direction defaults to `asc`; active column header appends `aria-sort="ascending"` or `aria-sort="descending"` for accessibility; sort arrow `▲`/`▼` shown inline (AC-004; WCAG 2.1)
6. Summary bar: placed above `<table>` as `<div role="status">`; derived via `useMemo` from unsorted `queue`: `total`, `highCount`, `mediumCount`, `lowCount`; renders `"{total} Waiting | {highCount} High Risk | {mediumCount} Medium Risk | {lowCount} Low Risk"` — recalculates when `queue` changes (AC-005)
7. Edge states: if `queue.length === 0` after a successful fetch, render a single `<tr><td colSpan={7}>No patients in queue for today.</td></tr>` in the table body — never an empty `<tbody>`; if a row has `noShowRiskTier === "Unknown"`, `RiskBadge` renders the grey info-circle variant with the visible text "Unknown" — not a blank `<td>` (Edge: empty queue; Edge: null risk; UXR-105)

---

## Current Project State
```
src/
└── web/
    └── src/
        ├── api/
        │   └── (queueApi.ts                     — CREATE)
        ├── components/
        │   └── queue/
        │       └── (RiskBadge.tsx               — CREATE)
        └── pages/
            └── (QueueDashboardPage.tsx           — CREATE)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | src/web/src/api/queueApi.ts | Typed fetch wrapper for GET /api/queue |
| CREATE | src/web/src/components/queue/RiskBadge.tsx | Risk tier badge with icon + text label |
| CREATE | src/web/src/pages/QueueDashboardPage.tsx | SCR-011 queue dashboard page |

---

## External References
- .propel/context/wireframes/Hi-Fi/wireframe-SCR-011-staff-queue-dashboard.html (SCR-011 HTML wireframe — column layout, summary bar position, badge colour and icon placement, empty-state message)
- https://www.w3.org/WAI/ARIA/apg/patterns/table/ (ARIA table pattern — `aria-sort` on sortable column headers; accessible risk badge; AC-004 accessibility)
- https://developer.mozilla.org/en-US/docs/Web/API/setInterval (MDN setInterval — 30s interval for live wait time update; cleanup via `clearInterval` in useEffect return; AC-003)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] Navigate to `/queue` as Staff; verify table renders 7 columns with correct headers; verify `aria-sort` attribute on the active sorted column (AC-001; AC-004)
- [ ] Verify Risk Tier badge for a "High" row shows red background + warning icon + "High Risk" text — no red-only cell (AC-002; UXR-403; UXR-105)
- [ ] Verify Risk Tier badge for an "Unknown" row shows grey background + info-circle icon + "Unknown" text — not a blank cell (Edge: null risk; UXR-105)
- [ ] Wait 30 seconds with the page open; verify Wait Time column values increment by 0–1 minutes without page reload (AC-003)
- [ ] Click "Risk Tier" column header; verify rows re-sort High → Medium → Low → Unknown descending; click again to toggle to ascending (AC-004)
- [ ] Stub `GET /api/queue` to return an empty array; verify `"No patients in queue for today."` appears in the table body — not an empty `<tbody>` (Edge: empty queue)
- [ ] Verify summary bar shows correct counts when queue has mixed risk tiers; verify counts update after a data refresh (AC-005)
- [ ] Navigate to `/queue` as a Patient-role user; verify redirect to unauthorised page (OWASP A01)

---

## Implementation Checklist
- [ ] `/queue` route is guarded by a Staff/Admin role check before `QueueDashboardPage` mounts; Patient-role users are redirected to the unauthorised page — the component never renders for non-staff (OWASP A01; AC-001)
- [ ] `RiskBadge` renders both an SVG icon and a visible text label for every tier value including "Unknown"; the badge background colour is supplementary, not the sole risk signal (AC-002; UXR-403; UXR-105; WCAG 2.1 SC 1.4.1)
- [ ] The wait-time `setInterval` is created in a `useEffect` and its return function calls `clearInterval`; this prevents the interval from running after the component unmounts and avoids memory leaks (AC-003)
- [ ] Risk Tier sort uses the numeric map `{ High: 3, Medium: 2, Low: 1, Unknown: 0 }` to produce a deterministic descending sort; Position and Wait Time sort numerically; the `sortedQueue` is derived via `useMemo` to avoid redundant re-sort on unrelated re-renders (AC-004)
- [ ] Summary bar uses `<div role="status">` so screen readers announce count changes; counts are derived from the unsorted `queue` array — sorting does not affect the totals (AC-005; WCAG 2.1)
- [ ] Empty-queue state renders a single `<tr>` with `<td colSpan={7}>` — the `<table>` and `<thead>` are still rendered so assistive technologies have context for the "no data" message (Edge: empty queue)
- [ ] `noShowRiskTier` typed as `"High" | "Medium" | "Low" | "Unknown"` in the TypeScript interface — the `"Unknown"` string value is the contract from the API (never `null`), enforced by the union type so a blank-cell path is not compilable (Edge: null risk; AC-002)
