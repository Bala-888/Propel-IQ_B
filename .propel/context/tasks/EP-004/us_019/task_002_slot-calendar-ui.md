# Task - TASK_002

## Requirement Reference
- **User Story:** us_019
- **Story Location:** .propel/context/tasks/EP-004/us_019/us_019.md
- **Acceptance Criteria:**
  - AC-001: The booking screen (SCR-006) fetches and displays only Available slots — no Booked or Blocked slots appear in the calendar grid
  - AC-002: The calendar respects pagination; fetching page 1 of 10 items from 50 total shows 10 slots and the pagination `total`, `page`, `pageSize`, `totalPages` values are reflected in the UI controls
  - AC-003: Available slots are displayed grouped by date; each slot shows the start time, duration in minutes, and an "Available" label with icon + text
  - AC-004: Clicking a slot highlights it with `--color-primary-subtle` background and enables the "Book this slot" button
- **Edge Cases:**
  - No available slots: when `slots` is empty, SCR-006 displays "No available slots at this time. Please check back later or add yourself to a wait list." — the calendar grid is not rendered
  - Slot becomes booked between page load and selection: this screen displays only the initial fetched state; the 409 conflict is handled in us_020 during booking submission — no action required here

---

## Design References
| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | SCR-006 (Appointment Slot Calendar) |
| **Wireframe Status** | AVAILABLE |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | .propel/context/wireframes/Hi-Fi/wireframe-SCR-006-appointment-slot-calendar.html |
| **Screen Spec** | SCR-006 |
| **UXR Requirements** | UXR-105 — availability status indicator uses icon + text, never colour alone; UXR-604 — a soft-alert banner for missing patient insurance information is shown on this screen above the calendar |
| **Design Tokens** | Refer to project design system tokens for `--color-primary-subtle` (selected slot background), slot cell layout, calendar date header, pagination control, and soft-alert banner |

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
| Frontend | React | 18.x | TR-001 (SCR-006 SlotCalendar component; slot selection state; pagination state) |
| Frontend | TypeScript | 5.x | TR-001 (typed SlotDto, SlotsResponse, PaginationMeta interfaces matching backend DTO) |
| Frontend | Vite | 5.x | TR-001 (build tooling) |
| Frontend | React Router | v6 | TR-001 (`useNavigate` to pass selected slot to booking confirmation screen in us_020) |

---

## Task Overview

Build the appointment slot calendar for SCR-006. On mount, `GET /slots?available=true&page=1&pageSize=10` is called and results are grouped by date for display. Each slot cell shows start time, duration, and an icon + text "Available" label. A single `selectedSlotId` state highlights the chosen slot and enables the "Book this slot" button. Pagination controls allow paging through results. An empty-state message replaces the calendar when no slots are returned. A soft-alert banner for missing insurance information (UXR-604) is shown above the calendar when the patient profile has no `insuranceId`.

---

## Dependent Tasks
- task_001 (us_019) — `GET /slots?available=true` endpoint with pagination must be available
- task_001 (us_009) — authenticated patient session context (`accessToken`, patient profile with `insuranceId`) must be available for the Authorization header and UXR-604 insurance check

---

## Impacted Components
- `src/web/src/features/slots/SlotCalendar.tsx` — new: main SCR-006 component; date-grouped slot grid; selection state; empty state; pagination; UXR-604 banner
- `src/web/src/features/slots/SlotCell.tsx` — new: individual slot cell (time, duration, icon + text "Available" label, selection highlight)
- `src/web/src/features/slots/SlotPagination.tsx` — new: Previous / Next pagination controls with current page indicator
- `src/web/src/api/slotsApi.ts` — new: `getSlots({page, pageSize}): Promise<SlotsResponse>` typed wrapper

---

