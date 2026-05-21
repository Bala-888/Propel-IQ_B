# Task - TASK_002

## Requirement Reference
- **User Story:** us_030
- **Story Location:** .propel/context/tasks/EP-006/us_030/us_030.md
- **Acceptance Criteria:**
  - AC-001: SCR-012 renders for Staff/Admin only at `/walkin/new`; displays Patient Search (typeahead), Reason for Visit (required), Slot Assignment (dropdown of today's Available slots), Priority (Normal/Urgent with icon + text)
  - AC-002: Typeahead fires `GET /patients/search?q=` at 3+ chars with 300ms debounce; selecting a result pre-fills patient ID and shows name + DOB confirmation row
  - AC-003: Submitting calls `POST /bookings/walkin`; on 201, navigate to SCR-011 queue with `queuePosition` toast
  - AC-004: "Create New Patient" option in typeahead opens MOD-004; on modal submit, new patientId pre-filled in form
- **Edge Cases:**
  - No same-day slots: Slot Assignment dropdown disabled with "No same-day slots available"; "Create Walk-in" button disabled; inline message "All same-day slots are full. Consider adding the patient to a wait list." with info icon
  - Duplicate booking today: API returns 409 `DuplicateBookingToday`; display `<div role="alert">` banner "This patient already has a confirmed booking today." with "Override and Proceed" CTA that re-submits with `overrideDuplicate: true`

---

## Design References
| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | SCR-012 (Walk-in Booking Form), MOD-004 (Walk-in Account Creation Modal) |
| **Wireframe Status** | AVAILABLE |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | .propel/context/wireframes/Hi-Fi/wireframe-SCR-012-walkin-booking-form.html |
| **Screen Spec** | SCR-012 — single-page form; MOD-004 — minimal patient creation modal |
| **UXR Requirements** | UXR-105 (Priority selector uses icon + text label "Normal" / "Urgent", not color alone; no-slots message uses info icon + text; duplicate banner uses warning icon + text) |
| **Design Tokens** | Priority Urgent accent, warning banner background, info message color (from project design token set) |

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
| Frontend | React + TypeScript | 18.x / 5.x | TR-001 — `WalkinBookingPage` (SCR-012) + `WalkinAccountCreationModal` (MOD-004); controlled form with per-field validation state (AC-001–004) |
| HTTP Client | Fetch API (browser built-in) | Web platform | `GET /patients/search`, `GET /slots?date=today`, `POST /bookings/walkin`, `POST /patients/walkin-create` (AC-002, AC-003, AC-004) |
| Routing | React Router | v6 | `/walkin/new` route with Staff/Admin role guard; `navigate` to SCR-011 on success (AC-001, AC-003) |

---

## Task Overview

Build the `WalkinBookingPage` (SCR-012) and `WalkinAccountCreationModal` (MOD-004). The page is Staff/Admin-only. The Patient Search typeahead uses debounced fetch with a 3-char minimum. The Slot Assignment dropdown loads today's available slots on mount. Priority selection uses icon + text labels for Normal and Urgent. On 201 from `POST /bookings/walkin`, the user is navigated to SCR-011 with a queue position toast. Duplicate and no-slots states are handled with accessible banners/messages.

---

## Dependent Tasks
- task_001 (us_030) — `GET /patients/search`, `POST /bookings/walkin`, `POST /patients/walkin-create` endpoints must be available
- task_001 (us_019) — `GET /slots?date=today&status=Available` endpoint must return today's available slots for the Slot Assignment dropdown

---

## Impacted Components
- `src/web/src/pages/WalkinBookingPage.tsx` (SCR-012) — new: single-page walk-in form with typeahead, slot dropdown, priority, duplicate banner
- `src/web/src/components/walkin/PatientTypeahead.tsx` — new: debounced typeahead with "Create New Patient" option
- `src/web/src/components/walkin/WalkinAccountCreationModal.tsx` (MOD-004) — new: minimal patient creation modal
- `src/web/src/api/walkinApi.ts` — new: typed fetch wrappers for search, walkin booking, and walkin patient create

---

## Implementation Plan
1. Create `walkinApi.ts`: `searchPatients(q: string): Promise<PatientSearchResult[]>` (GET); `createWalkinBooking(req: WalkinBookingRequest): Promise<WalkinBookingResponse>` (POST, handles 409 variants by throwing typed `ApiError`); `createWalkinPatient(req: WalkinPatientRequest): Promise<{patientId: string; firstName: string; lastName: string}>` (POST) (AC-002, AC-003, AC-004)
2. `WalkinBookingPage` (SCR-012): protected by Staff/Admin role guard on the route; fetches today's available slots via `GET /api/slots?date=today&status=Available` on mount; renders the four form sections as defined in AC-001 (AC-001; UXR-105)
3. `PatientTypeahead` component: internal `useEffect` debounced 300ms on `query` state; fires `searchPatients(query)` when `query.length >= 3`; renders a dropdown list of results + "Create New Patient" option at the bottom; selecting a result sets `selectedPatient` in parent state and shows a confirmation row with name + DOB (AC-002; OWASP A03 — minimum 3 chars before firing)
4. `WalkinAccountCreationModal` (MOD-004): opens when "Create New Patient" is clicked; fields: First Name (required), Last Name (required), Date of Birth (required), Phone Number (optional); submits via `createWalkinPatient`; on success, calls `onPatientCreated({ patientId, firstName, lastName })` prop and closes; parent sets `selectedPatient` from the callback (AC-004)
5. Slot Assignment dropdown: if `slots.length === 0` after fetch, render `<option disabled>No same-day slots available</option>` and disable the entire dropdown; show inline `<p role="status">` with an info icon + "All same-day slots are full. Consider adding the patient to a wait list." — disable "Create Walk-in" button when slots empty (Edge: no slots; UXR-105)
6. Form submit handler: if `duplicateBooking` state is set (from prior 409), add `overrideDuplicate: true` to the request; call `createWalkinBooking(request)`; on 201, navigate to SCR-011 with `state: { queuePosition: response.queuePosition, bookingId: response.bookingId }` and show toast (AC-003; Edge: duplicate override)
7. Duplicate banner: on `ApiError` with `error = "DuplicateBookingToday"` from `POST /bookings/walkin`, set `duplicateBooking = response.existingBookingId`; render `<div role="alert">` "This patient already has a confirmed booking today." with warning icon and "Override and Proceed" button; clicking Override calls submit again with `overrideDuplicate: true` (Edge: duplicate; UXR-105)

---

## Current Project State
```
src/
└── web/
    └── src/
        ├── api/
        │   └── (walkinApi.ts                         — CREATE)
        ├── components/
        │   └── walkin/
        │       ├── (PatientTypeahead.tsx              — CREATE)
        │       └── (WalkinAccountCreationModal.tsx    — CREATE)
        └── pages/
            └── (WalkinBookingPage.tsx                 — CREATE)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | src/web/src/api/walkinApi.ts | Typed fetch wrappers for search, walkin booking, walkin patient |
| CREATE | src/web/src/components/walkin/PatientTypeahead.tsx | Debounced typeahead with "Create New Patient" option |
| CREATE | src/web/src/components/walkin/WalkinAccountCreationModal.tsx | MOD-004 minimal patient creation modal |
| CREATE | src/web/src/pages/WalkinBookingPage.tsx | SCR-012 single-page walk-in form |

---

## External References
- .propel/context/wireframes/Hi-Fi/wireframe-SCR-012-walkin-booking-form.html (SCR-012 HTML wireframe — form field layout, Priority selector icons, duplicate banner position, no-slots message placement)
- https://www.w3.org/WAI/ARIA/apg/patterns/combobox/ (ARIA combobox pattern — accessible typeahead with `role="combobox"` + `role="listbox"`; AC-002 accessibility)
- https://react.dev/reference/react/useRef (React useRef — debounce timer cleanup in PatientTypeahead; prevents stale closure search firing)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] Navigate to `/walkin/new` as a Staff user; verify SCR-012 renders with all four form sections (AC-001)
- [ ] Type 2 chars in Patient Search; verify no API call fires; type 3 chars; verify dropdown appears with matching patients within 500ms (AC-002)
- [ ] Select a patient from typeahead; verify name + DOB confirmation row appears below the search field (AC-002)
- [ ] Click "Create New Patient"; verify MOD-004 opens; fill min fields; submit; verify new patientId pre-filled in form and modal closed (AC-004)
- [ ] Select patient + slot + reason, set Priority to Urgent; click "Create Walk-in"; verify 201 response and redirect to SCR-011 with queue position toast (AC-003)
- [ ] Stub `GET /slots?date=today&status=Available` to return empty array; verify dropdown shows disabled message and "Create Walk-in" button is disabled; verify info icon + text message displayed (Edge: no slots; UXR-105)
- [ ] Stub `POST /bookings/walkin` to return 409 `DuplicateBookingToday`; verify `<div role="alert">` banner with warning icon appears; click "Override and Proceed"; verify re-submission with `overrideDuplicate: true` (Edge: duplicate; UXR-105)
- [ ] Navigate to `/walkin/new` as a Patient role user; verify redirect to unauthorized page (OWASP A01)

---

## Implementation Checklist
- [x] `/walkin/new` route is guarded by a Staff/Admin role check in the React Router configuration; a Patient-role user is redirected to an unauthorised page before the component mounts (OWASP A01; AC-001)
- [x] `PatientTypeahead` does not fire `searchPatients` until `query.length >= 3`; the debounce timer is cleaned up in `useEffect` return to prevent stale requests firing after query is cleared (AC-002; OWASP A03)
- [x] Priority selection renders both an SVG icon and a visible text label ("Normal" / "Urgent") for each option — priority is not communicated by colour alone (UXR-105; WCAG 2.1 A)
- [x] No-slots state renders `<p role="status">` with an info icon + full message text; the "Create Walk-in" submit button has `disabled={slots.length === 0}` — the disabled state is not communicated by colour alone (Edge: no slots; UXR-105)
- [x] Duplicate booking banner uses `<div role="alert">` with a warning icon and full text; the "Override and Proceed" button sets `overrideDuplicate: true` in the next form submission — not a separate API call (Edge: duplicate; UXR-105; WCAG 2.1)
- [x] Form validation prevents submission when Patient is not selected, Reason for Visit is empty, or no Slot is chosen; each error is shown as icon + text below the field — not red border alone (UXR-105; OWASP A03 — client-side boundary validation)
- [x] `WalkinAccountCreationModal` (MOD-004) calls `createWalkinPatient` only on valid form submit (all required fields filled); on success the modal closes and passes the new patient object to `onPatientCreated` prop — the parent pre-fills `selectedPatient` without requiring the user to re-search (AC-004)
