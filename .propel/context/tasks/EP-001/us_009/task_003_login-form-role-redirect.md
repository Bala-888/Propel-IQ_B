# Task - TASK_003

## Requirement Reference
- **User Story:** us_009
- **Story Location:** .propel/context/tasks/EP-001/us_009/us_009.md
- **Acceptance Criteria:**
  - AC-002: Patient user → browser navigates to `/intake` (or stored `redirect` path) within 500 ms of receiving HTTP 200
  - AC-003: Staff user → browser navigates to `/queue` within 500 ms
  - AC-004: Admin user → browser navigates to `/admin` within 500 ms
- **Edge Cases:**
  - N/A — JWT expiry and refresh token replay edge cases are handled server-side in task_002; the SPA receives a 401 and must invoke the refresh flow or redirect to login

---

## Design References
| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | SCR-001 (Login) |
| **Wireframe Status** | AVAILABLE |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | .propel/context/wireframes/Hi-Fi/wireframe-SCR-001-login.html |
| **Screen Spec** | SCR-001 |
| **UXR Requirements** | UXR-105 — no color-only error states; every login failure message must include a warning icon AND error text |
| **Design Tokens** | Refer to project design system tokens for error color (`--color-error`), input border-error state, and icon sizing |

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
| Frontend | React | 18.x | TR-001 (SPA framework) |
| Frontend | TypeScript | 5.x | TR-001 (type-safe role discrimination and JWT decode) |
| Frontend | Vite | 5.x | TR-001 (build tooling) |
| Frontend | React Router | v6 | TR-001 (`useNavigate` for role-based redirect; `location.state` for stored redirect path) |

---

## Task Overview

Build the `LoginForm` React component for route `/login`. Use `react-hook-form` with Zod for client-side validation. Render a warning icon alongside any error message to satisfy UXR-105. On HTTP 200, decode the JWT `role` claim without a third-party library (`atob` + JSON.parse on the payload), store the `accessToken` in React context (not `localStorage` — XSS risk), store the `refreshToken` in `sessionStorage`, then navigate to the role-appropriate dashboard path — or the stored redirect path if present.

---

## Dependent Tasks
- task_002 (us_002) — React Router v6 route config must exist for adding `/login`, `/intake`, `/queue`, `/admin` routes
- task_002 (us_009) — `POST /auth/login` endpoint must be available for the form to call

---

## Impacted Components
- `src/web/src/features/auth/LoginForm.tsx` — new login form component
- `src/web/src/features/auth/loginSchema.ts` — new Zod validation schema
- `src/web/src/context/AuthContext.tsx` — add `accessToken`, `role`, and `setAuth` to context
- `src/web/src/App.tsx` — add `/login` route and role-guarded `/intake`, `/queue`, `/admin` routes
- `src/web/src/utils/jwt.ts` — new utility: `decodeJwtRole(token: string): string`

---

## Implementation Plan
1. Create `src/web/src/utils/jwt.ts` with `decodeJwtRole(token: string): string` — splits the JWT on `.`, base64-decodes the payload with `atob`, JSON.parses, and returns `payload.role`; no third-party JWT library needed for read-only client-side decode
2. Create `src/web/src/features/auth/loginSchema.ts` with a Zod schema: `email` (string, email), `password` (string, min 1)
3. Create `LoginForm.tsx` using `useForm({ resolver: zodResolver(loginSchema) })`; render email and password inputs; on submit, `POST /api/auth/login` with credentials; on HTTP 400/401 response, call `setError('root', { message: errorBody.error })` and render the root error with a `<WarningIcon />` element before the message text (UXR-105 — icon + text, not color-only)
4. On HTTP 200: call `decodeJwtRole(response.accessToken)` to get the role; store `accessToken` in `AuthContext` (in-memory state); store `refreshToken` in `sessionStorage`; clear any plaintext credentials from local variables immediately after use
5. Role-based redirect: check `sessionStorage.getItem('redirectAfterLogin')`; if present, navigate to that path; else `role === "Patient"` → `navigate('/intake')`, `role === "Staff"` → `navigate('/queue')`, `role === "Admin"` → `navigate('/admin')` — all three redirects execute within the same synchronous React commit cycle to meet the 500 ms requirement (AC-002, AC-003, AC-004)
6. In `App.tsx`, add `<Route path="/login" element={<LoginForm />} />`; in `PrivateRoute` wrapper (or equivalent), save `location.pathname` to `sessionStorage` as `redirectAfterLogin` before redirecting unauthenticated users to `/login` — this enables the stored redirect path behaviour (AC-002)

