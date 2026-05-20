# Task - TASK_002

## Requirement Reference
- **User Story:** us_002
- **Story Location:** .propel/context/tasks/EP-TECH/us_002/us_002.md
- **Acceptance Criteria:**
  - AC-002: Routes `/login`, `/intake`, `/queue`, `/admin` each render a designated placeholder component without a 404 or blank screen; browser URL matches the navigated path exactly
  - AC-003: Accessing any protected route without an auth token redirects to `/login?redirect=<original-path>` client-side with no full page reload
  - AC-004: `useRoleRedirect` hook resolves `Patient`→`/intake`, `Staff`→`/queue`, `Admin`→`/admin` from a mock auth context
- **Edge Cases:**
  - Unknown role: If `role` claim contains an unrecognised value (e.g., `Provider`), `useRoleRedirect` falls back to `/login` and emits `console.warn` — no uncaught exception thrown
  - Concurrent route transitions: Rapid back-to-back navigation must not render two routes simultaneously or leave previous route mounted — React Router v6 handles this natively; no custom guard required, but `useNavigate` must not be called outside a Router context

---

## Design References
| Reference Type | Value |
|----------------|-------|
| **UI Impact** | No |
| **Figma URL** | N/A |
| **Wireframe Status** | N/A |
| **Wireframe Type** | N/A |
| **Wireframe Path/URL** | N/A |
| **Screen Spec** | N/A |
| **UXR Requirements** | N/A |
| **Design Tokens** | N/A |

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
| Frontend | React | 18.x | TR-001 (React 18 mandated) |
| Frontend | TypeScript | 5.x (latest stable) | TR-001 (TypeScript mandated) |
| Frontend | React Router | v6 (latest stable) | NFR-010 (free OSS), standard SPA routing for React 18; `createBrowserRouter` API with declarative route config |
| Frontend | React Testing Library | latest stable | NFR-010 (free OSS), unit testing of auth context and hook |

---

## Task Overview

Layer React Router v6 routing, `AuthContext`, `ProtectedRoute`, and `useRoleRedirect` hook onto the scaffold created in task_001. Define four named routes (`/login`, `/intake`, `/queue`, `/admin`) each backed by a thin placeholder page component. Implement `AuthContext` that holds token state and a `ProtectedRoute` wrapper that redirects unauthenticated users to `/login?redirect=<path>`. Implement `useRoleRedirect` that maps the three known role values to their designated paths, falls back to `/login` for unknown roles with a `console.warn`, and is consumed by the post-login entry point.

---

## Dependent Tasks
- task_001 (us_002) — React 18 TypeScript Vite scaffold must exist before routing code can be added

---

## Impacted Components
- `frontend/src/App.tsx` — modified to mount `<RouterProvider>` with the route config
- `frontend/src/router/index.tsx` — new React Router v6 route configuration
- `frontend/src/pages/LoginPage.tsx` — new placeholder login page component
- `frontend/src/pages/IntakePage.tsx` — new placeholder intake page component
- `frontend/src/pages/QueuePage.tsx` — new placeholder queue page component
- `frontend/src/pages/AdminPage.tsx` — new placeholder admin page component
- `frontend/src/context/AuthContext.tsx` — new React context providing token state and `setToken`
- `frontend/src/components/ProtectedRoute.tsx` — new route guard component
- `frontend/src/hooks/useRoleRedirect.ts` — new hook returning destination path by role

---

