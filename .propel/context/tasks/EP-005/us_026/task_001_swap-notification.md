# Task - TASK_001

## Requirement Reference
- **User Story:** us_026
- **Story Location:** .propel/context/tasks/EP-005/us_026/us_026.md
- **Acceptance Criteria:**
  - AC-001: Email sent within 60 seconds of `SlotSwapCompleted` event; subject `"Your appointment has been updated – <NewDate>"`; body contains new slot date/time, clinic address, and new booking reference
  - AC-002: SMS dispatched concurrently with email (via `Task.WhenAll`); patient's phone number and `sms_notifications_enabled = true` required; content: `"Your UPACIP appointment has been moved to <NewDate> <NewTime>. Ref: <BookingId>"` via SMTP-to-SMS gateway
  - AC-003: QuestPDF PDF attached to email as `updated-confirmation-<newBookingId>.pdf`; content matches us_022 AC-002 mandatory fields (generated via the existing `IConfirmationPdfService`)
  - AC-004: `sms_notifications_enabled = false` → SMS skipped; log `SmsSkipped reason=OptedOut`; email channel is independent and continues; `email_notifications_enabled = false` → email skipped; log `EmailSkipped reason=OptedOut`
- **Edge Cases:**
  - Both email and SMS fail all 3 retries: log `SlotSwapNotificationFailed` with channel type, bookingId, and exception details; the committed slot swap is never reverted — notification failure does not affect booking state
  - Notification in-flight payload freshness: all booking details (new date/time, clinic address, booking reference) are loaded from the persisted new booking DB record using `evt.NewBookingId`; the event payload fields alone are never used to compose the notification content

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
| Backend | ASP.NET Core Web API (C#) | .NET 8.0 | TR-005 — `SlotSwapNotificationWorker : BackgroundService`; `SlotSwapNotificationService` domain service |
| Async Queue | System.Threading.Channels | .NET 8.0 built-in | Consumes `Channel<SlotSwapCompletedEvent>` singleton registered in us_025 (AC-001 — 60s SLA) |
| Email + SMS | System.Net.Mail (SmtpClient) | .NET 8.0 built-in | TR-009 — same SMTP client used for email (AC-001) and SMTP-to-SMS gateway dispatch (AC-002); `EnableSsl = true`; credentials from env vars only |
| PDF | QuestPDF + IConfirmationPdfService | Latest stable (reuse from us_022) | TR-009 — `IConfirmationPdfService.GenerateAsync` reused; no new PDF library required (AC-003) |
| Logging | Serilog | Compatible with .NET 8.0 | Structured events: `SmsSkipped`, `EmailSkipped`, `PdfGenerationTimeout`, `SlotSwapNotificationFailed` (AC-004; edges) |

---

## Task Overview

Build the `SlotSwapNotificationWorker` (BackgroundService) and `SlotSwapNotificationService`. The worker consumes `Channel<SlotSwapCompletedEvent>` (registered in us_025) and calls `SendAsync` per event. `SendAsync` loads the confirmed new booking from DB by `evt.NewBookingId` (never trusting event fields for composing content), checks per-channel opt-outs, generates the PDF via the existing `IConfirmationPdfService`, then dispatches email and SMS concurrently via `Task.WhenAll`. Each channel has an independent 3-retry envelope (30s/60s/120s). Failure of both channels is logged as `SlotSwapNotificationFailed` without affecting the committed booking.

---

## Dependent Tasks
- task_001 (us_025) — `SlotSwapCompletedEvent` record and `Channel<SlotSwapCompletedEvent>` singleton must be registered in `Program.cs` before this worker can register as a consumer
- task_002 (us_022) — `IConfirmationPdfService.GenerateAsync` and `ConfirmationData` record must be available for PDF attachment generation (AC-003)
- task_001 (us_009) — patient preferences (`email_notifications_enabled`, `sms_notifications_enabled`, `phone_number`) must exist on the user/patient record (AC-002, AC-004)

---

## Impacted Components
- `src/api/Features/Notifications/SlotSwapNotificationWorker.cs` — new: BackgroundService consuming Channel<SlotSwapCompletedEvent>
- `src/api/Features/Notifications/SlotSwapNotificationService.cs` — new: opt-out check, DB load, PDF, concurrent email + SMS dispatch
- `src/api/Features/Notifications/ISlotSwapNotificationService.cs` — new: interface with SendAsync(SlotSwapCompletedEvent)
- `src/api/Features/Notifications/SmsSettings.cs` — new: config record (SmsGatewayDomain) bound from environment variables
- `src/api/Program.cs` — modified: `AddHostedService<SlotSwapNotificationWorker>`; register `ISlotSwapNotificationService` as scoped; bind `SmsSettings` from config

---

## Implementation Plan
1. Create `SmsSettings` config record: `string SmsGatewayDomain` bound from `builder.Configuration.GetSection("Sms")` — SMTP-to-SMS gateway domain (e.g., `txt.att.net`); never hard-coded in source (OWASP A02); register `AddHostedService<SlotSwapNotificationWorker>()` and `AddScoped<ISlotSwapNotificationService, SlotSwapNotificationService>()` in `Program.cs` (AC-002)
2. `SlotSwapNotificationWorker : BackgroundService`: `await foreach (var evt in _channel.Reader.ReadAllAsync(stoppingToken))` consuming the singleton `Channel<SlotSwapCompletedEvent>` from us_025; per-event `try/catch(Exception)` with `logger.LogError` and continue (AC-001 — 60s SLA)
3. `SlotSwapNotificationService.SendAsync`: load the confirmed new booking from DB: `await dbContext.Bookings.Include(b => b.Slot).Include(b => b.Patient).FirstOrDefaultAsync(b => b.Id == evt.NewBookingId && b.Status == BookingStatus.Confirmed)`; if null → log `NewBookingNotFound {evt.NewBookingId}` and return — guards against race conditions where the swap is rolled back before the event is processed (Edge: payload freshness; AC-001)
4. Check opt-out flags independently: if `patient.EmailNotificationsEnabled == false` → `logger.LogInformation("EmailSkipped {PatientId} Reason=OptedOut")`; if `patient.SmsNotificationsEnabled == false` → `logger.LogInformation("SmsSkipped {PatientId} Reason=OptedOut")`; each channel skips independently without affecting the other (AC-004)
5. PDF generation: load `ConfirmationData` from the new booking record; call `await _pdfService.GenerateAsync(data, cts.Token)` with `CancellationTokenSource(10s)`; if `OperationCanceledException` → log `PdfGenerationTimeout {evt.NewBookingId}`; `pdfBytes = null`; email proceeds with fallback body text "Your updated confirmation document is being generated and will follow shortly" and no attachment (AC-003; us_022 Edge pattern reused)
6. Concurrent dispatch: `var emailTask = emailEnabled ? SendEmailAsync(booking, pdfBytes, ct) : Task.CompletedTask; var smsTask = smsEnabled ? SendSmsAsync(booking, ct) : Task.CompletedTask; await Task.WhenAll(emailTask, smsTask)` — email and SMS run simultaneously, not sequentially (AC-002)
7. `SendEmailAsync`: subject `$"Your appointment has been updated – {booking.Slot.StartTime:MMMM d, yyyy}"`; body with new date/time + clinic address + booking reference; attach `updated-confirmation-{booking.Id}.pdf` when pdfBytes non-null; 3-retry SMTP envelope (30s/60s/120s) using `EmailSettings` from us_022 (AC-001, AC-003; OWASP A02)
8. `SendSmsAsync`: construct SMS address `{patient.PhoneNumber}@{_smsSettings.SmsGatewayDomain}`; body `$"Your UPACIP appointment has been moved to {date:M/d/yyyy} {time:h:mm tt}. Ref: {booking.Id}"`; 3-retry SMTP envelope; on all-3-retry failure → log `SlotSwapNotificationFailed {evt.NewBookingId} Channel=SMS`; booking state is never modified on notification failure (AC-002; Edge: both fail all retries)

---

## Current Project State
```
src/
└── api/
    └── Features/
        └── Notifications/
            ├── IConfirmationPdfService.cs           (from us_022 — REUSE)
            ├── ConfirmationPdfService.cs             (from us_022 — REUSE)
            ├── ConfirmationData.cs                   (from us_022 — REUSE)
            ├── EmailSettings.cs                      (from us_022 — REUSE)
            ├── (ISlotSwapNotificationService.cs      — CREATE)
            ├── (SlotSwapNotificationService.cs       — CREATE)
            ├── (SlotSwapNotificationWorker.cs        — CREATE)
            └── (SmsSettings.cs                       — CREATE)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | src/api/Features/Notifications/SmsSettings.cs | Config record: SmsGatewayDomain from env vars |
| CREATE | src/api/Features/Notifications/ISlotSwapNotificationService.cs | Interface with SendAsync |
| CREATE | src/api/Features/Notifications/SlotSwapNotificationService.cs | Opt-out, DB load, PDF, concurrent email + SMS dispatch |
| CREATE | src/api/Features/Notifications/SlotSwapNotificationWorker.cs | BackgroundService consuming Channel<SlotSwapCompletedEvent> |
| MODIFY | src/api/Program.cs | AddHostedService; register ISlotSwapNotificationService scoped; bind SmsSettings |

---

## External References
- https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task.whenall?view=net-8.0 (Task.WhenAll — concurrent dispatch of email and SMS; AC-002 — not sequential)
- https://learn.microsoft.com/en-us/dotnet/api/system.net.mail.smtpclient.sendmailasync?view=net-8.0 (System.Net.Mail.SmtpClient.SendMailAsync — reused for both email and SMTP-to-SMS gateway; AC-001, AC-002)
- https://en.wikipedia.org/wiki/SMS_gateway#Email_clients (SMTP-to-SMS email gateway format: `phonenumber@gateway-domain` — AC-002 SMS dispatch pattern)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] Trigger a `SlotSwapCompletedEvent`; verify email arrives within 60 seconds with subject `"Your appointment has been updated – "`, body containing new date/time + clinic address + reference, and PDF attachment `updated-confirmation-<id>.pdf` (AC-001, AC-003)
- [ ] Verify email and SMS are dispatched at the same time (concurrent) — the SMS should not wait for the email to complete before starting (AC-002)
- [ ] Set patient `sms_notifications_enabled = false`; trigger swap event; verify email is sent and Serilog contains `SmsSkipped reason=OptedOut` — no SMS dispatch attempt (AC-004)
- [ ] Set patient `email_notifications_enabled = false` and `sms_notifications_enabled = true`; verify SMS is sent and Serilog contains `EmailSkipped reason=OptedOut` (AC-004)
- [ ] Stub the SMTP server as unreachable; trigger swap event; verify 3 retry attempts per channel with 30s/60s/120s delays; verify Serilog contains `SlotSwapNotificationFailed`; verify the new booking record still has `status = "Confirmed"` (Edge: both fail all retries)
- [ ] Change the booking's slot details between `SlotSwapCompletedEvent` emission and notification processing; verify the email body uses the freshly loaded DB slot details, not stale event fields (Edge: payload freshness)

---

## Implementation Checklist
- [ ] New booking details are loaded from the DB using `evt.NewBookingId` inside `SendAsync` before composing any notification content; event fields (`OldSlotId`, `NewSlotId`) are used only for routing and logging — never for constructing the email body, subject, or SMS text (Edge: payload freshness; AC-001)
- [ ] Email and SMS are dispatched via `Task.WhenAll(emailTask, smsTask)`; `SendEmailAsync` and `SendSmsAsync` are independent tasks that start simultaneously — neither awaits the other before beginning (AC-002)
- [ ] Opt-out flags are checked independently per channel before the channel's dispatch task is created; a skipped channel sets its task to `Task.CompletedTask` so `Task.WhenAll` still resolves correctly (AC-004)
- [ ] PDF generation uses the existing `IConfirmationPdfService.GenerateAsync` with a fresh `CancellationTokenSource(10s)` per notification; `OperationCanceledException` sets `pdfBytes = null` and logs `PdfGenerationTimeout`; the email is sent without attachment rather than blocking the notification (AC-003; us_022 pattern reuse)
- [ ] Both `SendEmailAsync` and `SendSmsAsync` have independent 3-retry envelopes (`int[] delays = [30, 60, 120]`) with `SmtpException` catch per attempt; all-retries-exhausted logs `SlotSwapNotificationFailed` with `Channel` field (`"Email"` or `"SMS"`); the committed swap booking record is never modified on notification failure (Edge: both fail all retries; AC-001)
- [ ] `SmsGatewayDomain` and all SMTP credentials are sourced exclusively from environment-bound configuration (`SmsSettings`, `EmailSettings`); no credentials, phone numbers, or gateway domains appear in source code or `appsettings.json` (OWASP A02)
- [ ] `SlotSwapNotificationService` is registered as scoped and resolved inside `SlotSwapNotificationWorker` via `IServiceScopeFactory` — scoped `AppDbContext` is not captured by the singleton worker across invocations (DI lifetime correctness; OWASP A04)
