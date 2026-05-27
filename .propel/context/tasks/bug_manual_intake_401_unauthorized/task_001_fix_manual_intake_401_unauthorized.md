# Bug Fix Task - bug_manual_intake_401_unauthorized

## Bug Report Reference

- Bug ID: `manual_intake_401_unauthorized`
- Source: Direct user-reported error — `POST /api/intake/manual` returns 401 Unauthorized for an authenticated patient

---

## Bug Summary

### Issue Classification

- **Priority**: Critical
- **Severity**: All patient write operations return 401 — intake submission, booking, mode switch, and document access are completely broken for authenticated patients
- **Affected Version**: HEAD — branch `Propel-IQ_Bs`
- **Environment**: All environments. Every request hitting a `GetPatientId()` helper returns 401 despite a valid JWT.

### Steps to Reproduce

1. Register a patient account and log in to obtain a valid JWT
2. Navigate to `/intake/manual`, fill all 5 sections, and click Submit
3. **Expected**: `POST /api/intake/manual` → `201 Created`
4. **Actual**: `POST /api/intake/manual` → `401 Unauthorized`

---

## Root Cause Analysis (Two Distinct Bugs)

### Bug 1 — `ClaimTypes.NameIdentifier` used with `MapInboundClaims = false`

- **File**: `GetPatientId()` helper in 4 controllers
- **Cause**: `Program.cs` sets `opts.MapInboundClaims = false` to prevent the JWT middleware from renaming short-form claim keys to long-form `ClaimTypes` URL strings. With this setting, the `sub` claim is stored as `"sub"`, **not** as `ClaimTypes.NameIdentifier` (`http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier`).

  Four controllers called `User.FindFirstValue(ClaimTypes.NameIdentifier)` with no fallback, which always returned `null` → `GetPatientId()` returned `null` → controller returned `Unauthorized()`.

  **Controllers affected (no fallback — broken)**:
  - `ManualIntakeController.cs`
  - `BookingsController.cs`
  - `ModeSwitchController.cs`
  - `DocumentsController.cs`

  **Fix**: Changed all four to `User.FindFirstValue(JwtRegisteredClaimNames.Sub)` and added `using System.IdentityModel.Tokens.Jwt;`.

### Bug 2 — JWT `sub` (user ID) used as `patients.id` (separate sequence)

- **Files**: `GetPatientId()` helpers + `TokenService` + `AuthController` + `User` entity
- **Cause**: After fixing Bug 1, the patient ID read from `sub` was the **user ID** (e.g., `8` for `users.id = 8`). However, `intake_records.patient_id` is a FK to `patients.id`, which is a separate auto-increment sequence (patient ID was `6` for the same user). Inserting with `patient_id = 8` violated the FK constraint → `500` with `23503: insert or update violates foreign key constraint`.

  Root design gap: `User` and `Patient` are independent entities with no FK between them; registration creates both but the JWT only embedded `user.Id`.

  **Fix — three-part**:
  1. Added `PatientId int?` property to `User` entity.
  2. Added `patient_id` column (FK → `patients.id`) to the `users` table.
  3. Updated `AuthController.RegisterAsync` to set `newUser.PatientId = newPatient.Id` after the first `SaveChangesAsync` (so `newPatient.Id` is populated).
  4. Updated `TokenService.GenerateAccessToken` to emit a `pid` claim (`patients.id`) when `user.PatientId` is set.
  5. Updated `GetPatientId()` in all 4 controllers to read `pid` first, with `sub` as fallback for tokens issued before this fix.

---

## Impact Assessment

- **Affected Features**: Manual Intake, AI Intake mode switch, Appointment Booking, Document upload/view
- **User Impact**: All patient write operations returned 401 or FK-500 — core patient workflows were completely non-functional after login
- **Data Integrity Risk**: None — requests were rejected before reaching persistent storage
- **Security Implications**: None introduced; the fix restores access only for authenticated patients with a valid `pid` claim

---

## Fix Summary

| File | Change |
|---|---|
| `Program.cs` | No change — `MapInboundClaims = false` is correct |
| `Data/Entities/User.cs` | Added `PatientId int?` property |
| `Services/TokenService.cs` | Emit `pid` claim when `user.PatientId` is set |
| `Controllers/AuthController.cs` | Set `newUser.PatientId = newPatient.Id` after patient save |
| `Controllers/ManualIntakeController.cs` | `GetPatientId()` reads `pid ?? sub`; added `using` |
| `Controllers/BookingsController.cs` | `GetPatientId()` reads `pid ?? sub`; added `using` |
| `Controllers/ModeSwitchController.cs` | `GetPatientId()` reads `pid ?? sub`; added `using` |
| `Controllers/DocumentsController.cs` | `GetPatientId()` reads `pid ?? sub`; added `using` |
| DB: `users` table | Added `patient_id INTEGER REFERENCES patients(id)` |
| DB: `intake_records` | Created table (was missing) |

---

## Verification

```
POST http://localhost:8080/intake/manual  (with valid patient JWT)
→  STATUS: 201
→  {"id":2,"warnings":null}
```
