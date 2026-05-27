# Bug Fix Task - bug_404_no_error_boundary

## Bug Report Reference

- Bug ID: `404_no_error_boundary`
- Source: Direct user-reported error — runtime UI screenshot/log

---

## Bug Summary

### Issue Classification

- **Priority**: High
- **Severity**: Core UX broken — any undefined route renders React Router's raw internal error UI
- **Affected Version**: HEAD (`8479684`) — branch `Propel-IQ_Bs`
- **Environment**: All browsers, all OS. Reproducible on any deployment where a user navigates to an undefined path (including `/`)

### Steps to Reproduce

1. Start the frontend dev server (`npm run dev` from `frontend/`)
2. Navigate directly to `http://localhost:5173/` (root path) or any undefined path (e.g., `/dashboard`, `/unknown`)
3. **Expected**: A branded 404 page or an automatic redirect to `/login`
4. **Actual**: React Router v6 default error UI renders

**Error Output**:

```text
Unexpected Application Error!
404 Not Found
💿 Hey developer 👋

You can provide a way better UX than this when your app throws errors by
providing your own ErrorBoundary or errorElement prop on your route.
```

### Root Cause Analysis

- **File**: `frontend/src/router/index.tsx:24-121`
- **Component**: `router` — `createBrowserRouter` route array
- **Function**: `createBrowserRouter([...routes])`
- **Cause**: The flat route array passed to `createBrowserRouter` has no `errorElement` property on any route object, and no catch-all `path: '*'` route. When React Router v6 receives a navigation to an unregistered URL (including the root `/`), it throws an `ErrorResponseImpl { status: 404 }` and falls through to its built-in default error renderer. There is no parent layout route to intercept this with a custom `errorElement`, so the raw development-mode error UI — including the literal "💿 Hey developer 👋" message — is shown in all environments.

### Impact Assessment

- **Affected Features**: All routes — any unrecognised URL (stale bookmarks, direct URL entry, removed pages, root domain navigation) shows the broken UI
- **User Impact**: Any user who lands on an undefined path (including the root `/`) sees React Router's internal error UI rather than a branded experience; the root `/` has no route defined, blocking first-time navigation
- **Data Integrity Risk**: No
- **Security Implications**: The React Router development error UI may expose internal route structure and stack trace information in non-production builds; should be replaced with a controlled error surface

---

## Fix Overview

Add a root layout route wrapping all existing routes. Attach `errorElement={<ErrorPage />}` to this root route so all route-level errors (404, loader failures, render errors) are intercepted and rendered via a branded component. Add a `path: '*'` catch-all as a sibling to all existing routes to render a branded `NotFoundPage` for any unmatched URL. Add an index redirect from `/` to `/login`.

---

## Fix Dependencies

- React Router v6 (`react-router-dom`) already installed — no new dependencies required
- `useRouteError` hook is available in the installed version of `react-router-dom`

---

## Impacted Components

### Frontend — React / TypeScript

- `frontend/src/pages/NotFoundPage.tsx` — **NEW**: Branded 404 page with "Return to Login" CTA
- `frontend/src/pages/ErrorPage.tsx` — **NEW**: Generic route error page using `useRouteError()`
- `frontend/src/router/index.tsx` — **MODIFIED**: Wrap routes in root layout with `errorElement`, add index redirect and `path: '*'` catch-all

### Frontend — Tests

- `frontend/src/pages/NotFoundPage.test.tsx` — **NEW**: Unit test for `NotFoundPage` render and navigation link
- `frontend/src/pages/ErrorPage.test.tsx` — **NEW**: Unit test for `ErrorPage` error message rendering via mocked `useRouteError`
- `frontend/src/router/index.test.tsx` — **NEW**: Integration test asserting unknown paths render `NotFoundPage`

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `frontend/src/pages/NotFoundPage.tsx` | Branded 404 page component; renders heading, message, and a `<Link to="/login">` CTA |
| CREATE | `frontend/src/pages/ErrorPage.tsx` | Generic route error boundary; calls `useRouteError()` to display a safe error message |
| MODIFY | `frontend/src/router/index.tsx` | Wrap all routes under a root `{ path: '/', errorElement: <ErrorPage />, children: [...] }`; add `{ index: true, element: <Navigate to="/login" replace /> }`; add `{ path: '*', element: <NotFoundPage /> }` |
| CREATE | `frontend/src/pages/NotFoundPage.test.tsx` | Regression unit test — renders `NotFoundPage` in a `MemoryRouter` and asserts heading and `/login` link |
| CREATE | `frontend/src/pages/ErrorPage.test.tsx` | Regression unit test — mocks `useRouteError` and asserts safe error message is rendered |
| CREATE | `frontend/src/router/index.test.tsx` | Regression integration test — uses `createMemoryRouter` to navigate to `/unknown` and assert `NotFoundPage` is rendered |

> Only list concrete, verifiable file operations. No speculative directory trees.

---

## Implementation Plan

1. **Create `NotFoundPage.tsx`**: Functional component returning a centred card layout with an `<h1>404 – Page Not Found</h1>`, a descriptive paragraph, and a React Router `<Link to="/login">Return to Login</Link>` button. Use existing `index.css` utility classes for styling — no new CSS module required.

2. **Create `ErrorPage.tsx`**: Functional component that calls `useRouteError()` from `react-router-dom`. Cast the error to extract a safe `statusText` or `message` string (never expose a raw stack trace). Render a branded card with the error summary and a `<Link to="/login">Return to Login</Link>` CTA.