---

## Current Project State
```
src/
└── web/
    └── src/
        ├── App.tsx                           (MODIFY — add /login route, update PrivateRoute)
        ├── context/
        │   └── AuthContext.tsx               (MODIFY — add accessToken, role, setAuth)
        ├── utils/
        │   └── jwt.ts                        (CREATE)
        └── features/
            └── auth/
                ├── LoginForm.tsx             (CREATE)
                └── loginSchema.ts            (CREATE)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | src/web/src/utils/jwt.ts | `decodeJwtRole(token)` — pure base64 decode, no library dependency |
| CREATE | src/web/src/features/auth/loginSchema.ts | Zod schema for email and password fields |
| CREATE | src/web/src/features/auth/LoginForm.tsx | Login form with react-hook-form, UXR-105 error display, role-based redirect, redirect-path restoration |
| MODIFY | src/web/src/context/AuthContext.tsx | Add `accessToken: string \| null`, `role: string \| null`, and `setAuth(token, role)` setter |
| MODIFY | src/web/src/App.tsx | Add `/login` route; update `PrivateRoute` to save redirect path before bouncing to `/login` |

---

## External References
- https://react-hook-form.com/get-started (react-hook-form — useForm, setError for root/server errors)
- https://zod.dev/?id=basic-usage (Zod — string().email() validator)
- https://reactrouter.com/en/main/hooks/use-navigate (React Router v6 useNavigate — synchronous navigation)
- https://datatracker.ietf.org/doc/html/rfc7519#section-3.1 (JWT structure — base64url-encoded payload for client-side decode)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] Login with a Patient account → browser navigates to `/intake` within 500 ms; no page reload occurs (AC-002)
- [ ] Login with a Staff account → browser navigates to `/queue` within 500 ms (AC-003)
- [ ] Login with an Admin account → browser navigates to `/admin` within 500 ms (AC-004)
- [ ] Submit login form with wrong password (API returns 401) → red inline error with warning icon appears under the form — no `alert()` or toast (UXR-105)
- [ ] Navigate to a protected route as an unauthenticated user; complete login → browser redirects to the originally requested path, not the default role path (AC-002 — stored redirect path)

---

## Implementation Checklist
- [x] `decodeJwtRole` uses `atob(token.split('.')[1])` and `JSON.parse` — no third-party JWT library introduced for client-side role decode (AC-002, AC-003, AC-004; bundle-size hygiene)
- [x] `accessToken` is stored in `AuthContext` React state (in-memory), NOT in `localStorage` or `document.cookie` — prevents XSS token theft (OWASP A02; token storage best practice)
- [x] Root error from a 401 API response is rendered with a `<WarningIcon />` element alongside the text — satisfies UXR-105 (no color-only error indication) (AC-002, AC-003, AC-004 — login failure display)
- [x] Role-based redirect uses an exhaustive switch/if-else covering all three roles; an unknown role defaults to `/login` to prevent silent access to a wrong dashboard (AC-002, AC-003, AC-004 — unknown role guard)
- [x] `sessionStorage.getItem('redirectAfterLogin')` is checked before the role-based default redirect and cleared immediately after use — prevents stale redirect paths affecting future logins (AC-002)
- [x] `PrivateRoute` stores `location.pathname + location.search` as `redirectAfterLogin` in `sessionStorage` before navigating to `/login` — enables the redirect restoration flow tested in validation (AC-002)
