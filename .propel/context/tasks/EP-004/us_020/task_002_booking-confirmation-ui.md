# Task - TASK_002

## Requirement Reference
- **User Story:** us_020
- **Story Location:** .propel/context/tasks/EP-004/us_020/us_020.md
- **Acceptance Criteria:**
  - AC-001: When the patient confirms the booking in MOD-002, `POST /bookings` is called; on HTTP 201 the patient is navigated to a booking confirmation screen showing `bookingId` and slot details
  - AC-002: On HTTP 409 "slot no longer available", the MOD-002 dialog closes and SCR-006 renders 3 alternative Available slots inline with the error message "This slot is no longer available. Here are some alternatives:" using icon + text
  - AC-004: On HTTP 409 "duplicate time window", the MOD-002 dialog closes and an error message "You already have an active booking for this time window." is shown with icon + text and a "View my bookings" link
- **Edge Cases:**
  - Slot Blocked by admin (409 "slot has been blocked by the clinic"): the dialog closes and an inline `<span role="alert">` message appears with icon + text — no alternatives section is rendered
  - Lock timeout (503): the dialog closes and an inline `<span role="alert">` "Booking could not be processed. Please try again." appears with a "Try again" button that reopens the dialog with the same slot pre-selected

---

## Design References
| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | SCR-006 (Appointment Slot Calendar — conflict states), MOD-002 (Booking Confirmation Dialog) |
| **Wireframe Status** | AVAILABLE |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | .propel/context/wireframes/Hi-Fi/wireframe-SCR-006-appointment-slot-calendar.html |
| **Screen Spec** | SCR-006, MOD-002 |
| **UXR Requirements** | UXR-602 — 409 concurrent conflict displays exactly 3 alternative Available slots inline; UXR-105 — all conflict and error states use icon + text, never colour alone |
| **Design Tokens** | Refer to project design system tokens for dialog overlay, conflict alert card, alternative slot list item, and retry button |

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
| Frontend | React | 18.x | TR-001 (MOD-002 dialog state; booking error discriminated state; alternatives list render) |
| Frontend | TypeScript | 5.x | TR-001 (typed BookingCreatedResponse, BookingConflictError discriminated union) |
| Frontend | Vite | 5.x | TR-001 (build tooling) |
| Frontend | React Router | v6 | TR-001 (`useNavigate` — post-201 navigation to confirmation screen; "View my bookings" link) |

---

## Task Overview

Build the MOD-002 Booking Confirmation Dialog and the full booking response handling layer in SCR-006. The dialog renders selected slot details and a "Confirm Booking" button that calls `POST /bookings`. On 201, the patient navigates to the confirmation screen. On 409 (concurrent slot conflict), the dialog closes and 3 alternative slot cells render inline (UXR-602). On 409 (blocked) and 409 (duplicate window), the dialog closes and a distinct `role="alert"` message renders. On 503, the dialog closes and a "Try again" button reopens it. All error states use icon + text per UXR-105.

---

## Dependent Tasks
- task_001 (us_020) — `POST /bookings` endpoint must return 201, 409 (with alternatives or without), and 503 response shapes
- task_002 (us_019) — `SlotCalendar.tsx`, `SlotCell.tsx`, and `slotsApi.ts` must exist; `selectedSlotId` state in `SlotCalendar` is the entry point for the booking flow

---

## Impacted Components
- `src/web/src/features/bookings/BookingConfirmDialog.tsx` — new: MOD-002 confirmation dialog with slot summary and confirm/cancel actions
- `src/web/src/features/bookings/BookingAlternatives.tsx` — new: renders 3 alternative SlotCell items after a concurrent 409 conflict
- `src/web/src/features/slots/SlotCalendar.tsx` — modified: open BookingConfirmDialog on "Book this slot" click; handle all booking response branches; render conflict/error states
- `src/web/src/api/bookingsApi.ts` — new: `createBooking({slotId}): Promise<BookingCreatedResponse>` with typed error shapes

---

