# Task - TASK_002

## Requirement Reference
- **User Story:** us_024
- **Story Location:** .propel/context/tasks/EP-005/us_024/us_024.md
- **Acceptance Criteria:**
  - AC-001: When the patient clicks "Set as Preferred" on a slot in MOD-003, `POST /bookings/{bookingId}/preferred-slot` is called; on 201 response, inline confirmation "Preferred slot registered" with a checkmark icon is shown
  - AC-003: The currently booked slot is not included in the MOD-003 available list; the UI must never allow the patient to select their active slot as preferred
- **Edge Cases:**
  - Slot unavailable before POST: if the API returns 409 `"The selected slot is no longer available."`, display an inline error and re-fetch the slot list
  - MOD-003 re-open refresh: the slot list is re-fetched on every modal open via `useEffect` with the modal's open state as a dependency

---

## Design References
| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | MOD-003 (Preferred Slot Selection Modal) |
| **Wireframe Status** | AVAILABLE |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | .propel/context/wireframes/Hi-Fi/wireframe-MOD-003-preferred-slot-selection.html |
| **Screen Spec** | MOD-003 — slot list (excluding current booking slot), "Set as Preferred" CTA, inline success/error states |
| **UXR Requirements** | UXR-105 (preferred status indicator uses checkmark icon + text label, not color alone; error states use icon + text) |
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
| Frontend | React + TypeScript | 18.x / 5.x | TR-001 — MOD-003 modal component; controlled slot selection state (AC-001, AC-003) |
| HTTP Client | Fetch API (browser built-in) | Web platform | POST preferred-slot and GET available slots (AC-001; Edge: re-fetch after 409) |
| Routing | React Router | v6 | SCR-006 (AppointmentSlotCalendar) hosts the MOD-003 trigger after booking confirmation |

---

## Task Overview

Build the `PreferredSlotSelectionModal` (MOD-003) component and wire it into SCR-006 (AppointmentSlotCalendar). The modal fetches available slots on open, filters out the patient's currently booked slot, and lets the patient select one to designate as preferred. "Set as Preferred" calls the backend; success shows an inline checkmark + label; a 409 "slot no longer available" response re-fetches the list. All status feedback uses icon + text, never colour alone. The modal slot list re-fetches on every open event to reflect the latest availability.

---

## Dependent Tasks
- task_001 (us_024) — `POST /api/bookings/{bookingId}/preferred-slot` endpoint must be available before this component can call it
- task_002 (us_020) — `BookingConfirmationDialog` (MOD-002) is the trigger point from which MOD-003 is opened after a successful booking

---

## Impacted Components
- `src/web/src/components/booking/PreferredSlotSelectionModal.tsx` — new: MOD-003 modal with slot list, selection, and success/error states
- `src/web/src/api/preferredSlotApi.ts` — new: typed fetch wrappers for GET available slots (filtered) and POST preferred-slot
- `src/web/src/pages/AppointmentSlotCalendar.tsx` (SCR-006) — modified: add "Choose Preferred Slot" button after booking confirmation, opening MOD-003
- `src/web/src/components/booking/BookingConfirmationDialog.tsx` (MOD-002) — modified: surface "Choose a Preferred Slot" link/button to open MOD-003 post-booking

---

## Implementation Plan
1. Create `preferredSlotApi.ts`: `setPreferredSlot(bookingId: string, slotId: string): Promise<{preferredSlotId: string; status: string}>` — calls `POST /api/bookings/{bookingId}/preferred-slot`; returns the typed response on 201; throws a typed `ApiError` carrying the HTTP status and error body on non-201 responses (AC-001; Edge: 409 handling)
2. Create `PreferredSlotSelectionModal` component: accepts props `bookingId: string`, `currentSlotId: string`, `isOpen: boolean`, `onClose: () => void`; fetches available slots via `GET /api/slots?available=true` on each open using `useEffect([isOpen])`; filters out any slot whose `id === currentSlotId` before rendering the list (AC-003 — current slot excluded; Edge: re-open refresh)
3. Slot list renders each slot item with date/time text and a "Set as Preferred" button; only one slot can be selected at a time (radio-button semantics); the selected slot is tracked in `selectedSlotId` state (AC-001; UXR-105)
4. "Set as Preferred" button click handler: set `isSubmitting = true`; call `setPreferredSlot(bookingId, selectedSlotId)`; on success, set `confirmationSlotId = selectedSlotId` and show inline `<span role="status">` with checkmark icon + "Preferred slot registered" text; on `ApiError` with status 409 body `"The selected slot is no longer available."`, display inline `<span role="alert">` with warning icon + "This slot is no longer available. Please select another." and re-fetch the slot list (AC-001; Edge: slot unavailable; UXR-105)
5. Handle 409 body `"Preferred slot selection is only available for active confirmed bookings."`: display error message in `<span role="alert">` and close the modal after 2 seconds (AC-004)
6. Handle 400 body `"Preferred slot cannot be the same as the active booking."`: display defensive inline error; this case should not occur if the filter is correct but is handled for API contract safety (AC-003 — defensive; UXR-105)

