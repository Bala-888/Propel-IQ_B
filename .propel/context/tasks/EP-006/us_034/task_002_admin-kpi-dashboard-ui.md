# Task - TASK_002

## Requirement Reference
- **User Story:** us_034
- **Story Location:** .propel/context/tasks/EP-006/us_034/us_034.md
- **Acceptance Criteria:**
  - AC-001: SCR-016 at `/admin/kpi` displays metric cards for Total Bookings, Confirmed, Cancelled, Walk-ins, Average Wait Time, and No-show Risk Distribution — populated from `GET /admin/metrics?date=today`
  - AC-002: `GET /admin/metrics?date=today` is re-called automatically every 60 seconds; metric card values update in-place without a full page reload
  - AC-003: "User Management" link or button on SCR-016 navigates to SCR-017 (`/admin/users`) in the same browser tab — no new tab
- **Edge Cases:**
  - No bookings today: all metric cards display `"0"` (or `"—"` for `averageWaitMinutes` when null) — not an error state or loading spinner
  - API returns 503: display `"Data temporarily unavailable. Refreshing..."` in the metric cards area; component does not crash; auto-refresh interval continues so the message clears on the next successful fetch

---

## Design References
| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | SCR-016 (Admin KPI Dashboard), SCR-017 (Admin User Management) |
| **Wireframe Status** | AVAILABLE |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | .propel/context/wireframes/Hi-Fi/wireframe-SCR-016-admin-kpi-dashboard.html |
| **Screen Spec** | SCR-016 — 6 metric cards + No-show Risk Distribution section + User Management navigation link |
| **UXR Requirements** | UXR-105 (No-show risk distribution uses icon + text label for each tier — not colour alone) |
| **Design Tokens** | Metric card background, risk tier icons (warning/caution/check), unavailable-state text colour (from project design system) |

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
| Frontend | React + TypeScript | 18.x / 5.x | TR-001 — `AdminKpiDashboardPage` (SCR-016); metric cards; auto-refresh interval; 503 error state (AC-001, AC-002, AC-003) |
| HTTP Client | Fetch API (browser built-in) | Web platform | `GET /admin/metrics?date=today` on mount and every 60s (AC-001, AC-002) |
| Routing | React Router | v6 | `/admin/kpi` Admin-only route; `<Link to="/admin/users">` for SCR-017 navigation (AC-003) |

---

## Task Overview

Build `AdminKpiDashboardPage` (SCR-016) at `/admin/kpi` with an Admin role guard. On mount the page fetches `GET /admin/metrics?date=today` and populates six metric cards plus a no-show risk distribution section. A `setInterval(60_000)` triggers automatic re-fetch without a page reload. The "User Management" link uses React Router `<Link>` to navigate to `/admin/users` in the same tab. A 503 response surfaces a "Data temporarily unavailable. Refreshing..." message in the cards area without crashing the component; auto-refresh continues. Zero metric values render as `"0"`.

---

## Dependent Tasks
- task_001 (us_034) — `GET /admin/metrics?date=today` endpoint must be available with the `AdminMetricsDto` contract
- task_001 (us_009) — JWT context and Admin role guard utilities must be present for route protection
- task_002 (us_011) — SCR-017 (`/admin/users`) must exist as the navigation target for the "User Management" link (AC-003)

---

## Impacted Components
- `src/web/src/pages/AdminKpiDashboardPage.tsx` — new: SCR-016 metric cards, auto-refresh, User Management link, 503 handling
- `src/web/src/api/adminApi.ts` — new: `getAdminMetrics(): Promise<AdminMetrics>` fetch wrapper

---

