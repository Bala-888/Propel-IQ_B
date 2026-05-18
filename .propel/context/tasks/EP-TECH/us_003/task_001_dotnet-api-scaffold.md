# Task - TASK_001

## Requirement Reference
- **User Story:** us_003
- **Story Location:** .propel/context/tasks/EP-TECH/us_003/us_003.md
- **Acceptance Criteria:**
  - AC-001: `GET https://localhost/api/health` returns HTTP 200 `{"status":"Healthy"}` within 2 seconds when the .NET API container is running
  - AC-002 (backend): Every response from the .NET API includes the `X-Service: api` header; the API processes incoming requests via forwarded-header middleware so it sees the correct client IP and protocol from behind Nginx
  - AC-004: `GET https://localhost/api/metrics` returns HTTP 200 with `Content-Type: text/plain; version=0.0.4` and includes at least `http_requests_received_total`
- **Edge Cases:**
  - JWT_SECRET missing: If `JWT_SECRET` environment variable is absent at startup, the API must throw a startup exception logged as "JWT_SECRET is not configured" and exit with code 1 — it must not start with an insecure default

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
| Backend | ASP.NET Core Web API (C#) | .NET 8.0 | TR-002 (mandated backend runtime), NFR-004 (Identity + JWT built-in), NFR-011 (SignalR built-in) |
| Backend | prometheus-net.AspNetCore | 8.x (latest stable) | NFR-002 (uptime + request metrics), AC-004 mandated |
| Infrastructure | Docker (multi-stage build) | 24.x (latest stable) | NFR-010 (free OSS), TR-014 (single Docker Compose stack) |

---

## Task Overview

Bootstrap the `src/api/` ASP.NET Core .NET 8.0 Web API project. Wire the standard middleware pipeline: `UseForwardedHeaders`, `UseHttpMetrics` (prometheus-net), `UseAuthentication`, `UseAuthorization`. Register the built-in health-checks service and map `/health` (returning `{"status":"Healthy"}`). Expose `/metrics` via `prometheus-net.AspNetCore`. Add a response-header middleware that injects `X-Service: api` on every outbound response. Implement startup environment validation that fails fast if `JWT_SECRET` is absent. Author the multi-stage `.NET 8` Dockerfile for the `api` Docker Compose service.

---

## Dependent Tasks
- task_001 (us_001) — `docker-compose.yml` must define the `api` service before the .NET container can be run inside the stack

---

## Impacted Components
- `src/api/Api.csproj` — new .NET 8.0 Web API project file
- `src/api/Program.cs` — DI registration, middleware pipeline, startup validation
- `src/api/Middleware/ServiceHeaderMiddleware.cs` — new response-header middleware adding `X-Service: api`
- `src/api/docker/Dockerfile` — multi-stage .NET 8.0 build + runtime image

---

## Implementation Plan
1. Run `dotnet new webapi -n Api -o src/api --no-openapi false` (keep OpenAPI for future use); remove boilerplate `WeatherForecast` controller
2. Add NuGet package `prometheus-net.AspNetCore` (8.x) to `Api.csproj`
3. In `Program.cs`, validate `JWT_SECRET` immediately after `var builder = WebApplication.CreateBuilder(args);`: `if (string.IsNullOrWhiteSpace(builder.Configuration["JWT_SECRET"])) { Console.Error.WriteLine("FATAL: JWT_SECRET is not configured"); Environment.Exit(1); }`
4. Register `builder.Services.AddHealthChecks()` and call `app.MapHealthChecks("/health", new HealthCheckOptions { ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse })` — or use the default JSON writer for `{"status":"Healthy"}`
5. In the middleware pipeline, add `app.UseForwardedHeaders(new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto })` before `UseAuthentication`
6. Add `app.UseHttpMetrics()` (prometheus-net) and `app.MapMetrics("/metrics")` in the pipeline; confirm `Content-Type: text/plain; version=0.0.4` in the response
7. Create `ServiceHeaderMiddleware.cs` that calls `context.Response.Headers["X-Service"] = "api"` in `InvokeAsync`; register with `app.UseMiddleware<ServiceHeaderMiddleware>()`

---

## Current Project State
```
src/
└── api/
    ├── Api.csproj                           (CREATE)
    ├── Program.cs                           (CREATE)
    ├── appsettings.json                     (CREATE)
    ├── appsettings.Development.json         (CREATE)
    ├── Middleware/
    │   └── ServiceHeaderMiddleware.cs       (CREATE)
    └── docker/
        └── Dockerfile                       (CREATE)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | src/api/Api.csproj | .NET 8.0 Web API project with `prometheus-net.AspNetCore` NuGet reference |
| CREATE | src/api/Program.cs | Middleware pipeline: ForwardedHeaders → HttpMetrics → Auth → HealthChecks + startup JWT_SECRET guard |
| CREATE | src/api/Middleware/ServiceHeaderMiddleware.cs | Sets `X-Service: api` response header on every response |
| CREATE | src/api/appsettings.json | Base app settings — logging levels, Kestrel binding |
| CREATE | src/api/docker/Dockerfile | `mcr.microsoft.com/dotnet/sdk:8.0` build stage; `mcr.microsoft.com/dotnet/aspnet:8.0` runtime stage |

---

## External References
- https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-8.0 (ForwardedHeaders middleware)
- https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks?view=aspnetcore-8.0 (Health checks API)
- https://github.com/prometheus-net/prometheus-net#aspnet-core-exporter (prometheus-net.AspNetCore 8.x setup)
- https://hub.docker.com/_/microsoft-dotnet-sdk (mcr.microsoft.com/dotnet/sdk:8.0 image)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] `dotnet build src/api/Api.csproj` exits with code 0 — zero compile errors (AC-001, AC-004 prerequisite)
- [ ] `docker compose up --wait api` then `curl -f https://localhost/api/health` returns `{"status":"Healthy"}` (AC-001)

---

## Implementation Checklist
- [ ] `src/api/Api.csproj` targets `net8.0` and references `prometheus-net.AspNetCore` NuGet package (AC-004)
- [ ] `Program.cs` startup guard: `if (string.IsNullOrWhiteSpace(config["JWT_SECRET"])) { Console.Error.WriteLine("FATAL: JWT_SECRET is not configured"); Environment.Exit(1); }` executes before any service registration (Edge: JWT_SECRET)
- [ ] `builder.Services.AddHealthChecks()` registered and `app.MapHealthChecks("/health")` returns `{"status":"Healthy"}` as JSON (AC-001)
- [ ] `app.UseForwardedHeaders()` with `XForwardedFor | XForwardedProto` is the first middleware in the pipeline, before `UseAuthentication` (AC-002 — correct client IP and proto visible to API)
- [ ] `app.UseHttpMetrics()` and `app.MapMetrics("/metrics")` registered; response `Content-Type` header is `text/plain; version=0.0.4` (AC-004)
- [ ] `ServiceHeaderMiddleware.InvokeAsync` sets `context.Response.Headers["X-Service"] = "api"` before calling `_next(context)`; registered with `app.UseMiddleware<ServiceHeaderMiddleware>()` (AC-002)
- [ ] `src/api/docker/Dockerfile` uses two-stage build (`sdk:8.0` build stage, `aspnet:8.0` runtime stage); no source code or secrets copied into the runtime image layer (AC-001, OWASP A02 — no secrets in image layers)