## Implementation Plan
1. Install `react-router-dom` v6: `npm install react-router-dom` in `frontend/`
2. Create `frontend/src/router/index.tsx` using `createBrowserRouter` with four routes: `{ path: '/login', element: <LoginPage /> }`, `{ path: '/intake', element: <ProtectedRoute><IntakePage /></ProtectedRoute> }`, `{ path: '/queue', element: <ProtectedRoute><QueuePage /></ProtectedRoute> }`, `{ path: '/admin', element: <ProtectedRoute><AdminPage /></ProtectedRoute> }`
3. Create four placeholder page components in `frontend/src/pages/` — each returns a `<main>` with a single `<h1>` naming the page; no business logic
4. Create `frontend/src/context/AuthContext.tsx` exporting `AuthContext` (`React.createContext`) with `{ token: string | null; setToken: (t: string | null) => void }` shape and `AuthProvider` wrapping children with state
5. Create `frontend/src/components/ProtectedRoute.tsx` — reads `token` from `AuthContext`; if null, calls `useNavigate` to `/login?redirect=${encodeURIComponent(location.pathname)}` and returns `null`; otherwise renders `<Outlet />` or `{children}`
6. Create `frontend/src/hooks/useRoleRedirect.ts` — defines `ROLE_PATHS: Record<string, string> = { Patient: '/intake', Staff: '/queue', Admin: '/admin' }`; returns `ROLE_PATHS[role] ?? ('/login' with a `console.warn(\`Unknown role: ${role}\`)`)` — never throws
7. Update `frontend/src/App.tsx` to wrap `<RouterProvider router={router} />` inside `<AuthProvider>`

---

## Current Project State
```
frontend/
├── src/
│   ├── App.tsx                      (MODIFY — mount RouterProvider)
│   ├── router/
│   │   └── index.tsx                (CREATE)
│   ├── pages/
│   │   ├── LoginPage.tsx            (CREATE)
│   │   ├── IntakePage.tsx           (CREATE)
│   │   ├── QueuePage.tsx            (CREATE)
│   │   └── AdminPage.tsx            (CREATE)
│   ├── context/
│   │   └── AuthContext.tsx          (CREATE)
│   ├── components/
│   │   └── ProtectedRoute.tsx       (CREATE)
│   └── hooks/
│       └── useRoleRedirect.ts       (CREATE)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | frontend/src/App.tsx | Replace placeholder `<div>` with `<AuthProvider><RouterProvider router={router} /></AuthProvider>` |
| CREATE | frontend/src/router/index.tsx | `createBrowserRouter` route config for `/login`, `/intake`, `/queue`, `/admin` with `ProtectedRoute` guards |
| CREATE | frontend/src/pages/LoginPage.tsx | Placeholder `<main><h1>Login</h1></main>` — no form logic |
| CREATE | frontend/src/pages/IntakePage.tsx | Placeholder `<main><h1>Intake</h1></main>` |
| CREATE | frontend/src/pages/QueuePage.tsx | Placeholder `<main><h1>Queue</h1></main>` |
| CREATE | frontend/src/pages/AdminPage.tsx | Placeholder `<main><h1>Admin</h1></main>` |
| CREATE | frontend/src/context/AuthContext.tsx | `AuthContext` + `AuthProvider` with `token`/`setToken` state |
| CREATE | frontend/src/components/ProtectedRoute.tsx | Token-check guard; redirects to `/login?redirect=<path>` when unauthenticated |
| CREATE | frontend/src/hooks/useRoleRedirect.ts | Role-to-path map hook with `console.warn` fallback for unknown roles |

---

## External References
- https://reactrouter.com/en/main/routers/create-browser-router (React Router v6 `createBrowserRouter`)
- https://reactrouter.com/en/main/hooks/use-navigate (React Router v6 `useNavigate`)
- https://react.dev/reference/react/createContext (React 18 `createContext`)
- https://testing-library.com/docs/react-testing-library/intro/ (React Testing Library)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [x] `npm run build` in `frontend/` completes with zero errors after all new files are added (AC-001 regression — confirmed via task_001 validation strategy)
- [x] Manual navigation to each of `/login`, `/intake`, `/queue`, `/admin` in development renders the correct placeholder `<h1>` and the browser URL bar matches the target path (AC-002)

---

## Implementation Checklist
- [x] `frontend/src/router/index.tsx` defines exactly four routes (`/login`, `/intake`, `/queue`, `/admin`) using `createBrowserRouter`; protected routes wrap children in `<ProtectedRoute>` (AC-002)
- [x] `ProtectedRoute` reads `token` from `AuthContext`; when `token` is `null`, calls `navigate('/login?redirect=' + encodeURIComponent(location.pathname))` and returns `null` — no full page reload (AC-003)
- [x] `AuthContext` provides `{ token: string | null; setToken }` shape; `AuthProvider` wraps the `RouterProvider` in `App.tsx` so context is available to all route components (AC-003)
- [x] `useRoleRedirect` hook defines `ROLE_PATHS = { Patient: '/intake', Staff: '/queue', Admin: '/admin' }` and returns the mapped path for each known role (AC-004)
- [x] `useRoleRedirect` returns `'/login'` and calls `console.warn(\`Unknown role: \${role}\`)` for any role value not in `ROLE_PATHS` — no `throw`, no uncaught exception (Edge: unknown role)
- [x] All four placeholder page components are plain functional components returning `<main><h1>[Page name]</h1></main>` with no business logic and correct TypeScript return types (AC-002)
- [x] `npm run build` passes with zero TypeScript errors after all router/context/hook files are added — `useNavigate` is only called inside components that are rendered within a `<RouterProvider>` (Edge: concurrent navigation safety / AC-001 regression)
