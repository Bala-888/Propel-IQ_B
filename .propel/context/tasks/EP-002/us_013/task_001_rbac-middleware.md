# Task - TASK_001

## Requirement Reference
- **User Story:** us_013
- **Story Location:** .propel/context/tasks/EP-002/us_013/us_013.md
- **Acceptance Criteria:**
  - AC-001: A Patient JWT calling `GET /dashboard/staff` receives HTTP 403 with body `{"error": "Access denied. Insufficient role."}`; no patient data is returned
  - AC-002: An unauthenticated request to any protected endpoint receives HTTP 401 with body `{"error": "Authentication required."}`; the response directs the client to `/login`
  - AC-003: Patient A's JWT calling `GET /patients/<B_id>/view` receives HTTP 403 with `{"error": "Access denied. You can only access your own records."}`; no data from Patient B is returned
  - AC-004: A Staff JWT calling `GET /admin/metrics` receives HTTP 403 and an audit log entry with `ActionType: UnauthorizedAccess`, `ResourceType: AdminMetrics`, `ActorRole: Staff` is written
  - AC-005: When the same source IP accumulates three HTTP 403 responses within 10 minutes, an `AdminNotification` row with `AlertType: RepeatedUnauthorizedAccess` and `SourceIp` is inserted
- **Edge Cases:**
  - Token expiry ordering: if the access token has expired but the RBAC check would pass, the middleware must emit HTTP 401 (not 403) so the client triggers the correct token refresh flow
  - Role changed after token issued: if a user's DB role differs from the role claim in the active JWT, the API must return HTTP 401 on the next request — not HTTP 403 — so the client is prompted to re-authenticate with fresh claims

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
| Backend | ASP.NET Core Web API (C#) | .NET 8.0 | TR-002 (`IAuthorizationMiddlewareResultHandler`, `IClaimsTransformation`, `ActionFilterAttribute`) |
| Backend | Microsoft.AspNetCore.Authentication.JwtBearer | .NET 8.0 built-in | TR-002 (JWT Bearer events: `OnChallenge` → 401, `OnForbidden` → 403; established in us_009) |
| Backend | Microsoft.Extensions.Caching.Distributed | .NET 8.0 built-in | TR-002 (`IDistributedCache` for repeated-403 IP counter; pattern established in us_010) |
| Backend | EF Core + Npgsql.EntityFrameworkCore.PostgreSQL | 8.x | TR-002 (DB role lookup in `IClaimsTransformation`; `IAdminNotificationRepository` insert) |

---

## Task Overview

Implement a hardened RBAC enforcement layer across the API. A custom `IAuthorizationMiddlewareResultHandler` emits JSON-formatted 401 and 403 responses (AC-001, AC-002). An `IOwnershipAuthorizationService` enforces per-record access for patient endpoints (AC-003). All 403 events are written to the audit log (AC-004). An `IDistributedCache`-backed counter fires an `AdminNotification` insert when the same IP reaches three 403s in ten minutes (AC-005). An `IClaimsTransformation` implementation validates the JWT role claim against the live DB role and returns 401 on mismatch to prevent stale-role access (Edge). Token expiry is resolved at the `JwtBearerOptions.Events` level so expired tokens yield 401 before RBAC can produce a 403 (Edge).

---

## Dependent Tasks
- task_001 (us_009) — JWT Bearer middleware and `[Authorize]` infrastructure must be in place
- task_002 (us_006) — `IAuditLogService` append-only log must be available for 403 audit entries
- task_002 (us_013) — `IAdminNotificationRepository` and the `AdminNotification` DB table must exist before the repeated-403 counter can insert records

---

## Impacted Components
- `src/api/Infrastructure/Auth/JsonAuthorizationMiddlewareResultHandler.cs` — new: custom 401/403 JSON responses
- `src/api/Infrastructure/Auth/DatabaseRoleClaimsTransformation.cs` — new: validates JWT role claim vs DB on every authenticated request
- `src/api/Infrastructure/Auth/OwnershipAuthorizationService.cs` — new: `IOwnershipAuthorizationService` with `CanAccessPatientRecordAsync`
- `src/api/Infrastructure/Auth/RepeatedUnauthorizedAccessTracker.cs` — new: `IDistributedCache`-backed 403-count tracker
- `src/api/Constants/Roles.cs` — new: static role constants `Patient`, `Staff`, `Admin`; role matrix XML comments
- `src/api/Controllers/PatientsController.cs` — modified: call `IOwnershipAuthorizationService` before returning record data
- `src/api/Program.cs` — modified: register all new services; configure `JwtBearerOptions.Events`

---

## Implementation Plan
1. Create `Roles.cs` constants class with `public const string Patient = "Patient"`, `Staff = "Staff"`, `Admin = "Admin"`; add XML doc comments describing the role matrix (Patient: own records; Staff: queue + patient data; Admin: all); update all existing `[Authorize(Roles = "...")]` decorators across controllers to use `Roles.*` constants (AC-001, AC-004 — standardization; removes magic strings)
2. Implement `JsonAuthorizationMiddlewareResultHandler : IAuthorizationMiddlewareResultHandler`: on `AuthorizationResult.Failure` where the user is not authenticated → write `{"error": "Authentication required."}` with `WWW-Authenticate: Bearer` header and HTTP 401; where the user is authenticated but forbidden → write `{"error": "Access denied. Insufficient role."}` with HTTP 403; call `IAuditLogService.LogAsync(UnauthorizedAccess, ...)` with `ResourceType` from route data and `ActorRole` from claims before writing the 403 response (AC-001, AC-002, AC-004)
3. Implement `RepeatedUnauthorizedAccessTracker` with method `TrackAndAlertAsync(string sourceIp)`: cache key `unauth_403:{sourceIp}`; `GetStringAsync` → parse count; increment; `SetStringAsync` with `AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)`; if new count ≥ 3 → call `IAdminNotificationRepository.InsertAsync(new AdminNotification { AlertType = "RepeatedUnauthorizedAccess", SourceIp = sourceIp, CreatedAt = DateTime.UtcNow })` — called from `JsonAuthorizationMiddlewareResultHandler` on every 403 (AC-005)
4. Implement `IOwnershipAuthorizationService` with `CanAccessPatientRecordAsync(ClaimsPrincipal user, Guid requestedPatientId) → bool`: extracts the patient sub-claim from the JWT; returns `true` if `sub == requestedPatientId.ToString()` or if role is `Staff` or `Admin`; used in `PatientsController` — if `false`, return `ObjectResult(new { error = "Access denied. You can only access your own records." }, 403)` (AC-003)
5. Implement `DatabaseRoleClaimsTransformation : IClaimsTransformation` with `TransformAsync(ClaimsPrincipal principal)`: only runs when the principal is authenticated; queries `ApplicationUser` by sub-claim ID; if `dbUser.Role != jwtRoleClaim` → return a new `ClaimsPrincipal` with all claims cleared so downstream `[Authorize]` emits 401 instead of 403; cache the DB lookup result in `IMemoryCache` with a 60-second sliding expiration to avoid per-request DB hits (Edge: role changed after token)
6. Configure `JwtBearerOptions.Events` in `Program.cs`: `OnChallenge` handler sets `context.HandleResponse()`, writes `{"error": "Authentication required."}` with `401` — this fires **before** the authorization policy evaluation, ensuring expired-token responses are 401 not 403 (Edge: token expiry ordering); `OnForbidden` is handled by `JsonAuthorizationMiddlewareResultHandler` (AC-002)
7. Register all new services in `Program.cs`: `services.AddSingleton<IAuthorizationMiddlewareResultHandler, JsonAuthorizationMiddlewareResultHandler>()`, `AddScoped<IOwnershipAuthorizationService, OwnershipAuthorizationService>()`, `AddTransient<IClaimsTransformation, DatabaseRoleClaimsTransformation>()`, `AddScoped<RepeatedUnauthorizedAccessTracker>()`; configure `AddDistributedMemoryCache()` for dev with Redis swap note (AC-001–005)

---

## Current Project State
```
src/
└── api/
    ├── Infrastructure/
    │   └── Auth/
    │       ├── JsonAuthorizationMiddlewareResultHandler.cs   (CREATE)
    │       ├── DatabaseRoleClaimsTransformation.cs           (CREATE)
    │       ├── OwnershipAuthorizationService.cs              (CREATE)
    │       └── RepeatedUnauthorizedAccessTracker.cs          (CREATE)
    ├── Constants/
    │   └── Roles.cs                                          (CREATE)
    ├── Controllers/
    │   └── PatientsController.cs                             (MODIFY — add ownership check)
    └── Program.cs                                            (MODIFY — register services + JwtBearerOptions.Events)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | src/api/Infrastructure/Auth/JsonAuthorizationMiddlewareResultHandler.cs | JSON 401/403 responses + audit log on 403 + 403-counter trigger |
| CREATE | src/api/Infrastructure/Auth/DatabaseRoleClaimsTransformation.cs | Validate JWT role vs DB role → clear claims on mismatch |
| CREATE | src/api/Infrastructure/Auth/OwnershipAuthorizationService.cs | Per-record access check for patient endpoints |
| CREATE | src/api/Infrastructure/Auth/RepeatedUnauthorizedAccessTracker.cs | IDistributedCache-backed 403 counter + AdminNotification insert |
| CREATE | src/api/Constants/Roles.cs | Role string constants + role matrix documentation |
| MODIFY | src/api/Controllers/PatientsController.cs | Call IOwnershipAuthorizationService; return 403 on own-record violation |
| MODIFY | src/api/Program.cs | Register auth services; configure JwtBearerOptions.Events for OnChallenge |

---

## External References
- https://learn.microsoft.com/en-us/aspnet/core/security/authorization/customizingauthorizationmiddlewareresponse?view=aspnetcore-8.0 (ASP.NET Core 8 — IAuthorizationMiddlewareResultHandler)
- https://learn.microsoft.com/en-us/aspnet/core/security/authentication/claims?view=aspnetcore-8.0#extend-or-add-custom-claims-using-iclaimstransformation (IClaimsTransformation — per-request claims enrichment)
- https://learn.microsoft.com/en-us/aspnet/core/security/authentication/jwt-authn?view=aspnetcore-8.0 (JwtBearerOptions.Events.OnChallenge — custom 401 response)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] Authenticate as Patient; call `GET /dashboard/staff`; verify HTTP 403 with body `{"error": "Access denied. Insufficient role."}` and no patient data in the response (AC-001)
- [ ] Call any protected endpoint with no `Authorization` header; verify HTTP 401 with body `{"error": "Authentication required."}` (AC-002)
- [ ] Authenticate as Patient A; call `GET /patients/<B_id>/view`; verify HTTP 403 with body `{"error": "Access denied. You can only access your own records."}` (AC-003)
- [ ] Authenticate as Staff; call `GET /admin/metrics`; verify HTTP 403 and an audit log row with `ActionType = UnauthorizedAccess`, `ResourceType = AdminMetrics`, `ActorRole = Staff` (AC-004)
- [ ] Send three 403-generating requests from the same IP within 10 minutes; verify a new `AdminNotification` row with `AlertType = RepeatedUnauthorizedAccess` and the correct `SourceIp` is present (AC-005)
- [ ] Issue a token for a Staff user; update the DB role to Patient; send a subsequent API request with the old token; verify HTTP 401 (not 403) is returned (Edge: role changed after token)
- [ ] Generate an expired token for a Patient user who would pass the RBAC check; send the request; verify HTTP 401 is returned (not 403) (Edge: token expiry ordering)

---

## Implementation Checklist
- [ ] `JsonAuthorizationMiddlewareResultHandler` calls `IAuditLogService` **before** writing the HTTP response body — the audit entry is persisted even if the response stream write fails (AC-004; OWASP A09)
- [ ] `RepeatedUnauthorizedAccessTracker.TrackAndAlertAsync` uses `IDistributedCache` with `AbsoluteExpirationRelativeToNow = 10 minutes` — the counter resets automatically per the AC-005 sliding window requirement (AC-005)
- [ ] `DatabaseRoleClaimsTransformation` caches the DB role lookup in `IMemoryCache` with a 60-second sliding expiration — avoids a DB hit on every request while still detecting role changes within a reasonable window (Edge: role changed after token; performance)
- [ ] `JwtBearerOptions.Events.OnChallenge` sets `context.HandleResponse()` before writing the 401 body — prevents the default Bearer challenge header from overriding the custom JSON body (Edge: token expiry ordering; AC-002)
- [ ] `OwnershipAuthorizationService.CanAccessPatientRecordAsync` bypasses the ownership check when the caller's role is `Staff` or `Admin` — staff must be able to access any patient record in the queue (AC-003 — role bypass; OWASP A01)
- [ ] All error response bodies use the exact string literals specified in the ACs — no deviation in casing or punctuation; tested with exact-match assertions in the validation strategy (AC-001, AC-002, AC-003; contract stability)
- [ ] `Roles.cs` constants are referenced via `[Authorize(Roles = Roles.Admin)]` throughout all controllers; no magic role strings remain in the codebase — verified by a `grep` step in the CI pipeline (AC-001, AC-004; OWASP A01 — consistent access control)
- [ ] `RepeatedUnauthorizedAccessTracker` is invoked on every HTTP 403, including those from `OwnershipAuthorizationService` — own-record violations count toward the IP threshold (AC-003, AC-005 — comprehensive 403 coverage)
