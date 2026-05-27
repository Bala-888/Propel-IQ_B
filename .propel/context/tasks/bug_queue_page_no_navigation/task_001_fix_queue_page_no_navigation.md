# Bug Fix Task - bug_queue_page_no_navigation

## Bug Report Reference

- Bug ID: `queue_page_no_navigation`
- Source: User-reported — after Staff login, only a table with "No patients in queue for today" is visible; no other options or actions are displayed

---

## Bug Summary

### Issue Classification

- **Priority**: High
- **Severity**: Staff users are completely unable to perform any action beyond viewing the empty queue — they cannot create walk-in bookings, cannot sign out, and cannot navigate to any other part of the application
- **Affected Version**: HEAD — branch `Propel-IQ_Bs`
- **Environment**: All environments. Reproducible on every Staff login.

### Steps to Reproduce

1. Navigate to `http://localhost:5173/login`
2. Sign in as `staff@clinic.com` / `Staff@1234`
3. Redirected to `/queue`
4. **Expected**: Queue dashboard with a navigation header, "New Walk-In" button to register walk-in patients, sign-out option, and the queue table
5. **Actual**: Only a bare queue table is shown. No header, no buttons, no navigation, no sign-out. "No patients in queue for today." is the only visible content beyond the table columns.

---

## Root Cause Analysis

### `QueuePage` had no navigation, action bar, or Header component

- **File**: `frontend/src/pages/QueuePage.tsx`
- **Cause**: `QueuePage` is the Staff role's default landing page (per `LoginForm.tsx` `ROLE_DESTINATIONS: { Staff: '/queue' }`). However, the page was implemented as a standalone queue table with no surrounding navigation shell:

  1. **No `<Header />`** — `AdminPage` imports and renders `<Header />` for the UPACIP wordmark; `QueuePage` did not.

  2. **No "New Walk-In" action** — Creating a walk-in booking (`/walkin/new`) is the primary staff action. There was no button or link to reach this page from the queue dashboard.

  3. **No sign-out** — There was no mechanism for Staff to end their session. The `SessionTimeoutModal` handles forced logout after inactivity, but there was no voluntary sign-out path.

  The result: Staff users logged in to a dead-end page. Every session required closing the browser tab to "log out", and there was no path to any other feature.

---

## Fix

**File**: `frontend/src/pages/QueuePage.tsx`

Three additions made to the page:

### 1. Added `<Header />` component
```tsx
import { Header } from '../components/layout/Header'
// ...
<Header />
```

### 2. Added action bar with "New Walk-In" button and "Sign out"
```tsx
<div style={styles.actionBar}>
  <div style={styles.pageTitle}>
    <h1>Today's queue</h1>
    <p>{date}</p>
  </div>
  <div style={styles.actionGroup}>
    <Link to="/walkin/new" style={styles.btnWalkIn}>+ New Walk-In</Link>
    <button style={styles.btnSignOut} onClick={handleSignOut}>Sign out</button>
  </div>
</div>
```

### 3. Added `handleSignOut` function
```tsx
function handleSignOut() {
  setAuth(null, null)                          // clear in-memory access token
  sessionStorage.removeItem('refreshToken')    // revoke stored refresh token reference
  sessionStorage.removeItem('redirectAfterLogin')
  navigate('/login', { replace: true })
}
```

---

## Impact Assessment

- **Affected Features**: All Staff-initiated workflows — walk-in booking, session management, navigation
- **User Impact**: Staff could not perform their primary duty (registering walk-in patients) and could not sign out. Effectively a complete loss of functionality for the Staff role.
- **Security Implications**: Lack of sign-out is a minor session hygiene concern — fixed by the `handleSignOut` handler which clears both the in-memory token and the `sessionStorage` refresh token before redirecting.

---

## Fix Summary

| File | Change |
|---|---|
| `frontend/src/pages/QueuePage.tsx` | Added `<Header />`, `handleSignOut`, action bar styles, "New Walk-In" link, and "Sign out" button |