## Implementation Plan
1. Create `slotsApi.ts` with `getSlots({page, pageSize}: {page: number, pageSize: number}): Promise<SlotsResponse>` — calls `GET /api/slots?available=true&page=${page}&pageSize=${pageSize}` with `Authorization: Bearer <accessToken>`; `SlotsResponse` is `{slots: SlotDto[], pagination: {total: number, page: number, pageSize: number, totalPages: number}}`; `SlotDto` is `{id: string, date: string, startTime: string, durationMinutes: number, status: string}` (AC-001, AC-002)
2. Build `SlotCalendar.tsx`: `useState<SlotDto[]>([])`, `useState<PaginationMeta>`, `useState<string|null>(null)` for `selectedSlotId`, `useState<boolean>(false)` for `isFetching`, `useState<string|null>(null)` for `fetchError`; call `getSlots({page:1, pageSize:10})` in `useEffect` on mount; group `slots` by `date` field using `Array.reduce` into `Record<string, SlotDto[]>` (AC-003)
3. Render grouped calendar: for each date key (sorted ascending), render a `<section>` with a `<h3>` date header; inside render a `<SlotCell>` for each slot in that date group; if `isFetching`, render a loading skeleton in place of the grid; if `fetchError`, render `<span role="alert">` with icon + text error message (AC-003; WCAG 4.1.3; UXR-105)
4. Build `SlotCell.tsx`: accepts `slot: SlotDto`, `isSelected: boolean`, `onSelect: (id: string) => void`; renders the formatted `startTime`, `"${durationMinutes} min"`, and a `<span>` containing a named icon component + text `"Available"` — no colour-only status; applies `style={{ backgroundColor: 'var(--color-primary-subtle)' }}` when `isSelected`; calls `onSelect(slot.id)` on click; minimum 44×44px touch target (AC-003, AC-004; UXR-105; WCAG 1.4.1; WCAG 2.5.5)
5. Empty state: when `!isFetching && slots.length === 0 && !fetchError`, render `<p>` "No available slots at this time. Please check back later or add yourself to a wait list." — the `<section>` date-group grid is not rendered (Edge: no available slots; UXR-105)
6. Build `SlotPagination.tsx`: renders "← Previous" and "Next →" `<button>` elements; "Previous" `disabled` when `page === 1`; "Next" `disabled` when `page === pagination.totalPages`; shows current page indicator `"Page {page} of {totalPages}"`; on click, calls `onPageChange(page ± 1)` prop; `SlotCalendar` calls `getSlots({page: newPage, pageSize})` and updates `slots` and `pagination` state (AC-002)
7. Insurance soft-alert (UXR-604): read `patient.insuranceId` from auth context (or `usePatientProfile` hook); if `insuranceId` is absent or empty, render a `<div role="status" aria-live="polite">` banner above the calendar containing a named icon component + text "Your insurance information may be missing. Consider adding it before booking." with a dismiss `×` button; dismissible via `useState<boolean>(isDismissed)` (UXR-604; UXR-105; WCAG 1.4.1; WCAG 4.1.3)
8. "Book this slot" `<button>`: `disabled={selectedSlotId === null}`; when enabled, calls `onSlotConfirmed(selectedSlotId)` prop (or `navigate('/booking/confirm', { state: { slotId: selectedSlotId } })`); button has `aria-label="Book this slot"` and minimum 44×44px touch target; `disabled` state adds a visual text/icon indicator beyond colour (AC-004; UXR-105; WCAG 2.5.5; WCAG 4.1.2)

---

