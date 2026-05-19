# Task - TASK_002

## Requirement Reference
- **User Story:** us_022
- **Story Location:** .propel/context/tasks/EP-004/us_022/us_022.md
- **Acceptance Criteria:**
  - AC-001: The patient receives an email with subject `"Appointment Confirmed – <Date>"`, a body containing appointment date/time and clinic location, and a `confirmation-<bookingId>.pdf` PDF attachment within 60 seconds of booking confirmation
  - AC-003: SMTP failure is retried at 30-second, 60-second, and 120-second intervals; after 3 exhausted retries, `level = "Error"` is logged with `eventType = "ConfirmationEmailFailed"`; the booking record remains `status = "Confirmed"` regardless
  - AC-004: When `email_notifications_enabled = false` in the patient's preferences, no email is dispatched, no SMTP connection is opened, and the log records `eventType = "ConfirmationEmailSkipped"` with `reason = "OptedOut"`
- **Edge Cases:**
  - Malformed email address: if the stored patient email fails RFC 5322 validation, log `eventType = "ConfirmationEmailFailed"` with `reason = "InvalidEmailAddress"` and skip all retry attempts — no SMTP connection is opened
  - PDF generation timeout: if `IConfirmationPdfService.GenerateAsync` does not complete within 10 seconds, log `eventType = "PdfGenerationTimeout"`; send the email without the PDF attachment; include body text "Your confirmation document is being generated and will follow shortly"

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
| Backend | ASP.NET Core Web API (C#) | .NET 8.0 | TR-005 (ConfirmationEmailWorker BackgroundService; ConfirmationEmailService domain service) |
| Async Queue | System.Threading.Channels | .NET 8.0 built-in | Bounded `Channel<BookingConfirmedEvent>` — same pattern as NoShowRiskScoringWorker; `TryWrite` after CommitAsync (AC-001 — 60s SLA) |
| Email | System.Net.Mail (SmtpClient) | .NET 8.0 built-in | TR-009 — free SMTP dispatch; `EnableSsl = true`; credentials from environment variables only (AC-001, AC-003; OWASP A02) |
| PDF | QuestPDF + Net.QRCoder | Latest stable | Used via `IConfirmationPdfService` from task_001 (AC-001, AC-002) |
| Logging | Serilog | Compatible with .NET 8.0 | Structured events: ConfirmationEmailSkipped, ConfirmationEmailFailed, PdfGenerationTimeout (AC-003, AC-004; Edge cases) |

---

## Task Overview

Build the `BookingConfirmedEvent` dispatch pipeline: after `CommitAsync()` in `BookingService`, a `BookingConfirmedEvent` is enqueued onto a bounded `Channel`. A `ConfirmationEmailWorker` (BackgroundService) dequeues events and calls `IConfirmationEmailService.SendAsync`. The service checks opt-out, validates the email address, generates the PDF (with a 10s timeout), constructs the email message, and dispatches via SMTP with a 3-retry exponential envelope. All failure paths log structured events to Seq without affecting the booking record status.

---

## Dependent Tasks
- task_001 (us_022) — `IConfirmationPdfService.GenerateAsync` must be available; `ConfirmationData` record must be defined
- task_001 (us_020) — `BookingService.CreateBookingAsync` with `CommitAsync()` is the enqueue point for `BookingConfirmedEvent`

---

## Impacted Components
- `src/api/Features/Notifications/ConfirmationEmailService.cs` — new: opt-out check, email validation, PDF call, SMTP retry
- `src/api/Features/Notifications/IConfirmationEmailService.cs` — new: interface with `SendAsync(BookingConfirmedEvent)` signature
- `src/api/Features/Notifications/ConfirmationEmailWorker.cs` — new: BackgroundService consuming `Channel<BookingConfirmedEvent>`
- `src/api/Features/Notifications/BookingConfirmedEvent.cs` — new: event record with patient, booking, and slot metadata
- `src/api/Features/Notifications/EmailSettings.cs` — new: configuration record bound from environment variables
- `src/api/Features/Bookings/BookingService.cs` — modified: enqueue `BookingConfirmedEvent` after `CommitAsync()` (alongside the existing `BookingCreatedEvent` for risk scoring)
- `src/api/Program.cs` — modified: register `Channel<BookingConfirmedEvent>` singleton, `AddHostedService<ConfirmationEmailWorker>`, `IConfirmationEmailService` as scoped, `EmailSettings` from config

---

## Implementation Plan
1. Define `BookingConfirmedEvent` record: `Guid BookingId`, `Guid PatientId`, `string PatientEmail`, `string PatientFullName`, `DateTimeOffset AppointmentDateTime`, `string ClinicName`, `string ClinicAddress`, `string? ProviderName`; register `Channel<BookingConfirmedEvent>.CreateBounded(new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.Wait })` as singleton; add `AddHostedService<ConfirmationEmailWorker>()`; bind `EmailSettings` from `builder.Configuration.GetSection("Email")` (AC-001; OWASP A02)
2. In `BookingService.CreateBookingAsync`, after `await transaction.CommitAsync()`: call `_confirmationChannel.Writer.TryWrite(new BookingConfirmedEvent { ... })`; this executes synchronously alongside the existing `_riskChannel.Writer.TryWrite` from us_021 — neither blocks the 201 response (AC-001 — 60s SLA)
3. Implement `ConfirmationEmailWorker : BackgroundService`: `await foreach (var evt in _channel.Reader.ReadAllAsync(stoppingToken))` with per-event `try/catch(Exception ex)` logging `logger.LogError(ex, "UnhandledConfirmationEmailError {BookingId}", evt.BookingId)` and continuing to next event (AC-001)
4. `ConfirmationEmailService.SendAsync`: load patient preferences via `dbContext.PatientPreferences.FirstOrDefaultAsync(p => p.PatientId == evt.PatientId)`; if `email_notifications_enabled = false` → `logger.LogInformation("{EventType} {PatientId} Reason=OptedOut", "ConfirmationEmailSkipped", evt.PatientId)`; return without opening SMTP (AC-004; OWASP A02 — no connection opened)
5. RFC 5322 email validation: `try { _ = new System.Net.Mail.MailAddress(evt.PatientEmail); } catch (FormatException) { logger.LogError("ConfirmationEmailFailed {BookingId} Reason=InvalidEmailAddress", evt.BookingId); return; }` — no retry loop entered for invalid addresses (Edge: malformed email)
6. PDF generation with 10s timeout: `using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)); byte[]? pdfBytes = null; try { pdfBytes = await _pdfService.GenerateAsync(confirmationData, cts.Token); } catch (OperationCanceledException) { logger.LogError("PdfGenerationTimeout {BookingId}", evt.BookingId); }` — `pdfBytes = null` triggers the no-attachment email path; a deferred delivery job placeholder is logged (Edge: PDF timeout; AC-002)
7. SMTP dispatch with 3-retry envelope: `var delays = new[] { 30, 60, 120 }; for (int attempt = 0; attempt < 3; attempt++) { try { await smtpClient.SendMailAsync(message); return; } catch (SmtpException) when (attempt < 2) { await Task.Delay(TimeSpan.FromSeconds(delays[attempt])); } catch (SmtpException) { logger.LogError("{EventType} {BookingId}", "ConfirmationEmailFailed", evt.BookingId); } }` (AC-003)
8. Email message construction: `var message = new MailMessage { Subject = $"Appointment Confirmed – {evt.AppointmentDateTime:MMMM d, yyyy}", Body = $"...\n{evt.AppointmentDateTime:...}\n{evt.ClinicAddress}" }`; if `pdfBytes != null`, attach as `new Attachment(new MemoryStream(pdfBytes), $"confirmation-{evt.BookingId}.pdf", "application/pdf")`; `SmtpClient` constructed from `_emailSettings.SmtpHost/Port` with `NetworkCredential` from environment-bound `SmtpUsername`/`SmtpPassword`; `EnableSsl = true` (AC-001, AC-002; OWASP A02 — credentials from env vars)

---

## Current Project State
```
src/
└── api/
    ├── Features/
    │   ├── Notifications/
    │   │   ├── IConfirmationPdfService.cs        (from task_001 us_022)
    │   │   ├── ConfirmationPdfService.cs          (from task_001 us_022)
    │   │   ├── ConfirmationData.cs                (from task_001 us_022)
    │   │   └── (IConfirmationEmailService.cs      — CREATE)
    │   │   └── (ConfirmationEmailService.cs       — CREATE)
    │   │   └── (ConfirmationEmailWorker.cs        — CREATE)
    │   │   └── (BookingConfirmedEvent.cs          — CREATE)
    │   │   └── (EmailSettings.cs                  — CREATE)
    │   └── Bookings/
    │       └── BookingService.cs                  (from us_020 — MODIFY: add BookingConfirmedEvent enqueue)
    └── Program.cs                                 (MODIFY: channel + worker + email settings registration)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | src/api/Features/Notifications/BookingConfirmedEvent.cs | Domain event record for email trigger |
| CREATE | src/api/Features/Notifications/EmailSettings.cs | SMTP config record bound from environment variables |
| CREATE | src/api/Features/Notifications/IConfirmationEmailService.cs | Interface with SendAsync signature |
| CREATE | src/api/Features/Notifications/ConfirmationEmailService.cs | Opt-out, validation, PDF call, SMTP retry |
| CREATE | src/api/Features/Notifications/ConfirmationEmailWorker.cs | BackgroundService consuming Channel |
| MODIFY | src/api/Features/Bookings/BookingService.cs | Enqueue BookingConfirmedEvent after CommitAsync |
| MODIFY | src/api/Program.cs | Register Channel, AddHostedService, IConfirmationEmailService, EmailSettings |

---

## External References
- https://learn.microsoft.com/en-us/dotnet/api/system.net.mail.smtpclient?view=net-8.0 (System.Net.Mail SmtpClient — SendMailAsync, NetworkCredential, EnableSsl)
- https://learn.microsoft.com/en-us/dotnet/api/system.net.mail.mailaddress?view=net-8.0 (MailAddress constructor — RFC 5322 validation via FormatException; Edge: malformed email)
- https://learn.microsoft.com/en-us/aspnet/core/fundamentals/hosted-services?view=aspnetcore-8.0 (ASP.NET Core 8 BackgroundService — IServiceScopeFactory for scoped DbContext access inside singleton worker)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] Create a booking for a patient with `email_notifications_enabled = true` and a valid email; within 60 seconds, verify the patient's inbox contains an email with subject matching `"Appointment Confirmed – "` and a `confirmation-<bookingId>.pdf` attachment (AC-001)
- [ ] Verify the email body contains the appointment date/time and clinic address (AC-001)
- [ ] Temporarily make the SMTP server unreachable; create a booking; verify exactly 3 send attempts are made at ~30s, ~60s, ~120s intervals; verify Serilog log contains `eventType = "ConfirmationEmailFailed"` after the 3rd failure (AC-003)
- [ ] Create a booking for a patient with `email_notifications_enabled = false`; verify no email is received and Serilog contains `eventType = "ConfirmationEmailSkipped"` with `reason = "OptedOut"` (AC-004)
- [ ] Set patient email to an invalid value (e.g., `"not-an-email"`); create a booking; verify Serilog contains `eventType = "ConfirmationEmailFailed"` with `reason = "InvalidEmailAddress"` and no SMTP connection is attempted (Edge: malformed email)
- [ ] Inject a slow PDF service (>10s delay) in a test; create a booking; verify: (a) email is sent without attachment within 60s, (b) email body contains "Your confirmation document is being generated and will follow shortly", (c) Serilog contains `eventType = "PdfGenerationTimeout"` (Edge: PDF timeout)
- [ ] Verify `SMTP_PASSWORD` is never written to `appsettings.json` or source code; environment variable injection is the only configuration path (OWASP A02)

---

## Implementation Checklist
- [ ] `BookingConfirmedEvent` is enqueued via `_confirmationChannel.Writer.TryWrite(evt)` synchronously after `CommitAsync()` — no `await` on the channel write; the 201 response path is not delayed; if the channel is full, a warning is logged and processing continues (AC-001 — 60s SLA; no latency regression)
- [ ] Opt-out check reads `PatientPreferences.email_notifications_enabled` before any other operation; if false, logs `ConfirmationEmailSkipped reason=OptedOut` and returns immediately without opening an SMTP connection (AC-004; OWASP A02 — minimum data operations)
- [ ] RFC 5322 validation uses `new System.Net.Mail.MailAddress(email)` inside a `try/catch(FormatException)` — on catch, logs `ConfirmationEmailFailed reason=InvalidEmailAddress` and returns; the retry loop is never entered for an invalid address (Edge: malformed email)
- [ ] PDF 10s timeout uses `new CancellationTokenSource(TimeSpan.FromSeconds(10))` passed to `IConfirmationPdfService.GenerateAsync`; `OperationCanceledException` is caught, `PdfGenerationTimeout` is logged, and `pdfBytes` remains null — the email is sent without an attachment with the fallback body text (Edge: PDF timeout; AC-001 — email still dispatched)
- [ ] SMTP retry loop uses `int[] delays = [30, 60, 120]`; the loop iterates exactly 3 times; on success the method returns early; after the 3rd `SmtpException`, `ConfirmationEmailFailed` is logged with the `bookingId`; no further retries or exceptions are propagated — the booking `status` column is never modified (AC-003; OWASP A04)
- [ ] `SmtpClient` is constructed with `EnableSsl = true`; `NetworkCredential` is populated from `_emailSettings.SmtpUsername` and `_emailSettings.SmtpPassword` which are bound exclusively from environment variables; no SMTP credentials appear in source code or `appsettings.json` (OWASP A02 — sensitive data exposure)
- [ ] `ConfirmationEmailService` is registered as scoped and uses `IServiceScopeFactory` to resolve `AppDbContext` within the singleton `ConfirmationEmailWorker` — prevents captured scoped service lifetime violations (OWASP A04; DI correctness)
- [ ] `BookingConfirmedEvent` contains only the minimum fields needed to compose the email and PDF: no medical history, diagnosis, intake, or medication data is included in the event record (OWASP A02; HIPAA minimum-necessary)
