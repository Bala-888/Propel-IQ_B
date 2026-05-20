# Task - TASK_002

## Requirement Reference
- **User Story:** us_009
- **Story Location:** .propel/context/tasks/EP-001/us_009/us_009.md
- **Acceptance Criteria:**
  - AC-001: `POST /auth/login` returns HTTP 200 with `{accessToken, refreshToken, role}`; JWT `role` claim equals the user's role; `exp` claim equals `iat + 900` seconds (15 minutes)
  - AC-005: Invalid credentials (non-existent email or wrong password) return HTTP 401 `{"error": "Invalid email or password"}` — identical message for both cases; audit log entry with `ActionType: LoginFailure` is written
  - AC-006: `isActive = false` user returns HTTP 401 `{"error": "Account is inactive. Please contact your administrator."}`
- **Edge Cases:**
  - JWT token used after expiry: the API must return HTTP 401 `{"error": "Token expired"}` — not HTTP 403; `ValidateLifetime = true` in JWT Bearer middleware and a custom `OnChallenge` handler differentiate expired vs. invalid token responses
  - Refresh token replay attack: `POST /auth/refresh` with an already-rotated token must mark ALL tokens with the same `family_id` as revoked and return HTTP 401 `{"error": "Refresh token reuse detected"}`

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
| Backend | ASP.NET Core Web API (C#) | .NET 8.0 | TR-002 (backend runtime); `AuthController` login and refresh endpoints |
| Backend | Microsoft.AspNetCore.Authentication.JwtBearer | 8.x (built-in) | TR-005 (JWT Bearer middleware; `ValidateLifetime=true` for expiry enforcement; `OnChallenge` event for custom 401 body) |
| Backend | System.IdentityModel.Tokens.Jwt | 7.x (transitive via JwtBearer) | TR-005 (JWT generation — `JwtSecurityTokenHandler`, `SigningCredentials` with HMAC-SHA256) |
| Backend | BCrypt.Net-Next | 4.x (latest stable) | TR-005 (bcrypt password hash verification in login flow) |
| Backend | EF Core | 8.x | TR-003 (User lookup and RefreshToken persistence via AppDbContext) |

---

## Task Overview

Implement `POST /auth/login` in `AuthController`: validate credentials via bcrypt, enforce account-active check, write audit log on any failure, generate a HMAC-SHA256 JWT with `role` and 15-minute expiry, issue a refresh token persisted to the `refresh_tokens` table (task_001 schema), and return HTTP 200. Implement `POST /auth/refresh` with rotation logic and family-level revocation. Configure JWT Bearer middleware in `Program.cs` with `ValidateLifetime = true` and a custom `OnChallenge` handler that returns `{"error": "Token expired"}` when the failure reason is expiry.

---

## Dependent Tasks
- task_001 (us_009) — `refresh_tokens` table with `FamilyId` column must exist before the refresh endpoint can persist or revoke tokens
- task_001 (us_003) — `AuthController` skeleton and `Program.cs` must exist
- task_003 (us_004) — `IAuditLogger` registered in DI for writing `LoginFailure` audit events

---

## Impacted Components
- `src/api/Controllers/AuthController.cs` — add `LoginAsync` and `RefreshAsync` action methods
- `src/api/DTOs/LoginRequest.cs` — new request DTO
- `src/api/DTOs/LoginResponse.cs` — new response DTO
- `src/api/DTOs/RefreshRequest.cs` — new request DTO
- `src/api/Program.cs` — add JWT Bearer middleware configuration with custom `OnChallenge`
- `src/api/Services/ITokenService.cs` — new interface for JWT generation
- `src/api/Services/TokenService.cs` — new implementation

---

## Implementation Plan
1. Add `BCrypt.Net-Next` 4.x NuGet to `Api.csproj`; create `ITokenService` / `TokenService` with `GenerateAccessToken(User user)` returning a signed JWT (claims: `sub = userId`, `role = user.Role`, `iat`, `exp = UtcNow + 15min`) using HMAC-SHA256 with the `JWT_SECRET` env var key; and `GenerateRefreshToken()` returning a `(string token, Guid familyId)` tuple
2. Configure JWT Bearer middleware in `Program.cs`: `builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(opts => { opts.TokenValidationParameters = new() { ValidateLifetime = true, ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)), ValidateAudience = false, ValidateIssuer = false }; opts.Events = new JwtBearerEvents { OnChallenge = ctx => { ctx.HandleResponse(); ctx.Response.StatusCode = 401; return ctx.Response.WriteAsJsonAsync(ctx.AuthenticateFailure?.GetType().Name.Contains("Expired") == true ? new { error = "Token expired" } : new { error = "Unauthorized" }); } }; })` (Edge: JWT expiry → 401 not 403)
3. Create `LoginRequest` DTO (`Email`, `Password` — both `[Required]`) and `LoginResponse` DTO (`AccessToken`, `RefreshToken`, `Role`)
4. `AuthController.LoginAsync` (POST /auth/login): query `Users` by email (case-insensitive); if not found → write audit log `LoginFailure` → return 401 `{"error": "Invalid email or password"}`; if password fails `BCrypt.Verify(request.Password, user.PasswordHash)` → write audit log `LoginFailure` → return same 401 (AC-005; OWASP A07 — identical response prevents email enumeration)
5. Check `user.IsActive`; if `false` → return 401 `{"error": "Account is inactive. Please contact your administrator."}` — no audit log write for inactive account (AC-006; AC-005 audit log is for credential failures only)
6. On success: call `TokenService.GenerateAccessToken(user)` and `TokenService.GenerateRefreshToken()`; persist a new `RefreshToken` entity `{ UserId = user.Id, Token = refreshToken, FamilyId = newFamilyId, ExpiresAt = UtcNow + 7days, IsRevoked = false }`; return HTTP 200 `{ accessToken, refreshToken, role }` (AC-001)
7. `AuthController.RefreshAsync` (POST /auth/refresh): look up `RefreshToken` by token value; if `IsRevoked = true` → update all rows with same `FamilyId` to `IsRevoked = true` → return 401 `{"error": "Refresh token reuse detected"}`; if `ExpiresAt < UtcNow` → return 401; else mark current token as revoked, issue new JWT + new `RefreshToken` with same `FamilyId`, return 200 (Edge: replay attack)

---

## Current Project State
```
src/
└── api/
    ├── Api.csproj                      (MODIFY — add BCrypt.Net-Next)
    ├── Program.cs                      (MODIFY — add JWT Bearer middleware)
    ├── Controllers/
    │   └── AuthController.cs           (MODIFY — add LoginAsync, RefreshAsync)
    ├── DTOs/
    │   ├── LoginRequest.cs             (CREATE)
    │   ├── LoginResponse.cs            (CREATE)
    │   └── RefreshRequest.cs           (CREATE)
    └── Services/
        ├── ITokenService.cs            (CREATE)
        └── TokenService.cs             (CREATE)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | src/api/Api.csproj | Add `BCrypt.Net-Next` 4.x NuGet reference |
| MODIFY | src/api/Program.cs | Add JWT Bearer middleware with `ValidateLifetime=true` and custom `OnChallenge` returning `{"error": "Token expired"}` on expiry |
| MODIFY | src/api/Controllers/AuthController.cs | Add `LoginAsync` (POST /auth/login) and `RefreshAsync` (POST /auth/refresh) |
| CREATE | src/api/DTOs/LoginRequest.cs | `{ string Email; string Password; }` with `[Required]` attributes |
| CREATE | src/api/DTOs/LoginResponse.cs | `{ string AccessToken; string RefreshToken; string Role; }` |
| CREATE | src/api/DTOs/RefreshRequest.cs | `{ string RefreshToken; }` |
| CREATE | src/api/Services/ITokenService.cs | Interface with `GenerateAccessToken(User)` and `GenerateRefreshToken()` |
| CREATE | src/api/Services/TokenService.cs | HMAC-SHA256 JWT generation + refresh token string generation; key from `JWT_SECRET` env var |

---

## External References
- https://learn.microsoft.com/en-us/aspnet/core/security/authentication/jwt-authn?view=aspnetcore-8.0 (ASP.NET Core 8 JWT Bearer authentication)
- https://github.com/BcryptNet/bcrypt.net (BCrypt.Net-Next 4.x — Verify and HashPassword)
- https://datatracker.ietf.org/doc/html/rfc6819#section-5.2.2.3 (Refresh token family invalidation — IETF OAuth threat model)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] `POST /auth/login` with valid credentials → HTTP 200; decode JWT `exp - iat` must equal 900 seconds (AC-001)
- [ ] Decode JWT `role` claim — must equal the user's role string stored in the database (AC-001)
- [ ] `POST /auth/login` with wrong password → HTTP 401 `{"error": "Invalid email or password"}`; same call with non-existent email → identical 401 body (AC-005; OWASP A07)
- [ ] `POST /auth/login` with `isActive=false` user → HTTP 401 `{"error": "Account is inactive..."}` (AC-006)
- [ ] Send expired JWT to a protected endpoint → HTTP 401 `{"error": "Token expired"}` (not 403) (Edge: JWT expiry)
- [ ] `POST /auth/refresh` with an already-used token → HTTP 401 `{"error": "Refresh token reuse detected"}`; query `refresh_tokens WHERE family_id = <id>` — all rows must have `is_revoked = true` (Edge: replay attack)

---

## Implementation Checklist
- [x] `TokenService.GenerateAccessToken` sets `exp = DateTime.UtcNow.AddMinutes(15)` — verified by decoding `exp` claim: `exp - iat == 900` (AC-001)
- [x] JWT is signed with HMAC-SHA256 using `JWT_SECRET` from `Environment.GetEnvironmentVariable("JWT_SECRET")` — not from `appsettings.json` (AC-001; OWASP A02 — no secrets in source)
- [x] `LoginAsync` returns the same `{"error": "Invalid email or password"}` HTTP 401 body whether the email does not exist or the password is wrong — no branching on error message (AC-005; OWASP A07 — prevent account enumeration)
- [x] `IAuditLogger.Log(userId ?? "unknown", "LoginFailure", email)` is called for both non-existent-email and wrong-password paths — before returning the 401 response (AC-005)
- [x] JWT Bearer `OnChallenge` event inspects `context.AuthenticateFailure` — if the failure is `SecurityTokenExpiredException`, writes `{"error": "Token expired"}`; otherwise writes generic unauthorized (Edge: JWT expiry → 401 not 403)
- [x] `RefreshAsync` marks ALL `RefreshToken` rows with matching `FamilyId` as revoked when a reuse is detected — not just the submitted token — then returns 401 (Edge: replay attack; RFC 6819 §5.2.2.3 token family invalidation)
- [x] `BCrypt.Verify(request.Password, user.PasswordHash)` is called with constant-time comparison provided by BCrypt — raw string comparison (`==`) must never be used for password verification (OWASP A02)
