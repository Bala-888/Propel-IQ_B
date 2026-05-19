# Task - TASK_002

## Requirement Reference
- **User Story:** us_008
- **Story Location:** .propel/context/tasks/EP-001/us_008/us_008.md
- **Acceptance Criteria:**
  - AC-004: Registration form displays inline, field-specific validation error messages without a page reload; submit button is disabled while any validation error is visible or while a submission is in flight
  - AC-005: On HTTP 201 response, the SPA navigates to `/intake` and the new patient's name appears in the page header within 1 second
- **Edge Cases:**
  - N/A — email format and DOB future-date edge cases are validated server-side and surfaced as server errors mapped to form fields in the error response handler

---

## Design References
| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | SCR-002 (Patient Registration) |
| **Wireframe Status** | AVAILABLE |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | .propel/context/wireframes/Hi-Fi/wireframe-SCR-002-patient-registration.html |
| **Screen Spec** | SCR-002 |
| **UXR Requirements** | UXR-105 — no color-only validation encoding; every field error must include error text AND a warning icon |
| **Design Tokens** | Refer to project design system tokens for error color (`--color-error`), icon size, and input border state |

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
| Frontend | React | 18.x | TR-001 (mandated SPA framework) |
| Frontend | TypeScript | 5.x | TR-001 (type safety for form state and API response shape) |
| Frontend | Vite | 5.x | TR-001 (build tooling for the SPA) |
| Frontend | React Router | v6 | TR-001 (useNavigate hook for post-registration redirect to `/intake`) |

---

## Task Overview

Build the `RegistrationForm` React component for route `/register`. Use `react-hook-form` with a Zod validation schema for client-side field validation. Render a warning icon alongside every inline error message to satisfy UXR-105 (no color-only encoding). Disable the submit button when `formState.isValid` is false or when the form is submitting. On HTTP 201, store the patient name in the auth context and use `useNavigate('/intake')` so the header can display it. Map API 400/409 server validation errors back onto form fields.

---

## Dependent Tasks
- task_002 (us_002) — React Router v6 route config must exist before adding the `/register` route
- task_001 (us_008) — `POST /auth/register` endpoint must exist for the form to submit against

---

## Impacted Components
- `src/web/src/features/registration/RegistrationForm.tsx` — new registration form component
- `src/web/src/features/registration/registrationSchema.ts` — new Zod validation schema
- `src/web/src/context/AuthContext.tsx` — modified to store `patientName` after successful registration
- `src/web/src/App.tsx` (or router config file) — add `/register` route
- `src/web/src/components/layout/Header.tsx` — modified to read and display `patientName` from `AuthContext`

---

## Implementation Plan
1. Install `react-hook-form` and `zod` and `@hookform/resolvers` if not already present; create `src/web/src/features/registration/registrationSchema.ts` with a Zod object schema validating: `name` (string, min 1), `dateOfBirth` (string, valid date format), `email` (string, email), `phone` (string, min 1), `insuranceProvider` (optional string), `insuranceId` (optional string)
2. Create `RegistrationForm.tsx` using `useForm<RegisterFormValues>({ resolver: zodResolver(registrationSchema) })`; render controlled inputs for all six fields; each field's error message node must render a warning icon element (`<WarningIcon />` or inline SVG) alongside the error text — satisfies UXR-105 (no color-only encoding)
3. Submit button: `<button type="submit" disabled={!formState.isValid || formState.isSubmitting}>` — disabled when any validation error is present or API call is in flight (AC-004)
4. `onSubmit` handler: call `POST /api/auth/register` with form data; on HTTP 400 or 409 response, parse the `validationErrors` / `error` body and set errors on the relevant fields using `setError('email', { message: ... })` from react-hook-form
5. On HTTP 201 response: call `authContext.setPatientName(response.name ?? formValues.name)` to store the name, then call `navigate('/intake')` using React Router v6 `useNavigate` (AC-005)
6. Add `/register` route in the router config and update `Header.tsx` to read `authContext.patientName` and render it when present — name must appear within the first render after navigation to `/intake` (AC-005 — 1-second timing requirement met by synchronous context update before navigation)

---

## Current Project State
```
src/
└── web/
    └── src/
        ├── App.tsx                                        (MODIFY — add /register route)
        ├── context/
        │   └── AuthContext.tsx                            (MODIFY — add patientName field)
        ├── components/
        │   └── layout/
        │       └── Header.tsx                             (MODIFY — display patientName from context)
        └── features/
            └── registration/
                ├── RegistrationForm.tsx                   (CREATE)
                └── registrationSchema.ts                  (CREATE)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | src/web/src/features/registration/registrationSchema.ts | Zod schema for all six registration fields with required/optional rules |
| CREATE | src/web/src/features/registration/RegistrationForm.tsx | react-hook-form component with UXR-105 inline errors (text + icon), disabled submit, API call, error mapping, and post-201 navigation |
| MODIFY | src/web/src/context/AuthContext.tsx | Add `patientName: string \| null` to context state and `setPatientName` setter |
| MODIFY | src/web/src/App.tsx | Add `<Route path="/register" element={<RegistrationForm />} />` to router config |
| MODIFY | src/web/src/components/layout/Header.tsx | Read `patientName` from `AuthContext` and render it when truthy |

---

## External References
- https://react-hook-form.com/get-started (react-hook-form — useForm, formState, setError)
- https://zod.dev/?id=basic-usage (Zod — object schema, string validators including .email())
- https://www.npmjs.com/package/@hookform/resolvers (zodResolver — connects Zod schema to react-hook-form)
- https://reactrouter.com/en/main/hooks/use-navigate (React Router v6 useNavigate)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] Submit form with phone field empty — red inline message "Phone number is required" appears below the phone input with a warning icon and without page reload (AC-004; UXR-105)
- [ ] Submit button is `disabled` when `formState.isValid` is false — inspect DOM attribute (AC-004)
- [ ] Submit form with all valid data mocked to return HTTP 201 — React Router navigates to `/intake` and the patient's name is visible in the header (AC-005)
- [ ] Submit form with a duplicate email (API returns 409) — the email field shows the server error message inline (AC-002 client-side display)

---

## Implementation Checklist
- [ ] `registrationSchema.ts` marks `insuranceProvider` and `insuranceId` as `z.string().optional()` — omitting them does not trigger client-side validation errors (Edge: insurance optional client-side)
- [ ] Every field error `<p>` or `<span>` node renders a warning icon element alongside the message text — satisfies UXR-105 (no color-only validation encoding) (AC-004)
- [ ] Submit `<button>` carries `disabled={!formState.isValid || formState.isSubmitting}` — covers both the "has errors" and "API call in flight" disabled states (AC-004)
- [ ] `onSubmit` maps HTTP 400 `validationErrors` keys to react-hook-form `setError(fieldName, { message })` so server-side errors (email format, DOB future) surface as inline field errors without re-rendering the whole page (AC-004; Edge server errors surfaced in form)
- [ ] `authContext.setPatientName(...)` is called synchronously before `navigate('/intake')` so the header component receives the name in the same render cycle — meets the 1-second display requirement (AC-005)
- [ ] `/register` route is added to the router configuration and the `RegistrationForm` component is the route element — the form is accessible at the correct path (AC-004, AC-005)