3. **Modify `router/index.tsx`**:
   - Import `ErrorPage`, `NotFoundPage`, and `Navigate` from their respective locations.
   - Replace the flat `createBrowserRouter([...routes])` array with a **single root route** object: `{ path: '/', errorElement: <ErrorPage />, children: [...existingRoutes] }`.
   - Add `{ index: true, element: <Navigate to="/login" replace /> }` as the **first child** of the root route (handles direct `/` navigation).
   - Append `{ path: '*', element: <NotFoundPage /> }` as the **last child** of the root route (catch-all for all other unknown paths).
   - All existing route objects remain unchanged — they are moved verbatim into the `children` array.

4. **Write `NotFoundPage.test.tsx`**: Render `<NotFoundPage />` wrapped in `<MemoryRouter>`. Assert heading text "404" is visible and the link to `/login` exists.

5. **Write `ErrorPage.test.tsx`**: Mock `react-router-dom`'s `useRouteError` to return `{ status: 404, statusText: 'Not Found' }`. Assert the rendered output contains "Not Found" without exposing a stack trace.

6. **Write `router/index.test.tsx`**: Use `createMemoryRouter` with `initialEntries={['/unknown-path']}` pointing at the exported `router` routes. Assert `NotFoundPage` heading renders. Assert navigating to `/login` does not trigger `NotFoundPage`.

---

## Regression Prevention Strategy

- [ ] Unit test: `NotFoundPage` renders heading and `/login` link (`NotFoundPage.test.tsx`)
- [ ] Unit test: `ErrorPage` renders `statusText` from `useRouteError()` without exposing raw stack (`ErrorPage.test.tsx`)
- [ ] Integration test: navigating to `/unknown` route renders `NotFoundPage` and not React Router's default error UI (`router/index.test.tsx`)
- [ ] Integration test: navigating to `/` redirects to `/login` (index route `Navigate` redirect)
- [ ] Manual smoke test: navigate to `http://localhost:5173/unknown` — branded 404 page renders
- [ ] Manual smoke test: navigate to `http://localhost:5173/` — redirects to `/login`

---

## Rollback Procedure

1. **Detection**: After applying the fix, if any existing route navigations are broken (components not rendering, layout shifts), the root layout route may have introduced an unexpected `Outlet` requirement. Check: do existing routes still render without an `<Outlet />` in the root route's `element`? (The root route uses no `element` — only `children` — so `Outlet` is not required.)
2. **Revert**: Remove the wrapping root route and restore the flat `createBrowserRouter([...])` array. Delete `NotFoundPage.tsx` and `ErrorPage.tsx` imports from `router/index.tsx`. The `path: '*'` and `index: true` entries must be removed.
3. **Data Recovery**: Not applicable — no data mutations involved.

---

## External References

- [React Router v6 — `errorElement`](https://reactrouter.com/en/main/route/error-element)
- [React Router v6 — `useRouteError`](https://reactrouter.com/en/main/hooks/use-route-error)
- [React Router v6 — Catch-All / 404 routes](https://reactrouter.com/en/main/route/route#path)
- [OWASP A05:2021 — Security Misconfiguration: avoid leaking stack traces to end users](https://owasp.org/Top10/A05_2021-Security_Misconfiguration/)

---

## Build Commands

```bash
# Frontend
cd frontend
npm install           # No new deps needed
npm run dev           # Dev server on http://localhost:5173
npm run build         # Production build
npm run test          # Vitest unit tests
npm run test -- --run # CI non-interactive run
```

---

## Implementation Validation Strategy

- [ ] Navigate to `http://localhost:5173/` — should redirect to `/login`, not show error
- [ ] Navigate to `http://localhost:5173/unknown-path` — should render branded `NotFoundPage`, not React Router default error
- [ ] Navigate to a valid route (e.g., `/login`) — should render normally with no regression
- [ ] All existing Vitest tests pass: `npm run test -- --run`
- [ ] New regression tests pass: `NotFoundPage.test.tsx`, `ErrorPage.test.tsx`, `router/index.test.tsx`
- [ ] Production build succeeds: `npm run build` exits 0

---

## Implementation Checklist

- [ ] Create `frontend/src/pages/NotFoundPage.tsx` with branded 404 UI and `/login` link [SOURCE:INPUT]
- [ ] Create `frontend/src/pages/ErrorPage.tsx` using `useRouteError()`, safe message display, no stack trace leakage [SOURCE:INFERRED] — Basis: OWASP A05 — do not expose internal error details to end users
- [ ] Modify `frontend/src/router/index.tsx`: wrap routes in root route, add `errorElement`, add `index` redirect, add `path: '*'` catch-all [SOURCE:INPUT]
- [ ] Write `NotFoundPage.test.tsx` regression unit test [SOURCE:INFERRED] — Basis: workflow requires at least one regression test per fix component
- [ ] Write `ErrorPage.test.tsx` regression unit test [SOURCE:INFERRED] — Basis: workflow requires at least one regression test per fix component
- [ ] Write `router/index.test.tsx` integration regression test [SOURCE:INFERRED] — Basis: validates the catch-all and errorElement work end-to-end in the router
- [ ] Verify no existing route navigation is broken (manual smoke test) [SOURCE:INFERRED] — Basis: structural change to router wrapping requires regression verification
- [ ] Confirm `npm run build` exits 0 with no TypeScript errors [SOURCE:INFERRED] — Basis: standard build gate
