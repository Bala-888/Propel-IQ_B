# Task - TASK_001

## Requirement Reference
- **User Story:** us_027
- **Story Location:** .propel/context/tasks/EP-005/us_027/us_027.md
- **Acceptance Criteria:**
  - AC-001: Reminder dispatched within a 5-minute window of the 24-hour threshold; email subject `"Reminder: Your appointment is tomorrow – <Date>"`; SMS `"Reminder: UPACIP appointment on <Date> at <Time>. Reply STOP to opt out."`
  - AC-002: Second reminder dispatched within a 5-minute window of the 2-hour threshold with updated subject/body; no duplicate of the 24-hour reminder; `reminder_24h_sent_at` and `reminder_2h_sent_at` tracking columns prevent double-fire
  - AC-003: If booking is `Cancelled` when the 2-hour job runs, no notification dispatched; Seq log records `ReminderSkipped reason=BookingCancelled`
  - AC-004: `sms_notifications_enabled = false` → email reminder only; no SMS dispatched
- **Edge Cases:**
  - Booking created < 24 hours before appointment: the 24h reminder window (`NOW() + 24h ± 5min`) will not match the appointment time, so the 24h reminder is naturally skipped; the 2h reminder fires normally when its window is reached — no retrospective 24h fire
  - Reschedule: `BookingService.RescheduleAsync` resets `reminder_24h_sent_at = null` and `reminder_2h_sent_at = null` so the job re-evaluates both windows from the new `appointment_datetime`

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
| Backend | ASP.NET Core Web API (C#) | .NET 8.0 | TR-005 — `AppointmentReminderJob : BackgroundService`; `PeriodicTimer` for 5-minute polling (AC-001, AC-002) |
| ORM | EF Core + Npgsql.EntityFrameworkCore.PostgreSQL | 8.x | TR-006 — candidate queries on `bookings` using `appointment_datetime` window filter; `reminder_24h_sent_at` / `reminder_2h_sent_at` nullable columns added via migration (AC-001, AC-002) |
| Database | PostgreSQL 15.3+ | 15.3+ | TR-007 — indexed `appointment_datetime` column for efficient window-range queries; nullable tracking columns on `bookings` table (AC-001, AC-002) |
| Email + SMS | System.Net.Mail (SmtpClient) | .NET 8.0 built-in | TR-009 — `EmailSettings` (reuse from us_022); `SmsSettings` (reuse from us_026); `EnableSsl = true`; credentials from env vars only (AC-001, AC-002; OWASP A02) |
| Logging | Serilog | Compatible with .NET 8.0 | Structured events: `ReminderDispatched`, `ReminderSkipped`, `ReminderFailed` (AC-003, AC-004; edges) |

---

## Task Overview

Build the `AppointmentReminderJob` hosted service. The job runs on a `PeriodicTimer` (5-minute interval). On each tick it executes two queries against the `bookings` table: one for bookings whose `appointment_datetime` falls in the `NOW() + 24h ± 5min` window with `reminder_24h_sent_at IS NULL`, and one for the `NOW() + 2h ± 5min` window with `reminder_2h_sent_at IS NULL`. Both queries filter `status = Confirmed`, so cancelled bookings are naturally excluded. For each candidate, email and SMS (if opted in) are dispatched concurrently via `Task.WhenAll`, then the corresponding tracking column is set to `DateTimeOffset.UtcNow`. The 24h window skip for late-created bookings is implicit — if a booking's `appointment_datetime` is already within 2h of now, it never enters the 24h window. The reschedule hook resets both tracking columns to null so the job re-fires from the new time.

---

## Dependent Tasks
- task_001 (us_007) — `bookings` table must exist; `reminder_24h_sent_at` and `reminder_2h_sent_at` columns are added via an EF Core migration in this task
- task_002 (us_022) — `EmailSettings` (SMTP config) must be registered in `Program.cs` and available for reuse
- task_001 (us_026) — `SmsSettings` (SMTP-to-SMS gateway config) must be registered in `Program.cs` and available for reuse

---

## Impacted Components
- `src/api/Features/Reminders/AppointmentReminderJob.cs` — new: BackgroundService with PeriodicTimer + dual-window query + dispatch loop
- `src/api/Features/Reminders/AppointmentReminderService.cs` — new: per-candidate opt-out check + concurrent email/SMS dispatch + tracking column update
- `src/api/Features/Reminders/IAppointmentReminderService.cs` — new: interface with `SendReminderAsync(Booking booking, ReminderTier tier)`
- `src/api/Domain/Entities/Booking.cs` — modified: add `DateTimeOffset? Reminder24hSentAt` and `DateTimeOffset? Reminder2hSentAt` properties
- `src/api/Features/Bookings/BookingService.cs` — modified: `RescheduleAsync` resets both reminder tracking columns to null
- `src/api/Infrastructure/Persistence/AppDbContext.cs` — modified: EF Core migration for two new nullable columns with index on `appointment_datetime`
- `src/api/Program.cs` — modified: `AddHostedService<AppointmentReminderJob>()`; register `IAppointmentReminderService` as scoped

---

## Implementation Plan
1. Add `DateTimeOffset? Reminder24hSentAt` and `DateTimeOffset? Reminder2hSentAt` properties to the `Booking` entity; configure as nullable columns in `AppDbContext.OnModelCreating`; generate EF Core migration; add a composite index on `(appointment_datetime, status, reminder_24h_sent_at, reminder_2h_sent_at)` to support the window queries efficiently (AC-001, AC-002; Edge: reschedule)
2. Register `AppointmentReminderJob : BackgroundService` using `PeriodicTimer(TimeSpan.FromMinutes(5))`; resolve a fresh `IServiceScope` per tick via `_scopeFactory.CreateScope()` for the scoped `AppDbContext` (AC-001; DI lifetime correctness; OWASP A04)
3. 24h candidate query per tick: `dbContext.Bookings.Include(b => b.Patient).Include(b => b.Slot) .Where(b => b.Status == BookingStatus.Confirmed && b.Reminder24hSentAt == null && b.AppointmentDatetime >= now.AddHours(24).AddMinutes(-5) && b.AppointmentDatetime <= now.AddHours(24).AddMinutes(5)) .ToListAsync(ct)` — naturally excludes bookings created < 24h before their appointment (AC-001; Edge: booking created < 24h)
4. 2h candidate query per tick (same tick, after 24h loop): identical pattern with `now.AddHours(2) ± 5min` and `Reminder2hSentAt == null`; `status == Confirmed` filter means cancelled bookings never appear in results (AC-002, AC-003 — natural exclusion)
5. Per-candidate dispatch in `AppointmentReminderService.SendReminderAsync(booking, tier)`: check `sms_notifications_enabled`; build `var emailTask = SendEmailAsync(booking, tier)` and `var smsTask = smsEnabled ? SendSmsAsync(booking, tier) : Task.CompletedTask`; `await Task.WhenAll(emailTask, smsTask)` — concurrent, not sequential (AC-004)
6. Email dispatch: 24h tier subject `$"Reminder: Your appointment is tomorrow – {dt:MMMM d, yyyy}"`; 2h tier subject `"Reminder: Your appointment is in 2 hours"`; body includes appointment date/time + clinic address; 3-retry SMTP envelope (30s/60s/120s) using `EmailSettings` (AC-001, AC-002; OWASP A02 — credentials from env vars)
7. SMS dispatch: 24h tier content `$"Reminder: UPACIP appointment on {dt:M/d/yyyy} at {dt:h:mm tt}. Reply STOP to opt out."`; 2h tier content `$"Reminder: Your UPACIP appointment is in 2 hours. {dt:M/d/yyyy} {dt:h:mm tt}. Reply STOP to opt out."`; sent via SMTP-to-SMS gateway using `SmsSettings`; on 3-retry exhaustion log `ReminderFailed {bookingId} Tier={tier} Channel=SMS` (AC-001, AC-002; OWASP A02)
8. After successful `Task.WhenAll`: set `booking.Reminder24hSentAt = DateTimeOffset.UtcNow` (or `Reminder2hSentAt`) and `await dbContext.SaveChangesAsync()` — tracking column write is the deduplication guard; reschedule hook in `BookingService.RescheduleAsync` resets both columns to null before `SaveChangesAsync` (AC-002; Edge: reschedule)

---

## Current Project State
```
src/
└── api/
    ├── Domain/
    │   └── Entities/
    │       └── Booking.cs                            (from us_020 — MODIFY: add Reminder24hSentAt, Reminder2hSentAt)
    ├── Features/
    │   ├── Reminders/
    │   │   ├── (AppointmentReminderJob.cs             — CREATE)
    │   │   ├── (IAppointmentReminderService.cs        — CREATE)
    │   │   └── (AppointmentReminderService.cs         — CREATE)
    │   └── Bookings/
    │       └── BookingService.cs                      (from us_020 — MODIFY: reschedule resets reminder columns)
    └── Infrastructure/
        └── Persistence/
            └── AppDbContext.cs                        (MODIFY: migration for two nullable columns + index)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | src/api/Domain/Entities/Booking.cs | Add Reminder24hSentAt and Reminder2hSentAt nullable DateTimeOffset properties |
| CREATE | src/api/Features/Reminders/IAppointmentReminderService.cs | Interface with SendReminderAsync(Booking, ReminderTier) |
| CREATE | src/api/Features/Reminders/AppointmentReminderService.cs | Opt-out check + concurrent email/SMS dispatch + tracking column update |
| CREATE | src/api/Features/Reminders/AppointmentReminderJob.cs | BackgroundService: PeriodicTimer + dual-window query loop |
| MODIFY | src/api/Infrastructure/Persistence/AppDbContext.cs | EF Core migration: two nullable columns + composite index on appointment_datetime |
| MODIFY | src/api/Features/Bookings/BookingService.cs | RescheduleAsync: reset both reminder tracking columns to null |
| MODIFY | src/api/Program.cs | AddHostedService<AppointmentReminderJob>; register IAppointmentReminderService scoped |

---

## External References
- https://learn.microsoft.com/en-us/dotnet/api/system.threading.periodictimer?view=net-8.0 (.NET 8 PeriodicTimer — 5-minute poll interval; consistent with us_025 job pattern)
- https://learn.microsoft.com/en-us/ef/core/querying/filters?view=efcore-8.0 (EF Core 8 LINQ window-range queries — `appointment_datetime >= threshold.AddMinutes(-5) && <= threshold.AddMinutes(5)` translated to parameterised SQL)
- https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task.whenall?view=net-8.0 (Task.WhenAll — concurrent email + SMS dispatch; AC-002 and us_026 consistent pattern)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] Create a booking with `appointment_datetime = NOW() + 24h`; wait for the next job tick; verify email received with subject `"Reminder: Your appointment is tomorrow – "` and SMS received with "Reply STOP to opt out." within the 5-minute window (AC-001)
- [ ] Verify `reminder_24h_sent_at` column is populated after the 24h reminder fires; run the job again; verify no second 24h reminder is sent (AC-002 — deduplication)
- [ ] Create a booking with `appointment_datetime = NOW() + 2h`; wait for job tick; verify the 2h reminder fires with updated subject/body; verify no 24h reminder is sent (AC-002; Edge: booking < 24h)
- [ ] Cancel a booking after its 24h reminder is sent; wait for the 2h window; verify no notification dispatched and Serilog contains `ReminderSkipped reason=BookingCancelled` (AC-003)
- [ ] Create a booking for a patient with `sms_notifications_enabled = false`; verify email reminder is sent and no SMS is dispatched (AC-004)
- [ ] Reschedule a booking after its 24h reminder is sent; verify both `reminder_24h_sent_at` and `reminder_2h_sent_at` are reset to null; verify the 24h reminder fires again relative to the new appointment time (Edge: reschedule)
- [ ] Create a booking with `appointment_datetime = NOW() + 3h`; verify the 24h reminder window is never matched and no 24h reminder fires; verify the 2h reminder fires when the 2h window is reached (Edge: booking < 24h)

---

## Implementation Checklist
- [ ] Both candidate queries filter `booking.Status == BookingStatus.Confirmed`; cancelled bookings are excluded by this filter alone — no additional `ReminderSkipped` log is written for cancelled bookings in the query path (AC-003 — natural exclusion via status filter; log only applies if a candidate is retrieved and then found cancelled inside the dispatch path)
- [ ] `reminder_24h_sent_at` and `reminder_2h_sent_at` are set to `DateTimeOffset.UtcNow` only after a successful `Task.WhenAll` resolves without exceptions; the tracking column write uses `SaveChangesAsync()` on the same `AppDbContext` instance that loaded the booking (AC-002 — deduplication write atomicity)
- [ ] The 24h window query uses `now.AddHours(24).AddMinutes(-5)` and `now.AddHours(24).AddMinutes(5)` as the lower and upper bounds; a booking created with fewer than 24h remaining will have an `appointment_datetime` outside this window at all job ticks, naturally skipping the 24h reminder without any explicit guard (Edge: booking < 24h)
- [ ] `BookingService.RescheduleAsync` resets both `Reminder24hSentAt = null` and `Reminder2hSentAt = null` before `SaveChangesAsync()`; this allows the job to re-evaluate both windows from the new `appointment_datetime` — no stale tracking values persist after a reschedule (Edge: reschedule)
- [ ] Email and SMS are dispatched via `Task.WhenAll(emailTask, smsTask)` where `smsTask = Task.CompletedTask` when `sms_notifications_enabled = false`; both tasks start simultaneously for opted-in patients (AC-004; AC-001 concurrent dispatch)
- [ ] All SMTP credentials for email and SMS gateway are sourced from `EmailSettings` and `SmsSettings` env-bound configuration respectively; no credentials, phone gateway domains, or SMTP passwords appear in source code (OWASP A02)
- [ ] `IServiceScopeFactory.CreateScope()` is called once per `PeriodicTimer` tick; the resolved `AppDbContext` is used for both the candidate query and the tracking column update within the same tick; the scope is disposed after the tick completes (DI lifetime correctness; OWASP A04)
- [ ] No PHI (patient name, medical data, phone number) is included in Serilog log fields; structured log events contain only non-PHI identifiers (`bookingId` as Guid, `tier`, `channel`) (OWASP A02; HIPAA — no PHI in logs)
