# Task - TASK_001

## Requirement Reference
- **User Story:** us_012
- **Story Location:** .propel/context/tasks/EP-001/us_012/us_012.md
- **Acceptance Criteria:**
  - AC-001: `POST /walkins` with `createAccount: false` creates a booking record without a linked patient account; the booking is visible in the queue with label "Walk-in — No account"
  - AC-002: `POST /walkins` with `createAccount: true` and a valid email creates a Patient user with a bcrypt-hashed temporary password and links the new patient ID to the booking record
  - AC-003: On successful account creation, an email is dispatched to the patient's address containing the temporary password and a login link; the email must be sent within 30 seconds of the API response
  - AC-004: If `POST /walkins` is called with an email that already belongs to an existing patient, the API returns HTTP 409 with the message "An account with this email already exists. Would you like to link this walk-in to that account?"
- **Edge Cases:**
  - Empty email when `createAccount = true`: the request must fail validation (HTTP 400) with `{"email": "Email is required to create an account"}` — the booking must not be created
  - Email delivery failure: if `IEmailSender` throws on all 3 retry attempts, the walk-in booking must still be committed, the failure must be logged to Seq via `ILogger`, and the API response must include `"credentialsEmailFailed": true`

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
| Backend | ASP.NET Core Web API (C#) | .NET 8.0 | TR-002 (`WalkInsController`; `[Authorize(Roles = "Staff")]`) |
| Backend | EF Core + Npgsql.EntityFrameworkCore.PostgreSQL | 8.x | TR-002 (ORM for `WalkInBooking` and `ApplicationUser` entities; duplicate email check via `AnyAsync`) |
| Backend | BCrypt.Net-Next | 4.x | TR-002 (temporary password hashing; same hasher established in us_009 and us_011) |
| Backend | Microsoft.AspNetCore.Identity (IEmailSender abstraction) | .NET 8.0 built-in | TR-002 (`IEmailSender` for credentials email; 3-retry envelope; established in us_011) |
| Logging | Seq | 2023.4+ | TR-004 (structured log sink for email delivery failure; `ILogger` injected into `WalkInService`) |

---

## Task Overview

Implement the `WalkInsController` with `POST /walkins` secured by `[Authorize(Roles = "Staff")]`. The endpoint handles two execution paths controlled by the `createAccount` boolean in the request body. The booking-only path (AC-001) inserts a `WalkInBooking` record without a patient link. The account-creation path (AC-002) checks for duplicate email (409 on conflict), generates a temporary password, creates a `Patient` user, links the booking, dispatches a credentials email with a 3-retry wrapper (AC-003), and returns `credentialsEmailFailed: true` if all retries fail without rolling back the committed booking (Edge). An optional `linkExistingAccountId` field handles the AC-004 "Yes, link" path.

---

## Dependent Tasks
- task_002 (us_009) — `ApplicationUser` table, JWT middleware, and `[Authorize(Roles = "Staff")]` enforcement must be in place
- task_001 (us_005) — EF Core `ApplicationUser` entity with `Email`, `PasswordHash`, `IsActive`, `Role` columns must exist in the schema
- task_001 (us_011) — `IEmailSender` SMTP configuration and `IUserManagementService` pattern can be reused; `MustChangePassword` column on `ApplicationUser` must already be added

---

## Impacted Components
- `src/api/Controllers/WalkInsController.cs` — new controller: `POST /walkins`
- `src/api/DTOs/CreateWalkInRequest.cs` — new DTO: `PatientName`, `DateOfBirth`, `createAccount: bool`, `Email?`, `linkExistingAccountId: Guid?`
- `src/api/DTOs/CreateWalkInResponse.cs` — new DTO: `BookingId`, `PatientId?`, `credentialsEmailFailed: bool`
- `src/api/Services/IWalkInService.cs` — new service interface
- `src/api/Services/WalkInService.cs` — new service implementation with account creation, email dispatch, and retry logic
- `src/api/Services/AuditLogService.cs` — modified: add `WalkInCreated` and `WalkInAccountLinked` action types

---

## Implementation Plan
1. Create `CreateWalkInRequest` DTO with `[Required] string PatientName`, `[Required] DateOnly DateOfBirth`, `bool CreateAccount`, `string? Email`, `Guid? LinkExistingAccountId`; implement `IValidatableObject.Validate` to enforce `Email` is non-empty when `CreateAccount = true` — returns `{"email": "Email is required to create an account"}` (Edge: empty email; AC-002)
2. Create `CreateWalkInResponse` DTO with `Guid BookingId`, `Guid? PatientId`, `bool CredentialsEmailFailed`; register `IWalkInService` → `WalkInService` in `Program.cs` via `AddScoped` (AC-001, AC-002, AC-003)
3. Implement `WalkInService.CreateAsync(CreateWalkInRequest)` — booking-only path (`CreateAccount = false`): insert `WalkInBooking` with `PatientId = null`, call `IAuditLogService.LogAsync(WalkInCreated)`, return `CreateWalkInResponse { BookingId, PatientId = null, CredentialsEmailFailed = false }` (AC-001)
4. Implement account-creation path in `WalkInService.CreateAsync` (`CreateAccount = true`): check for existing email via `users.AnyAsync(u => u.Email == email)` — throw `DuplicateEmailException` on conflict; generate 12-char random temp password, hash via `BCrypt.HashPassword`, insert `ApplicationUser { Role = "Patient", IsActive = true, MustChangePassword = true }`, set `booking.PatientId = newUser.Id`, call `IAuditLogService.LogAsync(WalkInCreated)` (AC-002, AC-004; OWASP A03)
5. Implement link-existing-account path in `WalkInService.CreateAsync` (`LinkExistingAccountId != null`): load the existing patient by ID — return 404 if not found; set `booking.PatientId = LinkExistingAccountId`, call `IAuditLogService.LogAsync(WalkInAccountLinked)` (AC-004 Yes path)
6. Implement email dispatch with 3-retry wrapper: `for (int attempt = 0; attempt < 3; attempt++) { try { await _emailSender.SendAsync(...); return false; } catch { if (attempt == 2) { _logger.LogError("Credentials email failed after 3 attempts for bookingId={BookingId}", booking.Id); return true; } } }` — returns `credentialsEmailFailed` flag; **email is sent after `SaveChangesAsync` commits** so booking is never rolled back due to email failure (AC-003; Edge: delivery failure; OWASP A09)
7. Implement `WalkInsController` with `[ApiController][Route("walkins")][Authorize(Roles = "Staff")]`; `POST /` calls `_walkInService.CreateAsync(request)` → 201 on success; catches `DuplicateEmailException` → 409 with conflict message body "An account with this email already exists. Would you like to link this walk-in to that account?" (AC-004); catches validation exceptions → 400 (Edge: empty email)

---

## Current Project State
```
src/
└── api/
    ├── Controllers/
    │   └── WalkInsController.cs                     (CREATE)
    ├── DTOs/
    │   ├── CreateWalkInRequest.cs                   (CREATE)
    │   └── CreateWalkInResponse.cs                  (CREATE)
    ├── Services/
    │   ├── IWalkInService.cs                        (CREATE)
    │   ├── WalkInService.cs                         (CREATE)
    │   └── AuditLogService.cs                       (MODIFY — WalkInCreated, WalkInAccountLinked)
    └── Program.cs                                   (MODIFY — register IWalkInService)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | src/api/Controllers/WalkInsController.cs | POST /walkins; Authorize(Roles="Staff"); 201/400/404/409 responses |
| CREATE | src/api/DTOs/CreateWalkInRequest.cs | DTO with IValidatableObject: email required when CreateAccount=true |
| CREATE | src/api/DTOs/CreateWalkInResponse.cs | Response DTO with BookingId, PatientId?, CredentialsEmailFailed |
| CREATE | src/api/Services/IWalkInService.cs | Interface: CreateAsync |
| CREATE | src/api/Services/WalkInService.cs | Implementation: booking-only, account-creation, link-existing paths + email retry |
| MODIFY | src/api/Services/AuditLogService.cs | Add WalkInCreated and WalkInAccountLinked action types |
| MODIFY | src/api/Program.cs | Register IWalkInService as scoped |

---

## External References
- https://learn.microsoft.com/en-us/aspnet/core/mvc/models/validation?view=aspnetcore-8.0 (ASP.NET Core 8 model validation — `IValidatableObject` for conditional field requirements)
- https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-api-authorization?view=aspnetcore-8.0 (IEmailSender abstraction — established in us_011)
- https://github.com/BcryptNet/bcrypt.net (BCrypt.Net-Next 4.x — password hashing)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] POST /walkins with `createAccount: false` returns 201; `WalkInBooking.PatientId` is null in the database; queue row label reads "Walk-in — No account" (AC-001)
- [ ] POST /walkins with `createAccount: true` and a new email returns 201; a Patient user row exists in the database with a bcrypt-hashed password; `WalkInBooking.PatientId` references the new user (AC-002)
- [ ] POST /walkins with `createAccount: true` and an existing email returns 409; the response body contains the conflict message; no duplicate user record is created (AC-004)
- [ ] POST /walkins with `createAccount: true` and `email: ""` returns 400 with `{"email": "Email is required to create an account"}` (Edge: empty email)
- [ ] Simulate `IEmailSender` throwing on all 3 attempts; verify the booking is committed to the database, `CredentialsEmailFailed = true` in the response, and a structured error log entry appears in Seq (Edge: delivery failure)
- [ ] POST /walkins with `linkExistingAccountId` set to a valid patient ID returns 201; `WalkInBooking.PatientId` references the existing patient; no new user is created (AC-004 Yes path)
- [ ] Audit log contains `WalkInCreated` entry after every successful booking regardless of path taken (AC-001, AC-002; OWASP A09)

---

## Implementation Checklist
- [x] `WalkInsController` is decorated with `[Authorize(Roles = "Staff")]` at the class level — only authenticated staff members can create walk-in bookings (AC-001, AC-002; OWASP A01)
- [x] `CreateWalkInRequest.Validate` (IValidatableObject) returns a validation error for empty email when `CreateAccount = true` before the service layer is reached — avoids partial execution with missing data (Edge: empty email; OWASP A03)
- [x] `WalkInService.CreateAsync` calls `SaveChangesAsync` to commit the booking **before** attempting to send the credentials email — email failure never causes a booking rollback (AC-003; Edge: delivery failure — transactional isolation)
- [x] The 3-retry email envelope uses a simple loop with no `Thread.Sleep` / `Task.Delay` — the 30-second budget (AC-003) must not be consumed by artificial wait time; retry only on transient `SmtpException` (AC-003)
- [x] `DuplicateEmailException` is a domain exception caught at the controller level → 409; it is not allowed to propagate to the global exception middleware to avoid generic 500 responses (AC-004; OWASP A05)
- [x] Temporary password is generated using `RandomNumberGenerator.Fill` (cryptographically secure) — not `System.Random` (AC-002; OWASP A02 — weak random number generation)
- [x] `CredentialsEmailFailed = true` in the response is a non-blocking signal — the HTTP status code is still 201; the frontend decides how to display the warning (Edge: delivery failure — separation of concerns)
- [x] Audit log entry `WalkInAccountLinked` records both the `WalkInBookingId` and `LinkedPatientId` in the `Details` payload — enables traceability of manual-linking decisions (AC-004 Yes path; OWASP A09)