## Implementation Plan
1. `adminApi.ts`: `getAdminMetrics(): Promise<AdminMetrics>` calls `GET /api/admin/metrics?date=today`; on 503 throws `ApiError { status: 503 }`; on non-2xx throws a typed `ApiError`; `AdminMetrics` TypeScript interface mirrors `AdminMetricsDto`: `{ totalBookings: number; confirmed: number; cancelled: number; walkIns: number; averageWaitMinutes: number | null; highRisk: number; mediumRisk: number; lowRisk: number }` (AC-001)
2. `AdminKpiDashboardPage` (SCR-016): Admin-only route guard; `useEffect` on mount calls `fetchMetrics()` which sets `metrics: AdminMetrics | null` and `error: "unavailable" | null`; sets `setInterval(fetchMetrics, 60_000)`; returns `clearInterval` in cleanup (AC-001, AC-002; OWASP A01)
3. Metric cards layout: six `<MetricCard>` elements rendering label + value: "Total Bookings Today" (`metrics.totalBookings`), "Confirmed" (`metrics.confirmed`), "Cancelled" (`metrics.cancelled`), "Walk-ins" (`metrics.walkIns`), "Avg Wait Time" (`metrics.averageWaitMinutes != null ? metrics.averageWaitMinutes.toFixed(0) + " min" : "—"`), and "No-show Risk" section; all values render as `"0"` when the count is zero — no empty state divergence (AC-001; Edge: no bookings)
4. No-show risk distribution section: three inline badges — each badge renders a tier-specific SVG icon alongside a visible text label: warning icon + `"High Risk: {n}"`, caution icon + `"Medium Risk: {n}"`, check icon + `"Low Risk: {n}"` — tier is never communicated by colour alone (UXR-105; WCAG 2.1 SC 1.4.1)
5. 503 error state: in the `catch` block, if `err.status === 503`, set `error = "unavailable"` and leave `metrics` as the last known value (or null on first load); render `<p role="alert">Data temporarily unavailable. Refreshing...</p>` above the metric cards area; the component continues to mount and the 60s interval keeps running so the banner disappears on the next successful fetch (Edge: query timeout)
6. "User Management" navigation: `<Link to="/admin/users">User Management</Link>` rendered as a prominent button-styled link using React Router v6 `<Link>` — no `target` attribute (same tab); navigates to SCR-017 per AC-003 (AC-003)

---

## Current Project State
```
src/
└── web/
    └── src/
        ├── api/
        │   └── (adminApi.ts                         — CREATE)
        └── pages/
            └── (AdminKpiDashboardPage.tsx            — CREATE)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | src/web/src/api/adminApi.ts | getAdminMetrics fetch wrapper |
| CREATE | src/web/src/pages/AdminKpiDashboardPage.tsx | SCR-016 KPI dashboard with metric cards, auto-refresh, 503 handling, User Management link |

---

## External References
- .propel/context/wireframes/Hi-Fi/wireframe-SCR-016-admin-kpi-dashboard.html (SCR-016 HTML wireframe — metric card layout, risk distribution badge placement, "User Management" button position)
- https://reactrouter.com/en/main/components/link (React Router v6 `<Link>` — same-tab navigation to `/admin/users`; AC-003)
- https://developer.mozilla.org/en-US/docs/Web/API/setInterval (MDN setInterval — 60s auto-refresh; cleanup via `clearInterval` in useEffect return; AC-002)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] Navigate to `/admin/kpi` as Admin; verify all 6 metric card labels render; verify values match a seeded booking dataset (AC-001)
- [ ] Stub `GET /admin/metrics` to return all zeros; verify all count cards show `"0"` and Average Wait Time shows `"—"` — not an error state (Edge: no bookings)
- [ ] Wait 60 seconds with page open; verify network tab shows a second `GET /admin/metrics` call and card values update (AC-002)
- [ ] Stub `GET /admin/metrics` to return 503; verify `"Data temporarily unavailable. Refreshing..."` alert appears; verify page does not crash; verify next 60s tick clears the alert when stub returns 200 (Edge: query timeout)
- [ ] Click "User Management" link; verify navigation to `/admin/users` in the same tab — no new tab opened (AC-003)
- [ ] Verify risk distribution badges each show an SVG icon and visible text label (`"High Risk: N"`) — no colour-only badge (UXR-105)
- [ ] Navigate to `/admin/kpi` as Staff role; verify redirect to unauthorised page (OWASP A01; AC-004)

---

## Implementation Checklist
- [ ] `/admin/kpi` route is guarded by an Admin-only role check before `AdminKpiDashboardPage` mounts; Staff and Patient roles are redirected before any API call is made (AC-004; OWASP A01)
- [ ] `setInterval(fetchMetrics, 60_000)` is created inside `useEffect` and its ID is returned via `clearInterval` in the cleanup function — the interval does not continue running after the component unmounts (AC-002 — no memory leak)
- [ ] The 503 error path sets `error = "unavailable"` without clearing `metrics` state; if the previous fetch was successful, metric cards continue to display the last known values below the "unavailable" banner (Edge: query timeout — graceful degradation)
- [ ] `averageWaitMinutes` is displayed as `"—"` when the API returns `null` and as the rounded value + `" min"` when a number — a null value is never rendered as `"0"` or `"null"` (AC-001; Edge: no bookings)
- [ ] The "User Management" `<Link>` component has no `target` attribute; it uses React Router v6 client-side navigation, keeping the admin in the same tab without a full page reload (AC-003)
- [ ] No-show risk distribution section renders a distinct SVG icon for each tier (High/Medium/Low) alongside the text label — the `aria-label` on each icon describes the tier for screen reader users; colour is supplementary (UXR-105; WCAG 2.1 SC 1.4.1)
