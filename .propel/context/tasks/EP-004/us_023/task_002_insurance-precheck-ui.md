# Task - TASK_002

## Requirement Reference
- **User Story:** us_023
- **Story Location:** .propel/context/tasks/EP-004/us_023/us_023.md
- **Acceptance Criteria:**
  - AC-002: When the pre-check returns `{"status": "Missing"}`, MOD-002 (Booking Confirmation Dialog) displays a soft non-blocking alert banner: "Insurance information is missing. You can still book, but please update it before your appointment." The "Confirm Booking" button remains enabled
  - AC-003: When the pre-check returns `{"status": "Complete"}`, no insurance alert is shown in MOD-002 — only the slot confirmation details are displayed
- **Edge Cases:**
  - Pre-check API unavailable (5xx or network error): the booking flow continues without any alert; no error message is surfaced in the UI; the dialog opens normally with "Confirm Booking" enabled
  - Incomplete vs Missing distinction: when status is `"Incomplete"`, the alert reads "Insurance information may be incomplete." — a distinct shorter message from the `"Missing"` variant

---

## Design References
| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | SCR-006 (Appointment Slot Calendar — insurance alert) |
| **Wireframe Status** | AVAILABLE |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | .propel/context/wireframes/Hi-Fi/wireframe-SCR-006-appointment-slot-calendar.html |
| **Screen Spec** | MOD-002 (Booking Confirmation Dialog) — soft alert banner rendered at top of dialog |
| **UXR Requirements** | UXR-604 (non-blocking soft alert — Confirm Booking always enabled); UXR-105 (alert uses warning icon + text, never color alone) |
| **Design Tokens** | Alert warning background, warning icon color (from project design token set) |

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
| Frontend | React + TypeScript | 18.x / 5.x | TR-001 — conditional alert banner state in MOD-002 (AC-002, AC-003) |
| HTTP Client | Fetch API (browser built-in) | Web platform | Existing API call pattern for pre-check before dialog opens (AC-001) |
| Routing | React Router | v6 | SCR-006 slot calendar component where the "Book this slot" click handler lives |

---

## Task Overview

Add the insurance pre-check call to the "Book this slot" click path in SCR-006 and conditionally render an alert banner inside MOD-002 (Booking Confirmation Dialog). The pre-check result is stored in component state as `insuranceStatus`. The dialog opens regardless of the result. The "Confirm Booking" button is never disabled by the pre-check. When the API call fails (5xx or network error), the dialog opens silently without any alert. The banner uses a warning icon alongside text to satisfy UXR-105 (no color-only feedback). The alert carries `role="alert"` for accessibility.

---

## Dependent Tasks
- task_001 (us_023) — `GET /api/insurance/pre-check` endpoint must return `{"status": "..."}` before the frontend call can be integrated
- task_002 (us_020) — `BookingConfirmationDialog` (MOD-002) must exist before the alert banner can be added to it

---

## Impacted Components
- `src/web/src/components/booking/BookingConfirmationDialog.tsx` — modified: add `insuranceStatus` prop and conditional alert banner
- `src/web/src/pages/AppointmentSlotCalendar.tsx` (SCR-006) — modified: call pre-check on "Book this slot" click; pass result to dialog
- `src/web/src/api/insuranceApi.ts` — new: typed fetch wrapper for `GET /api/insurance/pre-check`
- `src/web/src/components/booking/InsuranceAlertBanner.tsx` — new: reusable alert banner component (warning icon + text)

---

## Implementation Plan
1. Create `insuranceApi.ts`: `export type InsuranceStatus = "Complete" | "Incomplete" | "Missing"`. `export async function getInsurancePreCheck(patientId: string): Promise<InsuranceStatus | null>` — calls `GET /api/insurance/pre-check`; on non-ok response or any thrown error, returns `null` (Edge: API unavailable — null propagates the silent path) (AC-001)
2. In SCR-006 `AppointmentSlotCalendar`, on "Book this slot" button click: `setIsPreCheckLoading(true)`; `const status = await getInsurancePreCheck(patientId)`; `setInsuranceStatus(status)`; `setIsPreCheckLoading(false)`; `setDialogOpen(true)` — dialog always opens after the await resolves or rejects (AC-001, AC-002; Edge: API unavailable)
3. Pass `insuranceStatus: InsuranceStatus | null` as a prop to `BookingConfirmationDialog`; the prop type is optional with default `null` — the dialog does not depend on it for render correctness (AC-002, AC-003)
4. Create `InsuranceAlertBanner` component: accepts `status: "Missing" | "Incomplete"`; renders a `<div role="alert">` containing a warning SVG icon (`aria-hidden="true"`) and a `<span>` with the appropriate message; `"Missing"` → "Insurance information is missing. You can still book, but please update it before your appointment."; `"Incomplete"` → "Insurance information may be incomplete." (UXR-105 — icon + text; WCAG 2.1 A `role="alert"`; Edge: Incomplete vs Missing)
5. In `BookingConfirmationDialog`, at the top of the dialog body (before slot confirmation details), render `{(insuranceStatus === "Missing" || insuranceStatus === "Incomplete") && <InsuranceAlertBanner status={insuranceStatus} />}` (AC-002; AC-003 — no render when Complete or null)
6. When `insuranceStatus === "Complete"` or `insuranceStatus === null` (pre-check failed silently), the `InsuranceAlertBanner` is not rendered and no error state is displayed; "Confirm Booking" button is not conditioned on `insuranceStatus` — it remains enabled for all statuses (AC-003; Edge: API unavailable; UXR-604)
7. Loading state during pre-check: the "Book this slot" button shows a loading spinner (or is disabled with loading text) while `isPreCheckLoading === true`; this prevents duplicate dialog openings if the user double-clicks; the dialog does not open until the pre-check resolves (AC-001 — slot confirmation integrity; UXR-105)

