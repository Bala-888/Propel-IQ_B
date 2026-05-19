# Task - TASK_001

## Requirement Reference
- **User Story:** us_028
- **Story Location:** .propel/context/tasks/EP-005/us_028/us_028.md
- **Acceptance Criteria:**
  - AC-001: Google Calendar API `POST /calendars/primary/events` called with correct fields after booking confirmation; `googleEventId` stored in `booking_calendar_sync` table
  - AC-002: Microsoft Graph API `POST /me/events` called after booking confirmation; `outlookEventId` stored in `booking_calendar_sync` table
  - AC-003: On reschedule, PATCH calendar event with updated start/end times; no duplicate event created
  - AC-004: On cancellation, DELETE calendar event; `booking_calendar_sync.status` set to `"Deleted"`
  - AC-005: Calendar API 5xx error does not affect booking status; log `CalendarSyncFailed`; retry queued; booking remains `Confirmed`
- **Edge Cases:**
  - OAuth token expired: attempt refresh using stored refresh token before API call; if refresh fails, log `CalendarTokenRefreshFailed`, return `SyncResult.TokenExpired` to signal the frontend banner "Calendar sync unavailable. Please reconnect your calendar in Settings."
  - SCR-007 loads before sync completes: `POST /calendar/sync` returns 202 Accepted immediately; the actual API call runs asynchronously — the endpoint never blocks waiting for the calendar provider to respond

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
| Backend | ASP.NET Core Web API (C#) | .NET 8.0 | TR-005 — `CalendarSyncController`; `ICalendarSyncService` dispatching async calendar API calls (AC-001, AC-002) |
| ORM | EF Core + Npgsql.EntityFrameworkCore.PostgreSQL | 8.x | TR-006 — `PatientCalendarToken` and `BookingCalendarSync` entities; migrations (AC-001, AC-002) |
| HTTP Client | System.Net.Http (HttpClient) | .NET 8.0 built-in | TR-013 — `IHttpClientFactory` for Google Calendar API and Microsoft Graph API calls; `IHttpClientFactory` registered as named clients per provider (AC-001, AC-002; OWASP A03) |
| Encryption | BouncyCastle.Cryptography | 2.x (reuse from us_006) | AES-256 encryption of OAuth access and refresh tokens at rest in `PatientCalendarToken`; same `IPhiEncryptionService` pattern (OWASP A02) |
| Logging | Serilog | Compatible with .NET 8.0 | Structured events: `CalendarSyncFailed`, `CalendarTokenRefreshFailed`, `CalendarSyncDeleted` (AC-005; edges) |

---

## Task Overview

Implement `POST /api/calendar/sync` and supporting calendar sync infrastructure. The endpoint accepts `{provider, bookingId}`, validates patient ownership via JWT, and enqueues the actual API call asynchronously (returns 202 immediately). `CalendarSyncService` resolves the stored OAuth token, attempts a refresh if expired, calls the appropriate provider API (Google Calendar or Microsoft Graph), and persists the `ExternalEventId` in `booking_calendar_sync`. Reschedule and cancellation hooks in `BookingService` call the update/delete paths. All calendar provider errors are isolated from booking state — AC-005 is the invariant: no calendar failure can change a confirmed booking.

---

## Dependent Tasks
- task_001 (us_020) — `Booking` entity, `BookingService.CancelBookingAsync`, and `RescheduleAsync` must exist for the AC-003/AC-004 hooks
- task_001 (us_006) — `IPhiEncryptionService` (BouncyCastle AES-256) must be available to encrypt/decrypt OAuth tokens at rest

---

## Impacted Components
- `src/api/Features/Calendar/CalendarSyncController.cs` — new: `POST /api/calendar/sync` returning 202 Accepted
- `src/api/Features/Calendar/CalendarSyncService.cs` — new: token resolve + refresh + Google/Graph API calls
- `src/api/Features/Calendar/ICalendarSyncService.cs` — new: interface with SyncAsync, UpdateAsync, DeleteAsync
- `src/api/Domain/Entities/PatientCalendarToken.cs` — new: entity storing encrypted OAuth tokens per patient per provider
- `src/api/Domain/Entities/BookingCalendarSync.cs` — new: entity tracking ExternalEventId and sync status per booking
- `src/api/Features/Bookings/BookingService.cs` — modified: `RescheduleAsync` calls `ICalendarSyncService.UpdateAsync`; `CancelBookingAsync` calls `ICalendarSyncService.DeleteAsync`
- `src/api/Infrastructure/Persistence/AppDbContext.cs` — modified: add `DbSet<PatientCalendarToken>` and `DbSet<BookingCalendarSync>`; generate migration
- `src/api/Program.cs` — modified: register `ICalendarSyncService` as scoped; add named `HttpClient` for `"GoogleCalendar"` and `"MicrosoftGraph"` via `IHttpClientFactory`

---

## Implementation Plan
1. Create `PatientCalendarToken` entity: `Guid Id`, `Guid PatientId (FK)`, `string Provider ("Google" | "Outlook")`, `string EncryptedAccessToken`, `string EncryptedRefreshToken`, `DateTimeOffset TokenExpiry`; `AccessToken` and `RefreshToken` stored encrypted via `IPhiEncryptionService.Encrypt()`; UNIQUE index on `(PatientId, Provider)`; create `BookingCalendarSync` entity: `Guid Id`, `Guid BookingId (FK)`, `string Provider`, `string ExternalEventId`, `string Status ("Synced" | "Failed" | "Deleted")`, `DateTimeOffset CreatedAt`, `DateTimeOffset UpdatedAt`; generate EF Core migration (AC-001, AC-002; OWASP A02)
2. Register named `HttpClient` instances in `Program.cs`: `builder.Services.AddHttpClient("GoogleCalendar", c => c.BaseAddress = new Uri("https://www.googleapis.com/"))` and `builder.Services.AddHttpClient("MicrosoftGraph", c => c.BaseAddress = new Uri("https://graph.microsoft.com/v1.0/"))`; register `ICalendarSyncService` as scoped (AC-001, AC-002; OWASP A03 — base addresses validated at startup)
3. `POST /api/calendar/sync` action: `[Authorize(Roles = Roles.Patient)]`; extract `patientId` from JWT; validate `booking.PatientId == patientId`; return `202 Accepted` immediately; enqueue the actual sync via `Channel<CalendarSyncCommand>` singleton + `CalendarSyncWorker : BackgroundService` consuming it (AC-005 — booking unaffected by calendar errors; Edge: 202 before sync completes; OWASP A01)
4. OAuth token lifecycle in `CalendarSyncService.ResolveTokenAsync`: load `PatientCalendarToken` from DB; decrypt access token; if `TokenExpiry <= UtcNow + 60s` → call provider token endpoint `POST /oauth2/token` (Google) or `POST /oauth2/v2.0/token` (Microsoft) with the decrypted refresh token; on success, re-encrypt and persist new tokens; on failure → log `CalendarTokenRefreshFailed {patientId} Provider={provider}` and return `SyncResult.TokenExpired` (Edge: expired token; OWASP A02 — tokens never logged)
5. Google Calendar sync: `await httpClient.PostAsJsonAsync("calendar/v3/calendars/primary/events", new { summary = $"Appointment at {clinicName}", start = new { dateTime = startTime.ToString("o") }, end = new { dateTime = endTime.ToString("o") }, description = $"{clinicAddress} | Ref: {bookingId}" }, ct)`; on HTTP 200, parse `id` field and upsert `BookingCalendarSync` row with `Status = "Synced"` (AC-001)
6. Microsoft Graph sync: `await httpClient.PostAsJsonAsync("me/events", new { subject = ..., start = new { dateTime = ..., timeZone = "UTC" }, end = ..., body = new { content = $"{clinicAddress} | Ref: {bookingId}" } }, ct)`; on HTTP 201, parse `id` and upsert `BookingCalendarSync` row (AC-002)
7. Reschedule and cancel hooks in `BookingService`: `RescheduleAsync` — if `booking_calendar_sync` row exists with `Status = "Synced"`, call `ICalendarSyncService.UpdateAsync(bookingId, provider)` which issues PATCH with updated start/end (AC-003); `CancelBookingAsync` — if synced row exists, call `ICalendarSyncService.DeleteAsync(bookingId, provider)` which issues DELETE and sets `status = "Deleted"` (AC-004); both hooks execute after `CommitAsync()` — failure of either hook is caught and logged without rolling back the booking state change
8. Failure isolation: all `HttpRequestException` and non-success HTTP status responses in `CalendarSyncService` are caught; log `CalendarSyncFailed {bookingId} Provider={provider} StatusCode={code}`; upsert `BookingCalendarSync` with `Status = "Failed"`; enqueue a retry entry (simple retry counter, max 3) in the `CalendarSyncCommand` channel; the outer `BookingService` methods never propagate calendar exceptions (AC-005; OWASP A04)

---

## Current Project State
```
src/
└── api/
    ├── Domain/
    │   └── Entities/
    │       ├── (PatientCalendarToken.cs            — CREATE)
    │       └── (BookingCalendarSync.cs             — CREATE)
    ├── Features/
    │   ├── Calendar/
    │   │   ├── (CalendarSyncController.cs          — CREATE)
    │   │   ├── (ICalendarSyncService.cs            — CREATE)
    │   │   └── (CalendarSyncService.cs             — CREATE)
    │   └── Bookings/
    │       └── BookingService.cs                   (from us_020 — MODIFY: reschedule + cancel hooks)
    └── Infrastructure/
        └── Persistence/
            └── AppDbContext.cs                     (MODIFY: add DbSet + migration)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | src/api/Domain/Entities/PatientCalendarToken.cs | Entity with encrypted OAuth tokens; UNIQUE (PatientId, Provider) |
| CREATE | src/api/Domain/Entities/BookingCalendarSync.cs | Entity tracking ExternalEventId and sync status |
| CREATE | src/api/Features/Calendar/ICalendarSyncService.cs | Interface: SyncAsync, UpdateAsync, DeleteAsync |
| CREATE | src/api/Features/Calendar/CalendarSyncService.cs | Token refresh + Google/Graph API calls + failure isolation |
| CREATE | src/api/Features/Calendar/CalendarSyncController.cs | POST /api/calendar/sync returning 202 Accepted |
| MODIFY | src/api/Infrastructure/Persistence/AppDbContext.cs | Add DbSet<PatientCalendarToken>, DbSet<BookingCalendarSync>; migration |
| MODIFY | src/api/Features/Bookings/BookingService.cs | RescheduleAsync: UpdateAsync hook; CancelBookingAsync: DeleteAsync hook |
| MODIFY | src/api/Program.cs | Register ICalendarSyncService; named HttpClients; Channel<CalendarSyncCommand> |

---

## External References
- https://developers.google.com/calendar/api/v3/reference/events/insert (Google Calendar API v3 — events.insert: request body shape, response `id` field; AC-001)
- https://learn.microsoft.com/en-us/graph/api/user-post-events?view=graph-rest-1.0 (Microsoft Graph API — POST /me/events: request body shape, response `id` field; AC-002)
- https://learn.microsoft.com/en-us/dotnet/core/extensions/httpclient-factory (IHttpClientFactory named clients — prevents socket exhaustion vs new HttpClient(); TR-013 free open-source)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] With a valid Google OAuth token stored, call `POST /api/calendar/sync {provider: "Google", bookingId}`; verify 202 response; verify Google Calendar API is called with correct `summary`, `start`, `end`, `description`; verify `booking_calendar_sync` row created with `status = "Synced"` and `externalEventId` populated (AC-001)
- [ ] Repeat with `provider = "Outlook"`; verify Microsoft Graph `POST /me/events` called; verify `booking_calendar_sync` row (AC-002)
- [ ] Reschedule the booking; verify calendar PATCH is called with updated times; verify no second event created in the provider calendar (AC-003)
- [ ] Cancel the booking; verify calendar DELETE called; verify `booking_calendar_sync.status = "Deleted"` (AC-004)
- [ ] Stub the Google Calendar API to return 500; verify booking remains `Confirmed`; Serilog contains `CalendarSyncFailed`; `booking_calendar_sync.status = "Failed"` (AC-005)
- [ ] Set `PatientCalendarToken.TokenExpiry` to 30 seconds ago; call sync; verify a token refresh request is made before the calendar API call; on refresh success, new encrypted tokens are persisted (Edge: expired token)
- [ ] Stub the token refresh endpoint to return 401; verify `CalendarTokenRefreshFailed` is logged and `SyncResult.TokenExpired` is returned (Edge: expired token — refresh failure path)
- [ ] Verify `EncryptedAccessToken` and `EncryptedRefreshToken` in `PatientCalendarToken` are stored as AES-256 ciphertext — never as plaintext (OWASP A02)

---

## Implementation Checklist
- [ ] `PatientCalendarToken.EncryptedAccessToken` and `EncryptedRefreshToken` are stored as AES-256 ciphertext using `IPhiEncryptionService.Encrypt()`; tokens are decrypted only within `ResolveTokenAsync` immediately before the API call and are never written to Serilog or response bodies (OWASP A02; HIPAA)
- [ ] `POST /api/calendar/sync` returns `202 Accepted` before the calendar API call completes; the actual sync executes in `CalendarSyncWorker` consuming a `Channel<CalendarSyncCommand>` — the HTTP response is never blocked by external provider latency (Edge: SCR-007 loads before sync; AC-005)
- [ ] Ownership check: `booking.PatientId` is compared to the JWT `patientId` claim before any sync is initiated; a patient cannot trigger calendar sync for another patient's booking (OWASP A01; A07)
- [ ] Token refresh attempts one call to the provider token endpoint; if the refresh response is non-2xx, `SyncResult.TokenExpired` is returned without logging the refresh token value — only `patientId` and `provider` are included in the log (OWASP A02; Edge: expired token)
- [ ] All `HttpRequestException` and non-success HTTP status codes from Google/Graph APIs are caught in `CalendarSyncService`; `booking_calendar_sync` is updated to `Status = "Failed"` and `CalendarSyncFailed` is logged; the exception is NOT propagated to `BookingService` — booking state is never affected by calendar sync failures (AC-005; OWASP A04)
- [ ] `PATCH` (update) and `DELETE` (cancel) hooks in `BookingService` execute after `CommitAsync()` for the booking state change; they are invoked in a separate try/catch — a calendar hook failure cannot roll back the booking update or cancellation (AC-003, AC-004; AC-005 invariant)
- [ ] `IHttpClientFactory` named clients (`"GoogleCalendar"`, `"MicrosoftGraph"`) are used exclusively — no `new HttpClient()` instantiation in `CalendarSyncService`; base addresses are set at startup, not at call time (OWASP A03; socket exhaustion prevention)
