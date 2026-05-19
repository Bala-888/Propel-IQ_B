# Task - TASK_001

## Requirement Reference
- **User Story:** us_010
- **Story Location:** .propel/context/tasks/EP-001/us_010/us_010.md
- **Acceptance Criteria:**
  - AC-004: After 5 consecutive failed `POST /auth/login` requests from the same IP within a 15-minute window, the 6th attempt returns HTTP 429 with `{"error": "Too many failed attempts. Please try again in 15 minutes."}` and a `Retry-After: 900` header
  - AC-005: After the 15-minute window expires, the next login attempt from the previously blocked IP is processed normally and the failed-attempt counter resets to 1
- **Edge Cases:**
  - Rate limiting across load-balanced instances: the failed-attempt counter must be stored in a shared `IDistributedCache` backing store so that a request hitting instance B after previous failures on instance A does not bypass the limiter; `AddDistributedMemoryCache` is used for development (single instance), and the registration is documented to require replacement with `AddStackExchangeRedisCache` in multi-instance deployments

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
| Backend | ASP.NET Core Web API (C#) | .NET 8.0 | TR-002 (backend runtime); the rate limiter service and its integration into `AuthController` are .NET 8 components |
| Backend | Microsoft.Extensions.Caching.Distributed | built-in .NET 8.0 | NFR-004 (failed-attempt counter stored in `IDistributedCache`; `AddDistributedMemoryCache` for dev; swap to `AddStackExchangeRedisCache` for multi-instance) |

---

## Task Overview

Implement `ILoginRateLimiter` and `LoginRateLimiterService` using `IDistributedCache` to track consecutive failed login attempts per client IP address with a 15-minute absolute expiry. Integrate the service into `AuthController.LoginAsync` so that the 429 check occurs at the very start of the login flow (before any credential lookup), failures increment the counter, and successful logins reset it. Use `AddDistributedMemoryCache` for the development single-instance case and document the Redis swap path for production multi-instance deployments.

**Design note:** ASP.NET Core's built-in `AddRateLimiter` counts all requests and cannot distinguish failed from successful logins. This service implements a failure-specific counter to satisfy the AC-004 requirement of blocking only after 5 *failed* attempts.

---

## Dependent Tasks
- task_002 (us_009) — `AuthController.LoginAsync` must exist and be injectable with a service before integrating the rate limiter

---

## Impacted Components
- `src/api/Services/ILoginRateLimiter.cs` — new interface
- `src/api/Services/LoginRateLimiterService.cs` — new implementation using `IDistributedCache`
- `src/api/Controllers/AuthController.cs` — modified to inject and call `ILoginRateLimiter` at login start and after failure/success
- `src/api/Program.cs` — add `AddDistributedMemoryCache()` and register `ILoginRateLimiter`

---

## Implementation Plan
1. Create `ILoginRateLimiter` with three methods: `Task<int> GetFailCountAsync(string ip)`, `Task IncrementAsync(string ip)`, `Task ResetAsync(string ip)`
2. Implement `LoginRateLimiterService`: cache key = `$"login_fail:{ip}"`; `GetFailCountAsync` reads the integer from `IDistributedCache` (returns 0 if absent); `IncrementAsync` reads, increments, and writes back with `AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15)` — the 15-minute expiry restarts on each failure, satisfying the "window resets with each failure" AC model
3. In `AuthController.LoginAsync`, at the start: call `GetFailCountAsync(ip)` where `ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"`; if count ≥ 5, return HTTP 429 with `{"error": "Too many failed attempts. Please try again in 15 minutes."}` and `Response.Headers.Append("Retry-After", "900")` (AC-004)
4. After any failed credential check (wrong password, non-existent email), call `IncrementAsync(ip)` (AC-004 — failure-specific counting)
5. After a successful login, call `ResetAsync(ip)` — clears the counter so a valid user is not blocked after a previously failed attempt from the same IP (AC-005 implied)
6. In `Program.cs`, add `builder.Services.AddDistributedMemoryCache();` (dev/single-instance) and register `builder.Services.AddScoped<ILoginRateLimiter, LoginRateLimiterService>();`; add an inline comment: `// TODO: Replace AddDistributedMemoryCache with AddStackExchangeRedisCache in multi-instance deployments` (Edge: load-balanced instances)

---

## Current Project State
```
src/
└── api/
    ├── Program.cs                          (MODIFY — AddDistributedMemoryCache + register ILoginRateLimiter)
    ├── Controllers/
    │   └── AuthController.cs               (MODIFY — inject ILoginRateLimiter, add check/increment/reset calls)
    └── Services/
        ├── ILoginRateLimiter.cs            (CREATE)
        └── LoginRateLimiterService.cs      (CREATE)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | src/api/Services/ILoginRateLimiter.cs | Interface with `GetFailCountAsync`, `IncrementAsync`, `ResetAsync` |
| CREATE | src/api/Services/LoginRateLimiterService.cs | `IDistributedCache`-backed implementation with 15-minute absolute expiry per IP key |
| MODIFY | src/api/Controllers/AuthController.cs | Inject `ILoginRateLimiter`; add 429 guard at start; increment on failure; reset on success |
| MODIFY | src/api/Program.cs | Add `AddDistributedMemoryCache()` and scoped `ILoginRateLimiter` registration |

---

## External References
- https://learn.microsoft.com/en-us/aspnet/core/performance/caching/distributed?view=aspnetcore-8.0 (ASP.NET Core 8 `IDistributedCache` — `GetAsync`, `SetAsync`, `RemoveAsync` with expiry options)
- https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-8.0 (ASP.NET Core 8 built-in rate limiter — context for why custom failure-counting is required instead)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] Send 5 failing `POST /auth/login` requests from the same IP → HTTP 401 each time; 6th request → HTTP 429 with `Retry-After: 900` header (AC-004)
- [ ] Wait 15 minutes (or set cache expiry to 1s for test) → next login attempt processes normally, not blocked (AC-005)
- [ ] Successful login after 4 failed attempts → next failed attempt starts counter at 1, not 5 (AC-005 — reset on success)

---

## Implementation Checklist
- [ ] The 429 check (`GetFailCountAsync >= 5`) occurs before any database lookup in `LoginAsync` — no DB round-trip wasted on a blocked IP (AC-004; performance)
- [ ] `IncrementAsync` is called only after credential failure (wrong password, non-existent email) — NOT after inactive account 401, to avoid blocking users whose accounts are intentionally deactivated (AC-004 — failure-specific counting)
- [ ] HTTP 429 response includes `Retry-After: 900` response header (`Response.Headers.Append("Retry-After", "900")`) to allow clients to implement a backoff timer (AC-004; RFC 6585 §4)
- [ ] `AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15)` is set on the cache entry so the counter auto-expires after 15 minutes — satisfies AC-005 without a scheduled cleanup job
- [ ] `ResetAsync` calls `IDistributedCache.RemoveAsync(key)` so a successful login fully clears the counter — not just sets it to 0, which would still occupy cache space (AC-005)
- [ ] `Program.cs` comment documents the Redis swap path for multi-instance deployments — the service contract (`ILoginRateLimiter`) is unchanged when swapping the DI backing store (Edge: load-balanced instances)
