# Task - TASK_002

## Requirement Reference
- **User Story:** us_028
- **Story Location:** .propel/context/tasks/EP-005/us_028/us_028.md
- **Acceptance Criteria:**
  - AC-001: "Add to Google Calendar" button on SCR-007 triggers `POST /api/calendar/sync {provider: "Google", bookingId}`; inline success status shown after 202 response
  - AC-002: "Add to Outlook Calendar" button triggers the same endpoint with `provider: "Outlook"`; independent of Google Calendar button state
  - AC-005: Calendar sync status is a background concern — SCR-007 confirmation details render immediately without waiting for sync; a sync failure shows an inline error icon + retry option but never blocks or reverts the confirmation view
- **Edge Cases:**
  - OAuth token expired: if the sync response carries `{"syncResult": "TokenExpired"}` (polled via `GET /api/calendar/sync/status/{bookingId}`), display banner `<div role="alert">` "Calendar sync unavailable. Please reconnect your calendar in Settings." — banner persists until dismissed or reconnected
  - SCR-007 loads before sync completes: confirmation page renders booking details synchronously from route state; calendar sync buttons are rendered in a separate async section that does not block the main confirmation content

---

## Design References
| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | SCR-007 (Booking Confirmation — calendar add buttons) |
| **Wireframe Status** | AVAILABLE |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | .propel/context/wireframes/Hi-Fi/wireframe-SCR-007-booking-confirmation.html |
| **Screen Spec** | SCR-007 — "Add to Google Calendar" + "Add to Outlook Calendar" buttons; inline sync status; token-expired banner |
| **UXR Requirements** | UXR-105 (sync status uses icon + label, not color alone; success = checkmark icon + text; error = warning icon + text) |
| **Design Tokens** | Success icon color, warning/error icon color (from project design token set) |

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
| Frontend | React + TypeScript | 18.x / 5.x | TR-001 — SCR-007 `BookingConfirmationPage`; per-provider sync button state (AC-001, AC-002) |
| HTTP Client | Fetch API (browser built-in) | Web platform | `POST /api/calendar/sync` and `GET /api/calendar/sync/status/{bookingId}` for token-expired signal (AC-001, AC-002; Edge) |
| Routing | React Router | v6 | SCR-007 rendered via booking confirmation route; booking details passed via route state (non-blocking render; Edge) |

---

## Task Overview

Add the calendar sync section to SCR-007 (`BookingConfirmationPage`). The booking confirmation details render synchronously from route state. Below the confirmation, a separate async calendar section renders "Add to Google Calendar" and "Add to Outlook Calendar" buttons. Each button independently calls `POST /api/calendar/sync`; the 202 response triggers a polling check for `TokenExpired` status via `GET /api/calendar/sync/status/{bookingId}`. Success shows a checkmark icon + label; `TokenExpired` shows a persistent `role="alert"` banner; a non-202 error shows a warning icon + retry link. All calendar feedback uses icon + text, never colour alone.

---

## Dependent Tasks
- task_001 (us_028) — `POST /api/calendar/sync` and `GET /api/calendar/sync/status/{bookingId}` endpoints must be available
- task_002 (us_020) — `BookingConfirmationDialog` (MOD-002) or its successor page (SCR-007) must exist as the host component

---

## Impacted Components
- `src/web/src/pages/BookingConfirmationPage.tsx` (SCR-007) — new or modified: add calendar sync section with per-provider buttons and status display
- `src/web/src/components/calendar/CalendarSyncSection.tsx` — new: async calendar sync UI section (buttons, status, token-expired banner)
- `src/web/src/api/calendarSyncApi.ts` — new: typed fetch wrappers for POST sync and GET sync status

---

