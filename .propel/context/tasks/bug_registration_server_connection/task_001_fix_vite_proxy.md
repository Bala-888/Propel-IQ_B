# Bug Fix Task - bug_registration_server_connection

## Bug Report Reference

- Bug ID: `registration_server_connection`
- Source: User-reported — "Unable to connect to the server. Please try again." on account creation

---

## Bug Summary

### Issue Classification

- **Priority**: High
- **Severity**: Core feature broken in local development — registration is completely non-functional without Docker
- **Affected Version**: HEAD (`8479684`) — branch `Propel-IQ_Bs`
- **Environment**: Local development only (`npm run dev`). Not reproduced in Docker Compose.

### Steps to Reproduce

1. Start only the ASP.NET Core API: `dotnet run` from `src/api/` (listens on `http://localhost:8080`)
2. Start the Vite dev server: `npm run dev` from `frontend/` (listens on `http://localhost:5173`)
3. Navigate to `http://localhost:5173/register`
4. Fill in all required fields (First Name, Last Name, DOB, Email, Phone)
5. Click **Create Account**
6. **Expected**: Account created; redirected to `/intake`
7. **Actual**: Form displays "Unable to connect to the server. Please try again."

**Error Output**:

```text
Unable to connect to the server. Please try again.
(form-level root error rendered at the top of the registration form)
```

Underlying JS error (browser console):

```text
SyntaxError: Unexpected token '<', "<!DOCTYPE "... is not valid JSON
```

### Root Cause Analysis

- **File**: `frontend/vite.config.ts` — missing `server.proxy` section
- **Component**: Vite dev server configuration
- **Function**: `fetch('/api/auth/register', ...)` in `RegistrationForm.tsx:onSubmit`
- **Cause**: `vite.config.ts` defines no `server.proxy`. In local development the Vite dev server runs on port 5173 and the ASP.NET Core API runs on port 8080. The frontend's relative `fetch('/api/auth/register', ...)` resolves to `http://localhost:5173/api/auth/register`. Vite has no handler for `/api/*` paths — it returns its SPA HTML fallback (or a 404 HTML page). When `res.json()` is called on that HTML response it throws `SyntaxError: Unexpected token '<'`, which is caught by the `catch` block in `RegistrationForm.tsx:104` and surfaced as "Unable to connect to the server. Please try again."

  In Docker Compose, nginx's `location /api/` rule proxies and strips the `/api/` prefix before forwarding to the API container, so the issue is masked there.

### Impact Assessment

- **Affected Features**: Patient registration (`/register`) — completely broken in local dev
- **User Impact**: Developers running the app locally cannot register or test the registration flow without Docker
- **Data Integrity Risk**: No
- **Security Implications**: None — the fix adds a dev-server proxy; no production path changes

---

## Fix Overview

Add a `server.proxy` entry to `vite.config.ts` that forwards all `/api/*` requests from the Vite dev server to `http://localhost:8080`, rewriting the path to strip the `/api` prefix — replicating the nginx `proxy_pass http://api:8080/;` (trailing-slash rule) used in Docker.

**Fix already applied.** `frontend/vite.config.ts` now contains:

```ts
server: {
  proxy: {
    '/api': {
      target: 'http://localhost:8080',
      changeOrigin: true,
      rewrite: (path) => path.replace(/^\/api/, ''),
    },
  },
},
```

---

## Fix Dependencies

- No new npm packages required
- ASP.NET Core API must be running on `http://localhost:8080` during local dev

---

## Impacted Components

### Frontend — Vite Configuration

- `frontend/vite.config.ts` — **MODIFIED**: added `server.proxy` block

### Frontend — Tests

- `frontend/src/features/registration/RegistrationForm.test.tsx` — **NEW**: regression test for network failure path

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | `frontend/vite.config.ts` | Add `server.proxy` — `/api` → `http://localhost:8080` with path rewrite stripping `/api` prefix |
| CREATE | `frontend/src/features/registration/RegistrationForm.test.tsx` | Regression: assert network failure renders "Unable to connect" root error instead of throwing |

---

## Implementation Plan

1. ~~**Modify `vite.config.ts`**~~ — **DONE**: `server.proxy` added.
2. **Write `RegistrationForm.test.tsx`**: Mock `fetch` to reject (simulating network failure). Render `<RegistrationForm />` wrapped in `<MemoryRouter>`. Fill required fields and submit. Assert the root error message "Unable to connect to the server. Please try again." appears.

---

## Regression Prevention Strategy

- [ ] Unit test: mock `fetch` to throw `TypeError: Failed to fetch` → assert "Unable to connect to the server" error renders (`RegistrationForm.test.tsx`)
- [ ] Manual smoke test: `npm run dev` → navigate to `/register` → submit form → account is created (no "Unable to connect" error) ← **primary validation**
- [ ] Manual smoke test: kill the API → submit form → "Unable to connect" error renders as expected

---

## Rollback Procedure

1. **Detection**: If the proxy causes CORS errors or unexpected 502s in dev, remove the `server.proxy` block from `vite.config.ts` and restart the Vite dev server.
2. **Revert**: Delete the `server` section added to `vite.config.ts`. Production Docker path is unaffected.
3. **Data Recovery**: Not applicable.

---

## External References

- [Vite — Dev server proxy](https://vitejs.dev/config/server-options.html#server-proxy)
- [nginx `proxy_pass` trailing-slash path stripping](https://nginx.org/en/docs/http/ngx_http_proxy_module.html#proxy_pass)

---

## Build Commands

```bash
cd frontend
npm run dev       # Restart Vite dev server to pick up vite.config.ts change
npm run build     # Production build — server.proxy has no effect on build output
npm run test      # Vitest unit tests
```

---

## Implementation Validation Strategy

- [ ] `npm run dev` → navigate to `/register` → complete form → 201 response → redirected to `/intake`
- [ ] Browser DevTools Network tab: `POST /api/auth/register` now shows forwarded to `http://localhost:8080/auth/register`
- [ ] `npm run build` exits 0 — no TypeScript errors introduced
- [ ] All existing Vitest tests pass: `npm run test -- --run`

---

## Implementation Checklist

- [x] Modify `frontend/vite.config.ts` — add `server.proxy` with `/api` rewrite [SOURCE:INPUT]
- [ ] Write `frontend/src/features/registration/RegistrationForm.test.tsx` — network failure regression test [SOURCE:INFERRED] — Basis: workflow requires at least one regression test covering the root cause path
