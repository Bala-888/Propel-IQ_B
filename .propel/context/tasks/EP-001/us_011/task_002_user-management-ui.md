# Task - TASK_002

## Requirement Reference
- **User Story:** us_011
- **Story Location:** .propel/context/tasks/EP-001/us_011/us_011.md
- **Acceptance Criteria:**
  - AC-001: Admin clicks "New User", fills MOD-006 form (name, email, role = Staff), submits → user appears in table; welcome email is sent (backend)
  - AC-002: Admin opens MOD-006 for an existing user, changes role, saves → role column updates immediately in the table without a full page reload
  - AC-003: Admin clicks "Deactivate" in a table row, confirms the dialog → row status changes to inactive; deactivated user can no longer log in
  - AC-004: The "Deactivate" button for the logged-in admin's own row is `aria-disabled="true"`; clicking it shows an error banner "You cannot deactivate your own account" — no API call is made
  - AC-005: Submitting MOD-006 with a duplicate email displays the inline error "A user with this email already exists" without closing the modal
- **Edge Cases:**
  - Reactivating a deactivated user: the "Activate" action (isActive = true) must also be available from the table row — same confirmation pattern as deactivation
  - UXR-105: active/inactive status in the table must be conveyed with an icon AND a text label — not colour alone

---

## Design References
| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | SCR-017 (Admin User Management), MOD-006 (Create/Edit User Modal) |
| **Wireframe Status** | AVAILABLE |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | .propel/context/wireframes/Hi-Fi/wireframe-SCR-017-admin-user-management.html |
| **Screen Spec** | SCR-017, MOD-006 |
| **UXR Requirements** | UXR-105 — active/inactive status column must use icon + text label; no colour-only status indicator |
| **Design Tokens** | Refer to project design system tokens for table row striping, modal overlay, status badge (icon + label variants), button hierarchy in MOD-006 (primary = "Save", secondary = "Cancel") |

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
| Frontend | React | 18.x | TR-001 (SPA framework; component state for table, modal, and dialog) |
| Frontend | TypeScript | 5.x | TR-001 (typed props for `UserRow`, `UserModal`, `DeactivateDialog` components) |
| Frontend | Vite | 5.x | TR-001 (build tooling) |
| Frontend | React Router | v6 | TR-001 (`useNavigate` already in AuthContext; admin route guard at `/admin/users`) |
| Frontend | react-hook-form + zod + @hookform/resolvers | latest stable | TR-001 (MOD-006 form validation; established pattern from us_008 and us_009) |

---

## Task Overview

Build the User Management screen (SCR-017) and the Create/Edit User modal (MOD-006) in the admin section. SCR-017 renders a paginated table of all users with name, email, role, and status columns (status: icon + text per UXR-105). Each row has an Edit and a Deactivate/Activate action. MOD-006 serves both create and edit modes via a shared form component, validated with react-hook-form + zod. AC-004 guards the admin's own row client-side. API 409 responses surface as inline errors inside MOD-006 without closing it.

---

## Dependent Tasks
- task_001 (us_011) — `POST /admin/users` and `PATCH /admin/users/{id}` endpoints must exist
- task_002 (us_009) — `AuthContext` must expose the logged-in user's `id` and `role` for the AC-004 own-account guard
- task_002 (us_008) — Admin route guard must redirect non-admin users away from `/admin/*`

---

## Impacted Components
- `src/web/src/features/admin/UserManagementPage.tsx` — new page component (SCR-017); mounts at `/admin/users`
- `src/web/src/features/admin/UserModal.tsx` — new modal component (MOD-006); handles create + edit modes
- `src/web/src/features/admin/DeactivateDialog.tsx` — new confirmation dialog component
- `src/web/src/features/admin/UserStatusBadge.tsx` — new status badge component with icon + text (UXR-105)
- `src/web/src/api/adminUsersApi.ts` — new API client: `getUsers`, `createUser`, `patchUser`

---

## Implementation Plan
1. Create `adminUsersApi.ts` with three typed fetch wrappers: `getUsers(): Promise<User[]>` (GET /admin/users), `createUser(body: CreateUserRequest): Promise<User>` (POST /admin/users — throws `DuplicateEmailError` on 409), `patchUser(id: string, body: PatchUserRequest): Promise<User>` (PATCH /admin/users/<id>); all calls include the `Authorization: Bearer <accessToken>` header from `AuthContext` (AC-001, AC-002, AC-003)
2. Create `UserStatusBadge.tsx` rendering the user's active/inactive status as `<span>` containing a named icon component (e.g., `CheckCircleIcon` / `XCircleIcon`) and the text "Active" or "Inactive" — never uses colour as the sole differentiator (UXR-105; AC-003)
3. Build `UserManagementPage.tsx` (SCR-017): fetch users on mount via `getUsers`; render a `<table>` with columns Name, Email, Role, Status (via `UserStatusBadge`), Actions; the Actions column contains "Edit" (opens MOD-006 in edit mode) and "Deactivate" / "Activate" buttons; detect the logged-in admin's own row by comparing `user.id === authContext.userId` — render the Deactivate button with `aria-disabled="true"` and attach a click handler that shows the error banner without calling the API (AC-003, AC-004; UXR-105)
4. Build `UserModal.tsx` (MOD-006) for create and edit modes via a `mode: 'create' | 'edit'` prop: form fields Name (text), Email (email), Role (select: Patient | Staff | Admin); use `react-hook-form` with a `zod` schema that validates all three fields as required and email as valid format; in edit mode, pre-populate the form with the existing user's values (AC-001, AC-002)
5. In `UserModal.tsx` submit handler: call `createUser` or `patchUser` based on mode; on success, call `onSuccess(updatedUser)` callback and close the modal; on 409 (`DuplicateEmailError`), set a `formError` state and render the inline message "A user with this email already exists" inside the modal form without calling `onClose` — the modal remains open (AC-005)
6. Build `DeactivateDialog.tsx`: confirmation dialog with message "Deactivate [Name]? This user will lose access immediately." and "Confirm" / "Cancel" buttons; on confirm, call `patchUser(id, { isActive: false })`; on success, call `onSuccess` to update the table row; the same component is reused for reactivation with adjusted copy (AC-003; Edge: reactivation)
7. Wire `UserManagementPage.tsx` state: `users` array; update the array slice on `onSuccess` from the modal or dialog (optimistic update) — no full re-fetch required; this ensures AC-002 (role change visible immediately) and AC-003 (status change visible immediately) without a network round-trip