## Implementation Plan
1. Create `calendarSyncApi.ts`: `triggerCalendarSync(bookingId: string, provider: "Google" | "Outlook"): Promise<void>` — calls `POST /api/calendar/sync`; throws `ApiError` on non-202; `getCalendarSyncStatus(bookingId: string, provider: string): Promise<"Synced" | "Failed" | "TokenExpired" | "Pending">` — calls `GET /api/calendar/sync/status/{bookingId}?provider={provider}` (AC-001, AC-002; Edge: token expired)
2. `BookingConfirmationPage` (SCR-007): reads booking details from React Router route state (or a single `GET /api/bookings/{id}` fetch); renders confirmation content synchronously — the `<CalendarSyncSection>` component is mounted after the main content without blocking it (Edge: SCR-007 loads before sync; AC-005)
3. `CalendarSyncSection` component: maintains independent state per provider — `googleStatus: "Idle" | "Loading" | "Synced" | "Failed" | "TokenExpired"` and `outlookStatus`; "Add to Google Calendar" and "Add to Outlook Calendar" buttons rendered side-by-side (AC-001, AC-002)
4. On button click: set provider status to `"Loading"`; call `triggerCalendarSync(bookingId, provider)`; on resolve (202), set status to `"Synced"` and show checkmark icon + "Added to [Provider] Calendar" in `<span role="status">`; on non-202 `ApiError`, set status to `"Failed"` (AC-001, AC-002; UXR-105)
5. Token-expired handling: after setting status to `"Synced"`, call `getCalendarSyncStatus(bookingId, provider)` once after a 3-second delay to check for `TokenExpired` signal; if returned, set status to `"TokenExpired"` and render `<div role="alert">` banner "Calendar sync unavailable. Please reconnect your calendar in Settings." — banner persists until dismissed (Edge: expired token; UXR-105)

---

## Current Project State
```
src/
└── web/
    └── src/
        ├── api/
        │   └── (calendarSyncApi.ts                  — CREATE)
        ├── components/
        │   └── calendar/
        │       └── (CalendarSyncSection.tsx          — CREATE)
        └── pages/
            └── (BookingConfirmationPage.tsx          — CREATE or MODIFY from MOD-002 post-booking flow)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | src/web/src/api/calendarSyncApi.ts | Typed fetch wrappers for POST sync and GET sync status |
| CREATE | src/web/src/components/calendar/CalendarSyncSection.tsx | Per-provider buttons, status icons, token-expired banner |
| CREATE/MODIFY | src/web/src/pages/BookingConfirmationPage.tsx | SCR-007: booking details + async CalendarSyncSection mount |

---

## External References
- .propel/context/wireframes/Hi-Fi/wireframe-SCR-007-booking-confirmation.html (SCR-007 HTML wireframe — calendar add buttons position, status label layout, token-expired banner placement)
- https://react.dev/reference/react-router/use-location (React Router useLocation — reading route state for booking details passed from the confirmation dialog; non-blocking SCR-007 render)
- https://www.w3.org/WAI/WCAG21/Understanding/status-messages.html (WCAG 2.1 — role="status" for success notifications, role="alert" for token-expired banner; UXR-105)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] Navigate to SCR-007 after booking; verify confirmation details render immediately without waiting for calendar buttons to load (Edge: non-blocking render; AC-005)
- [ ] Click "Add to Google Calendar"; verify loading state shown during in-flight request; on 202, verify checkmark icon + "Added to Google Calendar" label appears (AC-001; UXR-105)
- [ ] Click "Add to Outlook Calendar" independently; verify it does not depend on or wait for the Google sync state (AC-002)
- [ ] Simulate `GET /api/calendar/sync/status` returning `TokenExpired`; verify `<div role="alert">` banner "Calendar sync unavailable. Please reconnect your calendar in Settings." appears (Edge: expired token; UXR-105)
- [ ] Stub `POST /api/calendar/sync` to return 500; verify inline warning icon + error text displayed; "Add to [Provider] Calendar" button is re-enabled for retry; booking confirmation content is unaffected (AC-005; UXR-105)
- [ ] Verify all sync status indicators use both an icon and visible text — no state is communicated by colour alone (UXR-105; WCAG 2.1 A)

---

## Implementation Checklist
- [ ] `BookingConfirmationPage` renders the booking summary section (date, time, clinic, reference) from route state or an initial fetch before mounting `<CalendarSyncSection>` — the calendar section never delays the visibility of the confirmation details (Edge: non-blocking; AC-005)
- [ ] `CalendarSyncSection` maintains independent `googleStatus` and `outlookStatus` state; clicking one provider button does not affect the loading or result state of the other (AC-001, AC-002)
- [ ] Success feedback uses `<span role="status">` with a checkmark icon (`aria-hidden="true"`) + text "Added to [Provider] Calendar" — not colour alone (UXR-105; WCAG 2.1 A)
- [ ] Token-expired banner uses `<div role="alert">` with a warning icon (`aria-hidden="true"`) + full text "Calendar sync unavailable. Please reconnect your calendar in Settings." — rendered only when status is `"TokenExpired"` (Edge: expired token; UXR-105)
- [ ] On 5xx or network error from `POST /api/calendar/sync`, status is set to `"Failed"`; a warning icon + "Calendar sync failed. Please try again." is shown with the button re-enabled for retry; no redirect or booking state change occurs (AC-005; UXR-105)
