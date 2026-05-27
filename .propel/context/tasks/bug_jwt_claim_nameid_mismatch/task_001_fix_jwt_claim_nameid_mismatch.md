# Bug Fix Task - bug_jwt_claim_nameid_mismatch

## Bug Report Reference

- Bug ID: `jwt_claim_nameid_mismatch`
- Source: Direct user-reported error — `POST /api/intake/manual` returns 401 Unauthorized

---

## Bug Summary

### Issue Classification

- **Priority**: Critical
- **Severity**: All patient write operations return 401 — intake submission, booking, mode switch, and document access are completely broken for authenticated patients
- **Affected Version**: HEAD — branch `Propel-IQ_Bs`
- **Environment**: All environments. Every request hitting a `GetPatientId()` helper on a controller action returns 401 despite a valid JWT.

### Steps to Reproduce

1. Log in as a Patient (`patient@clinic.com` / `Patient@1234`) and obtain a JWT
2. Navigate to `/intake/manual`, fill the form, and submit
3. **Expected**: `POST /api/intake/manual` → `201 Created`
4. **Actual**: `POST /api/intake/manual` → `401 Unauthorized`

### Root Cause Analysis

- **Layer**: ASP.NET Core controller — `GetPatientId()` helper method
- **Cause**: `Program.cs` configures JWT authentication with `MapInboundClaims = false`:
  ```csharp
  opts.MapInboundClaims = false;
  ```
  This prevents the JWT middleware from remapping short-form JWT claim names to the long-form `ClaimTypes` URL strings. Specifically, the `sub` claim remains stored in the `ClaimsPrincipal` under the key `"sub"`, **not** under `ClaimTypes.NameIdentifier` (`http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier`).

  However, the `GetPatientId()` helper in four controllers was reading the claim using the long-form key:
  ```csharp
  var raw = User.FindFirstValue(ClaimTypes.NameIdentifier); // always null
  ```
  Since `ClaimTypes.NameIdentifier` is never populated (due to `MapInboundClaims = false`), `raw` is always `null`, causing `GetPatientId()` to return `null`, which triggers the guard:
  ```csharp
  if (patientId is null)
      return Unauthorized(new { error = "Authentication required." });
  ```
  This returns HTTP 401 for every request — even those with a valid, unexpired JWT.

- **Files Affected** (broken — no fallback):
  - `src/api/Controllers/ManualIntakeController.cs` — `GetPatientId()` line 158
  - `src/api/Controllers/BookingsController.cs` — `GetPatientId()` line 94
  - `src/api/Controllers/ModeSwitchController.cs` — `GetPatientId()` line 138
  - `src/api/Controllers/DocumentsController.cs` — `GetPatientId()` line 181

- **Note**: 10 other `ClaimTypes.NameIdentifier` usages across the codebase already had a `?? User.FindFirstValue("sub")` fallback and were not broken.

### Impact Assessment

- **Affected Features**: Manual Intake submission, Appointment Booking, AI↔Manual mode switch, Document upload/view
- **User Impact**: Authenticated patients received 401 on every form submission — core patient workflows were completely non-functional
- **Data Integrity Risk**: None — requests were rejected before reaching the service layer
- **Security Implications**: None introduced — 401 is the correct behaviour; the fix restores intended access only for authenticated patients

---

## Fix Overview

Changed all four broken `GetPatientId()` helpers to read from `JwtRegisteredClaimNames.Sub` (`"sub"`) — the actual key under which the claim is stored when `MapInboundClaims = false`:

```csharp
// Before (always null — MapInboundClaims = false means this key is never populated)
var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);

// After (correct — reads the "sub" claim directly)
var raw = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
```

Added `using System.IdentityModel.Tokens.Jwt;` to all four files to resolve `JwtRegisteredClaimNames`.

---

## Fix Dependencies

- None — controller-only change; no DB migrations, frontend changes, or config changes required.

---

## Verification

After restarting the API:
```
POST http://localhost:5173/api/intake/manual  →  201 Created
POST http://localhost:5173/api/bookings       →  201 Created
```
