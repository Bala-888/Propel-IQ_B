# Task - TASK_002

## Requirement Reference
- **User Story:** us_012
- **Story Location:** .propel/context/tasks/EP-001/us_012/us_012.md
- **Acceptance Criteria:**
  - AC-001: "Create account" toggle is off → walk-in booking is submitted without account creation; the patient appears in the queue (SCR-011) as "Walk-in — No account"
  - AC-002: "Create account" toggle is on → MOD-004 captures a patient email; on success, the booking is submitted with `createAccount: true` and the system creates a linked Patient account
  - AC-004: If the API returns 409, MOD-004 replaces the email input with the conflict message "An account with this email already exists. Would you like to link this walk-in to that account?" and Yes / No action buttons
- **Edge Cases:**
  - Empty email with toggle on: form prevents submission and displays inline error "Email is required to create an account" with icon + text (UXR-105)
  - Email delivery failure: if `credentialsEmailFailed: true` appears in the API response, a persistent warning banner "Credentials email could not be delivered — please provide credentials manually" is shown on the booking confirmation view

---

## Design References
| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | SCR-012 (Walk-in Booking Form), MOD-004 (Walk-in Account Creation) |
| **Wireframe Status** | AVAILABLE |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | .propel/context/wireframes/Hi-Fi/wireframe-SCR-012-walkin-booking-form.html |
| **Screen Spec** | SCR-012, MOD-004 |
| **UXR Requirements** | UXR-105 — all error states (empty email, duplicate email) must use icon + text; no colour-only error indicators |
| **Design Tokens** | Refer to project design system tokens for toggle switch, modal overlay, inline error text, and warning banner variants |

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
| Frontend | React | 18.x | TR-001 (SPA framework; toggle state, modal state, conditional rendering) |
| Frontend | TypeScript | 5.x | TR-001 (typed props for `WalkInBookingForm`, `WalkInAccountModal`, `CreateWalkInRequest`) |
| Frontend | Vite | 5.x | TR-001 (build tooling) |
| Frontend | React Router | v6 | TR-001 (navigation to confirmation view after booking success) |
| Frontend | react-hook-form + zod + @hookform/resolvers | latest stable | TR-001 (MOD-004 email validation; established pattern from us_008, us_009, us_011) |

---

## Task Overview

Extend the Walk-in Booking Form (SCR-012) with a "Create account" toggle and implement the Walk-in Account Creation modal (MOD-004). When the toggle is off, the form submits as before (AC-001). When on, MOD-004 captures an email, validates it client-side, and includes `createAccount: true` in the POST body. A 409 response converts the modal into a conflict-resolution view with Yes/No options (AC-004). A `credentialsEmailFailed: true` response flag surfaces as a persistent warning banner after booking confirmation (delivery-failure edge case). All error states use icon + text per UXR-105.

---

## Dependent Tasks
- task_001 (us_012) — `POST /walkins` with `createAccount`, `email`, and `linkExistingAccountId` fields must be implemented
- task_002 (us_009) — `AuthContext` with `accessToken` must be available for the `Authorization` header on API calls
- task_002 (us_008) — Walk-in Booking Form (SCR-012) base structure must exist; this task extends it with the toggle

---

## Impacted Components
- `src/web/src/features/walkin/WalkInBookingForm.tsx` — modified: add "Create account" toggle; conditionally render or open MOD-004 based on toggle state
- `src/web/src/features/walkin/WalkInAccountModal.tsx` — new component (MOD-004): email capture, conflict resolution view, and warning state
- `src/web/src/features/walkin/WalkInConfirmation.tsx` — modified: conditionally render `CredentialsEmailWarningBanner` when `credentialsEmailFailed: true`
- `src/web/src/api/walkInApi.ts` — new API client: `createWalkIn(body: CreateWalkInRequest): Promise<CreateWalkInResponse>`

---

## Implementation Plan
1. Create `walkInApi.ts` with `createWalkIn(body: CreateWalkInRequest): Promise<CreateWalkInResponse>` — calls `POST /api/walkins` with `Authorization: Bearer <accessToken>` from `AuthContext`; throws `DuplicateEmailError` on 409; returns the typed response including `credentialsEmailFailed` boolean (AC-001, AC-002, AC-004)
2. Add a controlled "Create account" toggle switch to `WalkInBookingForm.tsx` using `useState<boolean>(false)`; when `false`, the form submit handler calls `createWalkIn` with `createAccount: false` (AC-001); when `true`, the toggle either opens `WalkInAccountModal` or reveals an inline email section before submission (AC-002)
3. Build `WalkInAccountModal.tsx` (MOD-004) — `mode: 'email-capture' | 'conflict'` internal state; initial state is `'email-capture'` with an email text input registered with `react-hook-form` and a zod schema `z.string().email("Must be a valid email").min(1, "Email is required to create an account")`; "Save" triggers form validation before passing email to the parent (AC-002; Edge: empty email)
4. Inline error for empty email in MOD-004: react-hook-form `formState.errors.email` renders `<span role="alert">` containing a warning icon component + the message text "Email is required to create an account" — icon and text are always rendered together (Edge: empty email; UXR-105)
5. On `DuplicateEmailError` (409) caught from `createWalkIn`: set `WalkInAccountModal` mode to `'conflict'`; render the message "An account with this email already exists. Would you like to link this walk-in to that account?" with "Yes, link account" and "No, use different email" buttons — modal remains open (AC-004)
6. "Yes, link account" handler: re-call `createWalkIn` with `linkExistingAccountId` populated from the 409 response body; on success, close the modal and proceed to confirmation (AC-004 Yes path); "No, use different email" handler: reset mode to `'email-capture'` and clear the email field (AC-004 No path)
7. In `WalkInConfirmation.tsx`, after a successful booking: if `response.credentialsEmailFailed === true`, render a persistent `<div role="alert">` warning banner "Credentials email could not be delivered — please provide credentials manually" — it must not auto-dismiss (Edge: delivery failure; UXR-105)

