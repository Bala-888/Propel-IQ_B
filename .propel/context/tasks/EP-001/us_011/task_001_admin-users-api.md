# Task - TASK_001

## Requirement Reference
- **User Story:** us_011
- **Story Location:** .propel/context/tasks/EP-001/us_011/us_011.md
- **Acceptance Criteria:**
  - AC-001: `POST /admin/users` creates a new user record, writes audit log entry `ActionType: UserCreated`, and sends a welcome email containing temporary credentials
  - AC-002: `PATCH /admin/users/<id>` updates the `role` field; audit log entry `ActionType: RoleChanged` with `{"from": "...", "to": "..."}` payload is written; the change is visible immediately after the response
  - AC-003: `PATCH /admin/users/<id>` sets `isActive = false`; the deactivated user can no longer authenticate (API returns HTTP 401); audit log entry `ActionType: UserDeactivated` is written
  - AC-005: If a `POST /admin/users` request contains an email that already exists, the API returns HTTP 409 and the body contains `"A user with this email already exists"` — the user record is not created
- **Edge Cases:**
  - Reactivating a deactivated user: `PATCH /admin/users/<id>` with `isActive = true` must re-enable authentication on the next login attempt
  - Invalid role value: if `role` in a PATCH body is not one of `{Patient, Staff, Admin}`, the API must return HTTP 400 with `{"role": "Invalid role value"}` without modifying the record

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
| Backend | ASP.NET Core Web API (C#) | .NET 8.0 | TR-002 (API layer; `AdminUsersController` with `[Authorize(Roles = "Admin")]`) |
| Backend | EF Core + Npgsql.EntityFrameworkCore.PostgreSQL | 8.x | TR-002 (ORM for `ApplicationUser` entity; duplicate email check via `AnyAsync`) |
| Backend | BCrypt.Net-Next | 4.x | TR-002 (temporary password hashing before storage; same hasher established in us_009) |
| Backend | Microsoft.AspNetCore.Authentication.JwtBearer | .NET 8.0 built-in | TR-002 (`[Authorize(Roles = "Admin")]` requires the JWT `role` claim; established in us_009) |
| Backend | Microsoft.AspNetCore.Identity (IEmailSender abstraction) | .NET 8.0 built-in | TR-002 (`IEmailSender<ApplicationUser>` for welcome email; SMTP configured via `appsettings`) |

---

## Task Overview

Implement the `AdminUsersController` exposing `POST /admin/users` and `PATCH /admin/users/<id>` secured with `[Authorize(Roles = "Admin")]`. The POST endpoint creates a new user with a bcrypt-hashed temporary password, writes a `UserCreated` audit log entry, and dispatches a welcome email via `IEmailSender`. The PATCH endpoint handles three sub-operations via a discriminated request body: role assignment (audit `RoleChanged`), deactivation (`isActive = false`, audit `UserDeactivated`), and reactivation (`isActive = true`). Deactivated accounts are blocked at the login service level. Duplicate email returns 409; invalid role value returns 400.

---

## Dependent Tasks
- task_002 (us_009) — JWT Bearer middleware and `[Authorize(Roles = "Admin")]` enforcement must be in place; `Admin` role claim must be issued in the JWT
- task_001 (us_005) — `ApplicationUser` EF Core entity with `Email`, `Role`, `IsActive`, `PasswordHash` columns must exist in the database schema

---

## Impacted Components
- `src/api/Controllers/AdminUsersController.cs` — new controller: `POST /admin/users`, `PATCH /admin/users/{id}`
- `src/api/DTOs/CreateUserRequest.cs` — new DTO: `Name`, `Email`, `Role`
- `src/api/DTOs/PatchUserRequest.cs` — new DTO: `Role?`, `IsActive?` (at least one must be non-null)
- `src/api/Services/IUserManagementService.cs` — new interface for user CRUD operations
- `src/api/Services/UserManagementService.cs` — new service implementation
- `src/api/Controllers/AuthController.cs` — modified: add `isActive` check before token issuance → 401 if false
- `src/api/Services/AuditLogService.cs` — modified: add `UserCreated`, `RoleChanged`, `UserDeactivated` action type handling

---

## Implementation Plan
1. Create `CreateUserRequest` DTO with `[Required] string Name`, `[Required][EmailAddress] string Email`, `[Required] string Role`; add `PatchUserRequest` with nullable `string? Role` and `bool? IsActive`; add model-level validation that rejects role values outside `{Patient, Staff, Admin}` using a custom `[AllowedValues]` attribute or `IValidatableObject` (AC-001, AC-005, Edge: invalid role)
2. Create `IUserManagementService` and `UserManagementService`: `CreateAsync(CreateUserRequest)` — check for existing email via `AnyAsync` (throw `DuplicateEmailException` on conflict), generate 12-char random temporary password, hash via `BCrypt.HashPassword`, insert `ApplicationUser`, call `IAuditLogService.LogAsync(UserCreated, userId)`, return the created user's ID; `PatchAsync(Guid id, PatchUserRequest)` — load entity, apply role/isActive changes, call audit log with appropriate action type, persist (AC-001, AC-002, AC-003, Edge: reactivation)
3. Implement `AdminUsersController` with `[ApiController][Route("admin/users")][Authorize(Roles = "Admin")]`; `POST /` maps to `CreateAsync` — returns 201 on success, 409 with message body on `DuplicateEmailException`, 400 on model validation failure; `PATCH /{id}` maps to `PatchAsync` — returns 200 on success, 400 on invalid role, 404 if user not found (AC-001, AC-002, AC-003, AC-005)
4. In `AuthController.LoginAsync`, after validating credentials and before issuing the JWT, query the user's `isActive` flag; return `401 Unauthorized` with the same generic message `"Invalid credentials."` if `isActive = false` — OWASP A07: do not reveal account-disabled status in the response body (AC-003; Edge: reactivation — re-check on every login attempt)
5. Register `IUserManagementService` → `UserManagementService` in `Program.cs` via `services.AddScoped`; configure `IEmailSender` with SMTP settings from `appsettings.json` (`Email:SmtpHost`, `Email:SmtpPort`, `Email:FromAddress`); OWASP A02: SMTP credentials must come from environment variables, never hardcoded in source (AC-001)
6. Dispatch welcome email from `UserManagementService.CreateAsync` after successful DB commit: subject "Welcome — your account has been created", body includes `Name`, `Email`, and the temporary plaintext password (sent only once at creation); the temporary password must be marked as requiring change on first login via a `MustChangePassword` boolean column on `ApplicationUser` (AC-001)
7. Extend `IAuditLogService` and `AuditLogService` with `RoleChanged` action type: store `{"from": previousRole, "to": newRole}` as the log entry payload; add `UserDeactivated` action type (AC-002, AC-003)

---

## Current Project State
```
src/
└── api/
    ├── Controllers/
    │   ├── AuthController.cs                        (MODIFY — add isActive check before JWT issuance)
    │   └── AdminUsersController.cs                  (CREATE)
    ├── DTOs/
    │   ├── CreateUserRequest.cs                     (CREATE)
    │   └── PatchUserRequest.cs                      (CREATE)
    ├── Services/
    │   ├── IUserManagementService.cs                (CREATE)
    │   ├── UserManagementService.cs                 (CREATE)
    │   └── AuditLogService.cs                       (MODIFY — UserCreated, RoleChanged, UserDeactivated)
    └── Program.cs                                   (MODIFY — register IUserManagementService + IEmailSender)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | src/api/Controllers/AdminUsersController.cs | POST /admin/users + PATCH /admin/users/{id}; Authorize(Roles="Admin") |
| CREATE | src/api/DTOs/CreateUserRequest.cs | Validated DTO for user creation (Name, Email, Role) |
| CREATE | src/api/DTOs/PatchUserRequest.cs | Nullable DTO for partial user update (Role?, IsActive?) |
| CREATE | src/api/Services/IUserManagementService.cs | Interface: CreateAsync + PatchAsync |
| CREATE | src/api/Services/UserManagementService.cs | Implementation with bcrypt, duplicate check, audit log, email dispatch |
| MODIFY | src/api/Controllers/AuthController.cs | Add isActive check → 401 before token issuance |
| MODIFY | src/api/Services/AuditLogService.cs | Add RoleChanged (with payload) and UserDeactivated action types |
| MODIFY | src/api/Program.cs | Register IUserManagementService + IEmailSender SMTP configuration |

---

## External References
- https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-api-authorization?view=aspnetcore-8.0 (ASP.NET Core 8 Identity — IEmailSender abstraction)
- https://learn.microsoft.com/en-us/aspnet/core/web-api/?view=aspnetcore-8.0 (ASP.NET Core 8 Web API — controller attributes and model validation)
- https://github.com/BcryptNet/bcrypt.net (BCrypt.Net-Next 4.x — password hashing reference)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] POST /admin/users with valid payload returns 201 and the user appears in the database with a bcrypt-hashed password, not the plaintext temporary password (AC-001)
- [ ] POST /admin/users with a duplicate email returns 409 and the body contains "A user with this email already exists"; no second record created (AC-005)
- [ ] PATCH /admin/users/<id> with `{"role": "Staff"}` on a Patient user returns 200; querying the user entity shows `Role = "Staff"`; audit log contains `ActionType: RoleChanged` with `{"from": "Patient", "to": "Staff"}` (AC-002)
- [ ] PATCH /admin/users/<id> with `{"isActive": false}` returns 200; subsequent POST /auth/login with that user's credentials returns 401 (AC-003)
- [ ] PATCH /admin/users/<id> with `{"isActive": true}` on a deactivated user returns 200; subsequent POST /auth/login with valid credentials returns 200 with JWT (Edge: reactivation)
- [ ] PATCH /admin/users/<id> with `{"role": "SuperAdmin"}` returns 400 with `{"role": "Invalid role value"}` and the user's role is unchanged (Edge: invalid role)
- [ ] POST /auth/login for a deactivated account returns 401 with the same message as an invalid-credentials response — no information leak about account status (AC-003; OWASP A07)

---

## Implementation Checklist
- [x] `AdminUsersController` is decorated with `[Authorize(Roles = "Admin")]` at the class level — all sub-routes inherit the restriction without per-method decoration drift (AC-001, AC-002, AC-003; OWASP A01)
- [x] `UserManagementService.CreateAsync` performs the duplicate email check via `AnyAsync` before inserting; returns HTTP 409 with a safe message — does not reveal whether the existing account is active or deactivated (AC-005; OWASP A07)
- [x] `PatchUserRequest.Role`, when present, is validated against `{"Patient", "Staff", "Admin"}` before `SaveChangesAsync` is called — ensures invalid role payloads return 400 without a partial DB write (Edge: invalid role; OWASP A03)
- [x] `AuthController.LoginAsync` checks `user.IsActive` after password verification and before JWT issuance; returns the identical 401 message as an invalid-password response — prevents account-status enumeration (AC-003; OWASP A07)
- [x] `IEmailSender` SMTP configuration (`SmtpHost`, `SmtpPort`, `FromAddress`, credentials) is bound from environment variables only — never committed to source code or `appsettings.json` in plain text (AC-001; OWASP A02)
- [x] `ApplicationUser` gains a `MustChangePassword` boolean column (default `true` on creation); the welcome email is sent only once per account creation, not on re-activation (AC-001 — credential lifecycle)
- [x] Audit log entries for `RoleChanged` store the `{"from": previousRole, "to": newRole}` JSON payload in the log record's `Details` column — enables compliance traceability (AC-002; OWASP A09)
- [x] All `AdminUsersController` action methods return `ActionResult<T>` and do not expose internal exception messages in the response body — unhandled exceptions are caught by the global exception middleware (OWASP A05 — security misconfiguration)