## Current Project State
```
src/
└── web/
    └── src/
        ├── features/
        │   └── slots/
        │       └── (SlotCalendar.tsx     — CREATE)
        │       └── (SlotCell.tsx         — CREATE)
        │       └── (SlotPagination.tsx   — CREATE)
        └── api/
            └── (slotsApi.ts             — CREATE)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | src/web/src/api/slotsApi.ts | getSlots typed API wrapper |
| CREATE | src/web/src/features/slots/SlotCalendar.tsx | Main SCR-006 component; date-grouped grid; UXR-604 banner |
| CREATE | src/web/src/features/slots/SlotCell.tsx | Individual slot cell with icon + text label and selection highlight |
| CREATE | src/web/src/features/slots/SlotPagination.tsx | Previous / Next pagination controls |

---

## External References
- https://react.dev/reference/react/useState (React 18 useState — slot selection, pagination, fetch state)
- https://react.dev/reference/react/useEffect (React 18 useEffect — fetch on mount)
- https://reactrouter.com/en/main/hooks/use-navigate (React Router v6 useNavigate with state — pass selectedSlotId to booking confirmation screen)
- https://www.w3.org/WAI/WCAG21/Understanding/target-size.html (WCAG 2.5.5 — 44×44px touch target for slot cells and pagination buttons)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] Load SCR-006; verify slots are grouped by date with a date `<h3>` header for each group; no Booked or Blocked slots appear in the grid (AC-001, AC-003)
- [ ] With 50 available slots, load page 1 of 10; verify exactly 10 slots are shown and the pagination indicator reads "Page 1 of 5" (AC-002)
- [ ] Click Next page; verify the next 10 slots load and page indicator updates; click Previous; verify return to page 1 (AC-002)
- [ ] Click a slot cell; verify the cell background changes to `--color-primary-subtle` and the "Book this slot" button becomes enabled (AC-004)
- [ ] Load SCR-006 when no available slots exist; verify the empty-state paragraph is shown and the calendar grid is absent (Edge: no available slots)
- [ ] Load SCR-006 with a patient profile that has no `insuranceId`; verify the UXR-604 soft-alert banner appears with icon + text above the calendar; click dismiss and verify it disappears (UXR-604; UXR-105)
- [ ] Verify each slot cell's "Available" indicator uses a named icon component + text — no colour-only status (UXR-105; WCAG 1.4.1)
- [ ] Verify "Book this slot" button is `disabled` when no slot is selected and enabled when one is selected; disabled state has a visual indicator beyond colour (AC-004; UXR-105; WCAG 1.4.1)

---

## Implementation Checklist
- [ ] `slotsApi.ts` calls `GET /api/slots?available=true&page=N&pageSize=N`; the `available=true` parameter is always included so Booked and Blocked slots are never fetched by the calendar component (AC-001; minimum data fetch)
- [ ] `SlotCalendar` groups the `slots` array by `date` string using `Array.reduce`; renders one labelled `<section>` per date in ascending order; the calendar grid is replaced by the empty-state `<p>` when `slots.length === 0` after a successful fetch (AC-003; Edge: no available slots)
- [ ] `SlotCell` renders a named icon component alongside the text `"Available"` within a `<span>` — availability status is never communicated by background or text colour change alone (AC-003; UXR-105; WCAG 1.4.1)
- [ ] `SlotCell` applies `style={{ backgroundColor: 'var(--color-primary-subtle)' }}` when `isSelected`; clicking a cell calls `onSelect(slot.id)` which updates `selectedSlotId` in `SlotCalendar`; only one slot may be selected at a time (AC-004)
- [ ] "Book this slot" `<button>` has `disabled={selectedSlotId === null}` and `aria-label="Book this slot"`; the disabled state includes a visual indicator (e.g., reduced opacity + aria-disabled label) beyond colour change; minimum 44×44px touch target (AC-004; UXR-105; WCAG 2.5.5; WCAG 4.1.2)
- [ ] `SlotPagination` renders "Previous" and "Next" as `<button>` elements; each is `disabled` at the boundary (`page === 1` and `page === totalPages` respectively); disabled buttons include `aria-disabled="true"` and a visual icon/text beyond colour (AC-002; WCAG 4.1.2)
- [ ] Insurance soft-alert (UXR-604) is rendered as `<div role="status" aria-live="polite">` with a named icon component + text message above the calendar; it is dismissible; rendered only when `patient.insuranceId` is absent — not shown when insurance data exists (UXR-604; UXR-105; WCAG 4.1.3)
- [ ] Loading skeleton replaces the calendar grid while `isFetching = true`; on API error, `<span role="alert">` with icon + text error message is rendered — the calendar grid is not shown in either transient state (WCAG 4.1.3; UXR-105)
