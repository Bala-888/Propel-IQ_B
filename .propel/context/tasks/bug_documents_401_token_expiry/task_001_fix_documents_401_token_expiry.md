# Bug Fix Task - bug_documents_401_token_expiry

## Bug Report Reference

- Bug ID: `documents_401_token_expiry`
- Source: Direct user-reported error — `GET /api/documents` returns 401 Unauthorized when navigating to the Document Processing Status page

---

## Bug Summary

### Issue Classification

- **Priority**: High
- **Severity**: Any patient who stays logged in for more than ~15 minutes will see "Failed to load documents" on every protected page — the session is silently broken for active users
- **Affected Version**: HEAD — branch `Propel-IQ_Bs`
- **Environment**: All environments. Reproducible within 15 minutes of logging in.

### Steps to Reproduce

1. Log in as a Patient and navigate around the app (upload a document, view slots, etc.)
2. Wait 15 minutes (or fast-forward by reducing `AddMinutes(15)` in TokenService)
3. Navigate to `/documents`
4. **Expected**: `GET /api/documents` → `200 OK` with document list
5. **Actual**: `GET /api/documents` → `401 Unauthorized` → UI shows "Failed to load documents"

---

## Root Cause Analysis

### JWT expiry (T+15min) decoupled from inactivity-based refresh

- **Files**: `frontend/src/App.tsx` / `frontend/src/hooks/useInactivityTimer.ts`
- **Cause**: The JWT access token expires 15 minutes after issue (`TokenService.cs: expires: now.AddMinutes(15)`). The only token refresh mechanism in the frontend is the `SessionTimeoutModal`, which is triggered by the **inactivity timer** — it fires after **14 minutes of user idle time** (no mouse/keyboard/scroll events).

  These two clocks are completely independent:
  - **JWT expiry clock**: counts 15 min from token issue time (wall clock)
  - **Inactivity timer**: resets on every user activity event

  A patient who is actively clicking, scrolling, and submitting forms will continuously reset the inactivity timer, meaning the session-timeout modal is never shown and the refresh endpoint is never called. Their JWT still expires at T+15min from login, making all subsequent API calls return 401.

  **Concrete scenario**: Patient logs in → uploads a document → views slots → waits for processing → navigates to `/documents` at T+16min → 401.

  The `DocumentStatusPage` catches the `DocumentApiError` with status 401 and displays "Failed to load documents (401)".

---

## Fix

### Proactive silent token refresh in `AppSessionManager`

Added a `useEffect` in `AppSessionManager` (`App.tsx`) that fires 2 minutes before the JWT expires, regardless of user activity.

**New utility** — `decodeJwtExp` in `utils/jwt.ts`:
```typescript
export function decodeJwtExp(token: string): number | null {
  // Decodes the exp claim and returns it in milliseconds
  // Returns null if malformed or missing
}
```

**Proactive refresh effect** in `AppSessionManager`:
```typescript
useEffect(() => {
  if (!accessToken) return
  const expMs = decodeJwtExp(accessToken)         // ms timestamp of expiry
  if (!expMs) return
  const refreshInMs = expMs - Date.now() - 2 * 60 * 1000  // fire 2 min before exp

  const timer = setTimeout(async () => {
    const storedRefresh = sessionStorage.getItem('refreshToken')
    if (!storedRefresh) return
    const res = await fetch('/api/auth/refresh', { ... body: { refreshToken: storedRefresh } })
    if (!res.ok) return  // inactivity timer handles forced logout on hard failure
    const body = await res.json()
    setAuth(body.accessToken, decodeJwtRole(body.accessToken))
    sessionStorage.setItem('refreshToken', body.refreshToken)
  }, refreshInMs)

  return () => clearTimeout(timer)  // cancel if user logs out before timer fires
}, [accessToken, setAuth])
```

**Behaviour after fix**:
- Token issued at T+0 → refresh fires at T+13min (2 min before expiry)
- Refresh issues new token valid until T+28min → next refresh fires at T+26min
- This perpetual cycle keeps the session alive for as long as the user is using the app
- On refresh failure (network error / refresh token revoked), the effect silently does nothing; the inactivity timer's forced-logout path handles the final cleanup

---

## Impact Assessment

- **Affected Features**: All patient-protected pages (`GET /documents`, `GET /slots`, `POST /intake`, etc.) — any request made after T+15min returns 401
- **User Impact**: Active patients are silently broken after 15 minutes despite never being idle — complete loss of access to all features
- **Data Integrity Risk**: None — requests are rejected, no partial writes
- **Security Implications**: None introduced; refresh token is stored in `sessionStorage` (tab-scoped, not localStorage) and rotated on every use by the backend (token rotation was already implemented in `AuthController.RefreshAsync`). The silent refresh respects the same security model.

---

## Fix Summary

| File | Change |
|---|---|
| `frontend/src/utils/jwt.ts` | Added `decodeJwtExp(token)` — decodes `exp` claim, returns ms timestamp |
| `frontend/src/App.tsx` | Added proactive refresh `useEffect` in `AppSessionManager`; imported `decodeJwtExp` and `decodeJwtRole` |

---

## Verification

`POST /auth/refresh` with a valid refresh token returns `200` and a new access token. After the fix, `AppSessionManager` silently refreshes the token at T+13min so patients navigating to `/documents` at T+15min+ receive a fresh token and `GET /api/documents` returns `200 OK`.