## Implementation Plan
1. Create `bookingsApi.ts`: `createBooking({slotId: string}): Promise<BookingCreatedResponse>`; `BookingCreatedResponse = {bookingId: string, status: string, slot: SlotDto}`; define `BookingConflictError` as a discriminated union `{errorCode: 'SLOT_UNAVAILABLE', message: string, alternatives: SlotDto[]} | {errorCode: 'SLOT_BLOCKED', message: string} | {errorCode: 'DUPLICATE_WINDOW', message: string} | {errorCode: 'LOCK_TIMEOUT', message: string}`; parse HTTP 409 body's `error` string to set `errorCode`; parse `alternatives` field when present (AC-001, AC-002, AC-004; Edge types)
2. Build `BookingConfirmDialog.tsx` (MOD-002): rendered as a `<dialog>` element with `aria-modal="true"` and `aria-labelledby` pointing to the title; accepts `slot: SlotDto` and `onConfirm: () => Promise<void>` and `onClose: () => void` props; renders slot date, time, and duration; "Confirm Booking" `<button>` calls `onConfirm()` with `isSubmitting` guard (disabled + spinner icon during call); "Cancel" `<button>` calls `onClose()` (MOD-002; AC-001; WCAG 2.5.5; WCAG 1.3.1)
3. In `SlotCalendar.tsx`, replace the "Book this slot" `<button>`'s `onClick` with `setIsDialogOpen(true)`; add `useState<BookingConflictError | null>(null)` for `bookingError`; add `useState<boolean>(false)` for `isDialogOpen`; the `onConfirm` handler calls `createBooking({slotId: selectedSlotId!})` then dispatches on result (AC-001, AC-002, AC-004)
4. On 201 from `createBooking`: call `setIsDialogOpen(false)`; call `navigate('/booking/confirmation', { state: { bookingId: res.bookingId, slot: res.slot } })` (AC-001)
5. On `BookingConflictError` with `errorCode = 'SLOT_UNAVAILABLE'`: call `setIsDialogOpen(false)`; call `setBookingError(err)`; render `<BookingAlternatives error={bookingError} onSelect={handleAlternativeSelect} />` in `SlotCalendar` — the section appears between the error alert and the slot grid (AC-002; UXR-602)
6. Build `BookingAlternatives.tsx`: renders `<section aria-label="Alternative slots">`; inside renders `<div role="alert">` containing a named icon component + text `"This slot is no longer available. Here are some alternatives:"`; then renders exactly 3 `<SlotCell>` components from `error.alternatives`; clicking an alternative calls `onSelect(slot.id)` which updates `selectedSlotId` in the parent and clears `bookingError` (AC-002; UXR-602; UXR-105; WCAG 4.1.3)
7. On `errorCode = 'SLOT_BLOCKED'`: `setIsDialogOpen(false)`; render `<span role="alert">` with icon + text "This slot has been blocked by the clinic." in `SlotCalendar` — no `<BookingAlternatives>` section (Edge: slot Blocked; UXR-105; WCAG 4.1.3)
8. On `errorCode = 'DUPLICATE_WINDOW'`: render `<span role="alert">` with icon + text "You already have an active booking for this time window." and a React Router `<Link>` "View my bookings"; on `errorCode = 'LOCK_TIMEOUT'`: render `<span role="alert">` with icon + text "Booking could not be processed. Please try again." and a "Try again" `<button>` that calls `setIsDialogOpen(true)` to reopen MOD-002 with `selectedSlotId` still set (AC-004; Edge: lock timeout; UXR-105; WCAG 4.1.3)

---