---

## Current Project State
```
src/
└── web/
    └── src/
        ├── api/
        │   └── (preferredSlotApi.ts                    — CREATE)
        ├── components/
        │   └── booking/
        │       ├── BookingConfirmationDialog.tsx        (from us_020/us_023 — MODIFY: add "Choose Preferred Slot" trigger)
        │       └── (PreferredSlotSelectionModal.tsx     — CREATE)
        └── pages/
            └── AppointmentSlotCalendar.tsx              (from us_019/us_023 — MODIFY: open MOD-003 state after booking)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | src/web/src/api/preferredSlotApi.ts | Typed fetch wrappers for POST preferred-slot |
| CREATE | src/web/src/components/booking/PreferredSlotSelectionModal.tsx | MOD-003 with slot list, selection, success/error states |
| MODIFY | src/web/src/pages/AppointmentSlotCalendar.tsx | Add MOD-003 open/close state; "Choose Preferred Slot" button after booking |
| MODIFY | src/web/src/components/booking/BookingConfirmationDialog.tsx | Add "Choose a Preferred Slot" link that triggers MOD-003 |

---

## External References
- .propel/context/wireframes/Hi-Fi/wireframe-MOD-003-preferred-slot-selection.html (MOD-003 HTML wireframe — slot list layout, "Set as Preferred" CTA, success/error state positions)
- https://react.dev/reference/react/useEffect (useEffect dependency array — re-fetching slot list on `isOpen` change)
- https://www.w3.org/WAI/WCAG21/Understanding/status-messages.html (WCAG 2.1 — role="status" for success, role="alert" for errors; UXR-105 compliance)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] Open MOD-003 for a confirmed booking; verify the slot list does not contain the booking's currently active slot (AC-003)
- [ ] Select an available slot and click "Set as Preferred"; verify inline "Preferred slot registered" with a checkmark icon appears and no error is shown (AC-001; UXR-105)
- [ ] Stub the API to return 409 `"The selected slot is no longer available."`; verify the inline error "This slot is no longer available. Please select another." is shown with a warning icon, and the slot list re-fetches (Edge: slot unavailable; UXR-105)
- [ ] Close MOD-003 and re-open it; verify `useEffect` triggers a fresh slot list fetch (Edge: re-open refresh)
- [ ] Stub the API to return 409 `"Preferred slot selection is only available for active confirmed bookings."`; verify the error message is shown and the modal closes (AC-004)
- [ ] Verify all status messages use `role="status"` (success) or `role="alert"` (error) and contain both an icon and visible text — no feedback relies on colour alone (UXR-105; WCAG 2.1 A)

---

## Implementation Checklist
- [ ] `PreferredSlotSelectionModal` uses `useEffect([isOpen])` to re-fetch available slots every time the modal opens; `currentSlotId` is filtered out client-side before the list is rendered — the current booking slot never appears as a selectable option (AC-003; Edge: re-open refresh)
- [ ] `setPreferredSlot` in `preferredSlotApi.ts` returns a typed response on 201 and throws a typed `ApiError` (with `status` and `message` fields) on non-201 responses; the component catches `ApiError` and routes to the correct inline feedback based on `status` (409 vs 400) (AC-001; Edge: 409 typed handling)
- [ ] Success feedback uses `<span role="status">` containing a checkmark icon (`aria-hidden="true"`) + "Preferred slot registered" text — not colour alone (UXR-105; WCAG 2.1 A)
- [ ] Error feedback uses `<span role="alert">` containing a warning icon (`aria-hidden="true"`) + error text — not colour alone; 409 "slot unavailable" error additionally triggers a slot list re-fetch (Edge: slot unavailable; UXR-105)
- [ ] `isSubmitting` state prevents duplicate "Set as Preferred" submissions while the POST is in-flight; the button is disabled with a loading indicator when `isSubmitting === true` (duplicate submission guard)
- [ ] The "Set as Preferred" button is not rendered for a slot that matches `confirmationSlotId` (the already-registered preferred slot) — once a slot is registered, the UI reflects the current preferred selection visually without allowing re-submission of the same slot (AC-001 — idempotency UX)
