# Task - TASK_001

## Requirement Reference
- **User Story:** us_008
- **Story Location:** .propel/context/tasks/EP-001/us_008/us_008.md
- **Acceptance Criteria:**
  - AC-001: `POST /auth/register` with valid payload returns HTTP 201 `{"userId": "<uuid>", "role": "Patient"}`; creates a row in `patients` with encrypted email; writes audit log entry with `ActionType: PatientRegistration`
  - AC-002: Duplicate email returns HTTP 409 `{"error": "An account with this email already exists"}` — no new row created
  - AC-003: Missing mandatory field returns HTTP 400 with `validationErrors: {"<fieldName>": "<message>"}` — no partial record persisted
- **Edge Cases:**
  - Invalid email format: HTTP 400 `{"email": "Must be a valid email address"}` before any database write
  - Date of birth in the future: HTTP 400 `{"dateOfBirth": "Date of birth cannot be in the future"}` before any database write
  - Insurance fields optional: if `insuranceProvider` and `insuranceId` are omitted or empty string, the patient row stores `NULL` for those columns — not an empty string

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
| Backend | ASP.NET Core Web API (C#) | .NET 8.0 | TR-002 (backend runtime); `AuthController` and request DTO live in the API project |
| Backend | EF Core | 8.x | TR-003 (persists `User` and `Patient` entities; duplicate email check via LINQ query) |
| Backend | Npgsql.EntityFrameworkCore.PostgreSQL | 8.x | TR-003 (PostgreSQL 15.3+ driver; PHI value converters from us_006 transparently encrypt stored email) |

---

## Task Overview

Implement the `POST /auth/register` endpoint in `AuthController`. The endpoint validates the incoming `RegisterPatientRequest` DTO using DataAnnotations and a custom DOB future-date guard, checks for duplicate email in `patients`, creates a `User` (role = `Patient`, GUID PK) and `Patient` entity (PHI encrypted via EF Core value converters from us_006), writes a `PatientRegistration` audit log entry, and returns HTTP 201 with `{userId, role}`. All validation failures must fire before any database write.

---

## Dependent Tasks
- task_001 (us_003) — `AuthController` skeleton and API project scaffold must exist
- task_001 (us_005) — `User` and `Patient` entity classes and `AppDbContext` must exist
- task_002 (us_006) — `IPhiEncryptionService` EF Core value converters must be configured on PHI columns so encryption is transparent
- task_004 (us_004) — `IAuditLogger` must be registered so the controller can write the audit entry

---

## Impacted Components
- `src/api/Controllers/AuthController.cs` — add `RegisterAsync` action method for `POST /auth/register`
- `src/api/DTOs/RegisterPatientRequest.cs` — new request DTO with DataAnnotations
- `src/api/DTOs/RegisterPatientResponse.cs` — new response DTO `{userId, role}`

---

## Implementation Plan
1. Create `src/api/DTOs/RegisterPatientRequest.cs` with properties: `Name` (`[Required]`), `DateOfBirth` (`[Required]`), `Email` (`[Required, EmailAddress]`), `Phone` (`[Required]`), `InsuranceProvider` (nullable, no attribute), `InsuranceId` (nullable, no attribute); normalise empty-string insurance fields to `null` in the controller before persisting (Edge: insurance optional)
2. Add a custom validation step in `RegisterAsync` for DOB: `if (request.DateOfBirth > DateOnly.FromDateTime(DateTime.UtcNow)) return BadRequest(new { validationErrors = new { dateOfBirth = "Date of birth cannot be in the future" } });` — executed after `ModelState.IsValid` check so format/required errors fire first (Edge: DOB future)
3. Add `AuthController.RegisterAsync([FromBody] RegisterPatientRequest request)`: return `BadRequest(ModelState)` formatted as `{validationErrors: {...}}` if `!ModelState.IsValid` (AC-003, Edge: email format)
4. Query `AppDbContext.Patients.AnyAsync(p => p.Email == request.Email)` — but note PHI value converters encrypt on write, so the duplicate check must use a raw or decrypted comparison; use `AppDbContext.Patients.AsEnumerable().Any(p => decrypt(p.Email) == request.Email)` or an index on the hashed email; simplest safe approach: query encrypted value by encrypting the lookup email with `IPhiEncryptionService.Encrypt(request.Email)` (AC-002)
5. If duplicate found, return `Conflict(new { error = "An account with this email already exists" })` (AC-002)
6. Create `User` entity with `Id = Guid.NewGuid()`, `Role = "Patient"`, `Email = request.Email`, `CreatedAt = DateTime.UtcNow`; create `Patient` entity referencing the new `UserId` — EF Core value converters automatically encrypt PHI columns on `SaveChangesAsync` (AC-001)
7. After successful `SaveChangesAsync`, call `_auditLogger.Log(newUser.Id.ToString(), "PatientRegistration", newPatient.Id.ToString())` and return `CreatedAtAction(...)` with HTTP 201 body `new RegisterPatientResponse { UserId = newUser.Id, Role = "Patient" }` (AC-001)

---

## Current Project State
```
src/
└── api/
    ├── Controllers/
    │   └── AuthController.cs          (MODIFY — add RegisterAsync action)
    └── DTOs/
        ├── RegisterPatientRequest.cs  (CREATE)
        └── RegisterPatientResponse.cs (CREATE)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | src/api/Controllers/AuthController.cs | Add `[HttpPost("register")] RegisterAsync` with full validation, duplicate check, entity creation, audit log, and 201 response |
| CREATE | src/api/DTOs/RegisterPatientRequest.cs | Request DTO with DataAnnotations for required fields, email format, nullable insurance fields |
| CREATE | src/api/DTOs/RegisterPatientResponse.cs | Response DTO `{ Guid UserId; string Role; }` |

---

## External References
- https://learn.microsoft.com/en-us/aspnet/core/mvc/models/validation?view=aspnetcore-8.0 (ASP.NET Core 8 model validation — DataAnnotations, ModelState)
- https://learn.microsoft.com/en-us/aspnet/core/web-api/?view=aspnetcore-8.0#automatic-http-400-responses (ASP.NET Core 8 automatic 400 responses from `[ApiController]`)
- https://learn.microsoft.com/en-us/dotnet/api/system.dateonly (DateOnly in .NET 8 — used for `DateOfBirth` future-date guard)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] `POST /auth/register` with all valid fields → HTTP 201 with `userId` (GUID) and `role: "Patient"` (AC-001)
- [ ] Query `SELECT email FROM patients WHERE id = <id>` directly — must return bytea ciphertext, not plaintext (AC-001 — PHI encryption via us_006 value converters)
- [ ] `POST /auth/register` twice with same email → second call returns HTTP 409 (AC-002)
- [ ] `POST /auth/register` without `dateOfBirth` → HTTP 400 with `validationErrors.dateOfBirth` set (AC-003)
- [ ] `POST /auth/register` with `email = "notanemail"` → HTTP 400 with `validationErrors.email` set (Edge: invalid email format)
- [ ] `POST /auth/register` with `dateOfBirth = <tomorrow>` → HTTP 400 with `validationErrors.dateOfBirth = "Date of birth cannot be in the future"` (Edge: DOB future)
- [ ] `POST /auth/register` without `insuranceProvider` → HTTP 201; `SELECT insurance_provider FROM patients WHERE id = <id>` returns NULL (Edge: insurance optional)

---

## Implementation Checklist
- [ ] `RegisterPatientRequest` has `[Required]` on `Name`, `DateOfBirth`, `Email`, `Phone`; `[EmailAddress]` on `Email`; `InsuranceProvider` and `InsuranceId` are nullable with no `[Required]` attribute (AC-003; Edge: insurance optional)
- [ ] `ModelState.IsValid` is checked first; if invalid, `BadRequest` is returned with a `validationErrors` keyed response — no database query occurs before this check (AC-003; Edge: email format — OWASP A03 validation at boundary)
- [ ] DOB future-date guard fires after `ModelState.IsValid` passes, returns HTTP 400 with `{"dateOfBirth": "Date of birth cannot be in the future"}` if `request.DateOfBirth > DateOnly.FromDateTime(DateTime.UtcNow)` (Edge: DOB future)
- [ ] Duplicate email lookup encrypts the query email using `IPhiEncryptionService.Encrypt(request.Email)` and matches against the encrypted `email` column — avoids full-table decryption scan (AC-002; performance)
- [ ] Empty-string `InsuranceProvider` and `InsuranceId` are normalised to `null` before entity creation so the column stores `NULL` not an encrypted empty string (Edge: insurance optional)
- [ ] `IAuditLogger.Log(userId, "PatientRegistration", patientId)` is called after `SaveChangesAsync` succeeds — not before, to avoid audit entries for failed registrations (AC-001)
- [ ] HTTP 201 response body is `{"userId": "<guid>", "role": "Patient"}` — no sensitive PHI fields are included in the response (AC-001; OWASP A02 — PHI never in API response)