---

## Current Project State
```
src/
└── web/
    └── src/
        ├── api/
        │   └── (insuranceApi.ts                      — CREATE)
        ├── components/
        │   └── booking/
        │       ├── BookingConfirmationDialog.tsx      (from us_020 — MODIFY: add insuranceStatus prop + banner)
        │       └── (InsuranceAlertBanner.tsx          — CREATE)
        └── pages/
            └── AppointmentSlotCalendar.tsx            (from us_019/us_020 — MODIFY: add pre-check call on "Book this slot" click)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | src/web/src/api/insuranceApi.ts | Typed fetch wrapper returning InsuranceStatus \| null |
| CREATE | src/web/src/components/booking/InsuranceAlertBanner.tsx | Warning icon + text banner with role="alert" |
| MODIFY | src/web/src/pages/AppointmentSlotCalendar.tsx | Pre-check call on "Book this slot"; pass status to dialog |
| MODIFY | src/web/src/components/booking/BookingConfirmationDialog.tsx | Add insuranceStatus prop; render InsuranceAlertBanner conditionally |

---

## External References
- .propel/context/wireframes/Hi-Fi/wireframe-SCR-006-appointment-slot-calendar.html (SCR-006 HTML wireframe — insurance alert position within the slot calendar and MOD-002 dialog)
- https://react.dev/reference/react-dom/components/dialog (React dialog + state patterns for controlled modal open/close)
- https://www.w3.org/WAI/WCAG21/Understanding/status-messages.html (WCAG 2.1 — role="alert" for dynamic status messages; UXR-105 compliance)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] Stub the API to return `{"status": "Missing"}`; click "Book this slot"; verify MOD-002 opens with the soft alert banner displaying the full "Insurance information is missing…" message and a warning icon (AC-002; UXR-105)
- [ ] Stub the API to return `{"status": "Incomplete"}`; verify MOD-002 shows "Insurance information may be incomplete." banner (Edge: Incomplete vs Missing; UXR-105)
- [ ] Stub the API to return `{"status": "Complete"}`; verify MOD-002 opens with no banner (AC-003)
- [ ] Stub the API to return a 500 error; verify the dialog still opens, no error message appears in the UI, and "Confirm Booking" remains enabled (Edge: API unavailable)
- [ ] In all four scenarios above, verify the "Confirm Booking" button is enabled before and after the pre-check resolves (UXR-604 — non-blocking)
- [ ] Verify the alert banner carries `role="alert"` and contains both a warning icon and a text message — no alert relies on colour alone to convey meaning (UXR-105; WCAG 2.1 A)
- [ ] Double-click "Book this slot" while the pre-check is in-flight; verify the dialog does not open twice (loading state guard)

---

## Implementation Checklist
- [ ] `getInsurancePreCheck` returns `null` (not throwing) on any non-ok HTTP response or network error; the caller in `AppointmentSlotCalendar` treats `null` as the silent skip path — dialog opens without an alert (Edge: API unavailable; AC-002 — booking never blocked by pre-check failure)
- [ ] `InsuranceAlertBanner` renders a `<div role="alert">` containing a warning icon (`aria-hidden="true"`) and a `<span>` with the message text; the alert conveys status via icon + text — not by colour alone (UXR-105; WCAG 2.1 A)
- [ ] `InsuranceAlertBanner` renders the exact message for `"Missing"`: "Insurance information is missing. You can still book, but please update it before your appointment." and the exact message for `"Incomplete"`: "Insurance information may be incomplete." — these strings must not be swapped (AC-002; Edge: Incomplete vs Missing)
- [ ] The `InsuranceAlertBanner` component is not rendered when `insuranceStatus === "Complete"` or `insuranceStatus === null`; no visual element indicating insurance status appears in the dialog for these states (AC-003)
- [ ] The "Confirm Booking" button in `BookingConfirmationDialog` does not have its `disabled` prop conditioned on `insuranceStatus`; it remains clickable for all pre-check outcomes including `"Missing"` and `"Incomplete"` (UXR-604 — non-blocking; AC-002)
- [ ] `isPreCheckLoading` state prevents the dialog from opening until `getInsurancePreCheck` resolves; the "Book this slot" button is in a loading or disabled state during the in-flight request (duplicate-open guard)
- [ ] `InsuranceStatus` is a TypeScript union type `"Complete" | "Incomplete" | "Missing"` — not a freeform string; the API response is narrowed at the boundary in `insuranceApi.ts` before being stored in component state (type safety; AC-001)