## Current Project State
```
src/
└── web/
    └── src/
        ├── features/
        │   ├── bookings/
        │   │   └── (BookingConfirmDialog.tsx    — CREATE)
        │   │   └── (BookingAlternatives.tsx     — CREATE)
        │   └── slots/
        │       └── SlotCalendar.tsx             (from us_019 — MODIFY: dialog state, booking handler, error branches)
        └── api/
            ├── slotsApi.ts                      (from us_019 — do not modify)
            └── (bookingsApi.ts                  — CREATE)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | src/web/src/api/bookingsApi.ts | createBooking typed wrapper with discriminated conflict error |
| CREATE | src/web/src/features/bookings/BookingConfirmDialog.tsx | MOD-002 confirmation dialog |
| CREATE | src/web/src/features/bookings/BookingAlternatives.tsx | 3 alternative slots on 409 concurrent conflict |
| MODIFY | src/web/src/features/slots/SlotCalendar.tsx | Open dialog on "Book this slot"; handle all response branches |

---

## External References
- https://developer.mozilla.org/en-US/docs/Web/HTML/Element/dialog (HTML `<dialog>` element — modal for MOD-002; `aria-modal` and `aria-labelledby` attributes)
- https://reactrouter.com/en/main/hooks/use-navigate (React Router v6 useNavigate with state — post-201 redirect to confirmation screen)
- https://www.w3.org/WAI/ARIA/apg/patterns/dialog-modal/ (WAI-ARIA modal dialog pattern — focus trap and keyboard escape handling for MOD-002)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] Click "Book this slot" on SCR-006; verify MOD-002 dialog opens showing correct slot date, time, and duration; Tab key navigates between "Confirm Booking" and "Cancel"; Escape closes the dialog (MOD-002; WCAG modal pattern)
- [ ] Click "Confirm Booking" in MOD-002 for an Available slot; verify `POST /api/bookings` is called with the correct `slotId`; on 201, verify navigation to the confirmation screen with `bookingId` and slot details in route state (AC-001)
- [ ] Simulate 409 `SLOT_UNAVAILABLE` response with 3 alternatives; verify MOD-002 closes, `<BookingAlternatives>` section renders with icon + text error and 3 slot cells; clicking an alternative pre-selects it and clears the error (AC-002; UXR-602; UXR-105)
- [ ] Simulate 409 `SLOT_BLOCKED`; verify MOD-002 closes, `<span role="alert">` "This slot has been blocked by the clinic." appears with icon + text; no alternatives section rendered (Edge: slot Blocked; UXR-105)
- [ ] Simulate 409 `DUPLICATE_WINDOW`; verify `<span role="alert">` "You already have an active booking for this time window." with icon + text and "View my bookings" link (AC-004; UXR-105)
- [ ] Simulate 503; verify `<span role="alert">` "Booking could not be processed. Please try again." with icon + text and "Try again" button; clicking "Try again" reopens MOD-002 with the same slot still selected (Edge: lock timeout; UXR-105)
- [ ] Verify all error and advisory states use a named icon component + visible text — no state communicated by colour alone (UXR-105; WCAG 1.4.1)

---

## Implementation Checklist
- [ ] `BookingConfirmDialog` renders as `<dialog aria-modal="true" aria-labelledby="dialog-title">`; focus is trapped inside the dialog while open; Escape key closes it; "Cancel" button calls `onClose()`; all interactive elements have `aria-label` or visible label text (MOD-002; WCAG 2.5.5; WCAG 1.3.1; WAI-ARIA modal pattern)
- [ ] `bookingsApi.ts` parses the 409 response body's `error` string to set `errorCode`; `SLOT_UNAVAILABLE` maps to responses containing `alternatives`; `SLOT_BLOCKED`, `DUPLICATE_WINDOW`, and `LOCK_TIMEOUT` are distinct codes — no fall-through between branches (AC-002, AC-004; Edge: Blocked, timeout; type-safe error handling)
- [ ] On 201, `navigate('/booking/confirmation', { state: { bookingId, slot } })` is called; the `selectedSlotId` state in `SlotCalendar` is cleared after successful navigation so a back-navigation does not re-trigger the booking flow (AC-001)
- [ ] `BookingAlternatives` renders exactly 3 `<SlotCell>` components from `error.alternatives`; the `<div role="alert">` precedes the alternative slots list and contains a named icon component + visible text message — never colour-only (AC-002; UXR-602; UXR-105; WCAG 1.4.1; WCAG 4.1.3)
- [ ] `SLOT_BLOCKED` error renders `<span role="alert">` with icon + text; no `<BookingAlternatives>` section is rendered in this branch — the two 409 branches produce visually and semantically distinct UI states (Edge: slot Blocked; UXR-105; WCAG 4.1.3)
- [ ] `LOCK_TIMEOUT` (503) "Try again" button sets `isDialogOpen(true)` with `selectedSlotId` still intact in `SlotCalendar` state; it does NOT re-fetch the slot list — the patient resumes from the same slot selection without losing their choice (Edge: lock timeout)
- [ ] `BookingConfirmDialog` "Confirm Booking" button sets `isSubmitting = true` (disabled + spinner icon replaces button icon) for the duration of the `createBooking` call; on resolution (success or error), `isSubmitting = false`; disabled state uses a visual indicator beyond colour (AC-001; UXR-105; WCAG 1.4.1)
