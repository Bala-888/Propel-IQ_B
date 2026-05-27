# Bug Fix Task - bug_login_submit_disabled

## Bug Report Reference

- Bug ID: `login_submit_disabled`
- Source: User-reported — "credentials not working" on login page; Sign In button appears unresponsive despite correct email and password

---

## Bug Summary

### Issue Classification

- **Priority**: High
- **Severity**: Any user who fills in valid credentials and clicks Sign In without explicitly blurring the password field is silently blocked — the button does nothing, no error is shown, credentials appear broken
- **Affected Version**: HEAD — branch `Propel-IQ_Bs`
- **Environment**: All environments. Reproducible on every fresh page load.

### Steps to Reproduce

1. Navigate to `http://localhost:5173/login`
2. Click the Email field → type `admin@clinic.com`
3. Click the Password field → type `Admin@1234`
4. Click the **Sign In** button directly (without pressing Tab to move focus away from password)
5. **Expected**: Form submits, user is authenticated and redirected to `/admin`
6. **Actual**: Nothing happens. Button is disabled and unclickable. No error message is shown.

---

## Root Cause Analysis

### `mode: 'onBlur'` + `disabled={!isValid}` deadlock

- **File**: `frontend/src/features/auth/LoginForm.tsx`
- **Cause**: Two settings interact to create a state where the button can never be clicked:

  1. `useForm` is configured with `mode: 'onBlur'` — React Hook Form only runs field validation when a field loses focus (blur event). Before any field is blurred, `isValid = false` regardless of what the user has typed.

  2. The submit button uses `disabled={!isValid || isSubmitting}`. Since `isValid` starts as `false` and only becomes `true` after **every required field has been blurred and is valid**, the button remains disabled until the user explicitly tabs away from the last field.

  **Concrete scenario**:
  - User clicks Email → types valid email → clicks Password (blurs email ✓, email validated)
  - User types valid password → **clicks Sign In directly** (password field never blurred)
  - `isValid = false` because password hasn't been validated yet (no blur event fired)
  - Button is `disabled` → click is silently ignored → no error, no feedback

  The user sees correct credentials entered and a button they can click, but nothing happens.

  **Note**: pressing `Enter` inside the password input field **does** work because `handleSubmit` is called via the form's submit event, bypassing the button's `disabled` attribute entirely. This inconsistency (Enter works, click doesn't) adds to the confusion.

---

## Fix

**File**: `frontend/src/features/auth/LoginForm.tsx`

Removed `!isValid` from the `disabled` attribute on the submit button:

```tsx
// Before
<button disabled={!isValid || isSubmitting} ...>

// After
<button disabled={isSubmitting} ...>
```

**Why this is safe**: `handleSubmit(onSubmit)` already runs full validation before calling `onSubmit`. If any field is invalid, `handleSubmit` populates `errors` and surfaces inline error messages — it never calls `onSubmit` with invalid data. The `!isValid` guard was a redundant pessimistic check that caused the regression.

---

## Impact Assessment

- **Affected Features**: Login for all roles (Patient, Staff, Admin)
- **User Impact**: Any user who clicks the button without tabbing away from the last field cannot log in — the form appears to silently reject their credentials, leading to frustration and repeat failed attempts (which can also trigger the rate limiter)
- **Security Implications**: None. Removing `!isValid` does not bypass authentication — the API still validates credentials server-side. Client-side form validation errors are still shown via `handleSubmit`'s built-in validation pass.

---

## Fix Summary

| File | Change |
|---|---|
| `frontend/src/features/auth/LoginForm.tsx` | Changed `disabled={!isValid \| \| isSubmitting}` → `disabled={isSubmitting}` on the Sign In button |