---

## Current Project State
```
src/
└── web/
    └── src/
        ├── features/
        │   └── walkin/
        │       ├── WalkInBookingForm.tsx            (MODIFY — add toggle + MOD-004 integration)
        │       ├── WalkInAccountModal.tsx            (CREATE — MOD-004)
        │       └── WalkInConfirmation.tsx            (MODIFY — add CredentialsEmailWarningBanner)
        └── api/
            └── walkInApi.ts                         (CREATE)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | src/web/src/features/walkin/WalkInBookingForm.tsx | Add "Create account" toggle; wire toggle state to MOD-004 |
| CREATE | src/web/src/features/walkin/WalkInAccountModal.tsx | MOD-004: email capture + conflict resolution; react-hook-form + zod |
| MODIFY | src/web/src/features/walkin/WalkInConfirmation.tsx | Render persistent warning banner when credentialsEmailFailed = true |
| CREATE | src/web/src/api/walkInApi.ts | Typed API wrapper: createWalkIn with 409 → DuplicateEmailError |

---

## External References
- https://react-hook-form.com/get-started (react-hook-form — controlled validation for MOD-004 email field)
- https://zod.dev/ (Zod `.email()` and `.min(1)` validators for the email schema)
- https://www.w3.org/WAI/ARIA/apg/patterns/dialog-modal/ (WAI-ARIA dialog pattern — modal accessibility for MOD-004)
- https://developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/Roles/alert_role (ARIA alert role — for inline errors and warning banner per UXR-105)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] Toggle "Create account" off; submit the Walk-in Booking Form; verify the API is called with `createAccount: false` and the patient row in SCR-011 shows "Walk-in — No account" (AC-001)
- [ ] Toggle "Create account" on; enter a valid new email in MOD-004; submit; verify the API is called with `createAccount: true` and the modal closes on success (AC-002)
- [ ] Toggle on; leave the email field empty; attempt to submit MOD-004; verify the form does not submit and "Email is required to create an account" appears with an icon alongside the text — no API call is made (Edge: empty email; UXR-105)
- [ ] Toggle on; enter an email that already exists; submit; verify the modal transitions to the conflict view with the correct message and Yes/No buttons — the modal does not close (AC-004)
- [ ] Click "Yes, link account" in the conflict view; verify the booking is submitted with `linkExistingAccountId` and the modal closes on success (AC-004 Yes path)
- [ ] Click "No, use different email" in the conflict view; verify the modal resets to the email-capture view with an empty email field (AC-004 No path)
- [ ] Simulate `credentialsEmailFailed: true` in the API response; verify the persistent warning banner appears in `WalkInConfirmation` and does not disappear after a few seconds (Edge: delivery failure)

---

## Implementation Checklist
- [x] `WalkInAccountModal` toggle is a controlled boolean in `WalkInBookingForm` state — the modal's open/closed state is driven by the parent; the modal itself never controls its own visibility (AC-001, AC-002 — clear state ownership)
- [x] Zod schema for MOD-004 uses `.email()` for format validation AND `.min(1, "Email is required to create an account")` for presence — both rules produce distinct, user-readable messages surfaced via react-hook-form `errors.email.message` (Edge: empty email; AC-002; UXR-105)
- [x] `WalkInAccountModal` error messages render inside `<span role="alert">` with a named icon component + text string — colour is supplementary, never the sole error indicator (UXR-105; WCAG 1.4.1)
- [x] Conflict view in `WalkInAccountModal` is driven by internal `mode` state (`'email-capture'` vs `'conflict'`) — the component is not unmounted and re-mounted on 409, preserving form context for the No-path reset (AC-004; user experience continuity)
- [x] "Yes, link account" re-calls `createWalkIn` with `linkExistingAccountId`; the existing patient's ID is parsed from the 409 response body and held in component state — not re-fetched via a separate GET (AC-004 Yes path; avoids extra round-trip)
- [x] Warning banner in `WalkInConfirmation` uses `<div role="alert">` and does NOT use `setTimeout` or auto-dismiss logic — staff must manually acknowledge the delivery failure and provide credentials (Edge: delivery failure; OWASP A09 — audit-visible outcome)
- [x] `walkInApi.ts` uses the `Authorization: Bearer <accessToken>` header from `AuthContext`; a 401 response triggers the existing session-expiry flow (all ACs; OWASP A01)