---

## Current Project State
```
src/
└── web/
    └── src/
        ├── features/
        │   └── admin/
        │       ├── UserManagementPage.tsx           (CREATE — SCR-017)
        │       ├── UserModal.tsx                    (CREATE — MOD-006)
        │       ├── DeactivateDialog.tsx              (CREATE)
        │       └── UserStatusBadge.tsx              (CREATE — UXR-105)
        └── api/
            └── adminUsersApi.ts                     (CREATE)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | src/web/src/features/admin/UserManagementPage.tsx | SCR-017: user table, own-account guard, row actions |
| CREATE | src/web/src/features/admin/UserModal.tsx | MOD-006: create + edit form with react-hook-form + zod; 409 inline error |
| CREATE | src/web/src/features/admin/DeactivateDialog.tsx | Confirmation dialog for deactivation and reactivation |
| CREATE | src/web/src/features/admin/UserStatusBadge.tsx | Icon + text status badge (UXR-105 compliant) |
| CREATE | src/web/src/api/adminUsersApi.ts | Typed API wrappers: getUsers, createUser, patchUser |

---

## External References
- https://react-hook-form.com/get-started (react-hook-form — form state management and validation)
- https://zod.dev/ (Zod schema validation — email, required field, and enum validation)
- https://www.w3.org/WAI/WCAG21/Techniques/aria/ARIA14 (WAI-ARIA — aria-disabled attribute for AC-004 own-account guard)
- https://reactrouter.com/en/main/route/route (React Router v6 — route registration for /admin/users)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] Authenticated as Admin, navigate to `/admin/users`; verify the users table renders with Name, Email, Role, Status, and Actions columns; status column shows icon + text (not colour only) for both active and inactive users (UXR-105; AC-003)
- [ ] Click "New User"; fill in MOD-006 with valid data and role = Staff; submit; verify the modal closes and the new user row appears in the table (AC-001)
- [ ] Open MOD-006 for an existing Patient user; change role to Staff; save; verify the role column updates immediately in the table without a page reload (AC-002)
- [ ] Enter a duplicate email in MOD-006 create mode; submit; verify the modal remains open and "A user with this email already exists" appears inline in the form (AC-005)
- [ ] Click "Deactivate" on another user's row; confirm the dialog; verify the status badge changes to "Inactive" immediately (AC-003)
- [ ] Inspect the logged-in admin's own row; verify the Deactivate button has `aria-disabled="true"`; click it; verify the error banner "You cannot deactivate your own account" appears and no network request to PATCH is made (AC-004)
- [ ] Activate a deactivated user row; verify the status badge changes to "Active" immediately (Edge: reactivation)

---

## Implementation Checklist
- [ ] `UserStatusBadge` renders both a named icon and a text string for Active and Inactive states — colour is supplementary, not the sole differentiator; satisfies WCAG 1.4.1 non-text contrast (UXR-105; AC-003)
- [ ] `UserModal.tsx` zod schema validates `email` with `.email()` and `role` with `.enum(['Patient', 'Staff', 'Admin'])` — client-side validation mirrors API validation to catch errors before the network round-trip (AC-001, AC-005; OWASP A03)
- [ ] `UserModal.tsx` submit handler sets a `formError` state on 409 and renders the error message inside the modal body — `onClose` is NOT called; the modal stays open so the admin can correct the email (AC-005)
- [ ] Own-account Deactivate button in `UserManagementPage.tsx` is detected by comparing `user.id === authContext.userId` (not by role); the button receives `aria-disabled="true"` and `onClick` shows the error banner without dispatching a `patchUser` call — avoids an additional API round-trip (AC-004; accessibility)
- [ ] `DeactivateDialog.tsx` is reused for both deactivation and reactivation — the message copy and the `PatchUserRequest.isActive` value differ based on a `targetState: boolean` prop; no duplicated dialog components (AC-003; Edge: reactivation — DRY)
- [ ] `adminUsersApi.ts` includes the `Authorization` header from `AuthContext` on every request; a 401 response triggers the same session-expired flow as other API calls (AC-001, AC-002, AC-003; OWASP A01)
- [ ] Table rows are updated in-place after successful modal or dialog submission (optimistic update on local `users` state); no full page re-fetch required — immediate feedback per AC-002 and AC-003 (AC-002, AC-003; UX responsiveness)
- [ ] MOD-006 form fields include associated `<label>` elements with `htmlFor`; error messages use `role="alert"` for screen reader announcement — accessibility requirement aligned with UXR-105 spirit (AC-001, AC-005; WCAG 2.1)
