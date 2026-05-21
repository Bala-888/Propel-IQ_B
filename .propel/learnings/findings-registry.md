<!-- Schema: ./findings-registry-schema.md -->
# Findings Registry

## Index

| File | Finding IDs |
|------|-------------|
| src/api/Features/Patients/WalkinPatientService.cs | F001 |
| src/api/Features/Bookings/WalkinBookingController.cs | F002 |
| src/api/Features/Bookings/WalkinBookingService.cs | F003 |
| src/api/Features/Queue/QueueService.cs | F004 |
| src/api/Data/Entities/Booking.cs | F005 |
| frontend/src/pages/QueuePage.tsx | F006 |
| src/api/Features/Queue/QueueService.cs (MarkArrivedAsync) | F007 |
| src/api/Features/Queue/QueueController.cs | F008 |
| frontend/src/pages/QueuePage.tsx | F009 |
| src/api/Features/Queue/QueueEntryDto.cs | F010 |
| src/api/Features/Queue/QueueService.cs | F011 |
| src/api/Features/Admin/AdminMetricsService.cs | F012 |
| src/api/Features/Admin/AdminMetricsService.cs | F013 |
| src/api/Features/Queue/QueueService.cs (MarkArrivedAsync) | F014 |

## Entries

```yaml
- id: F001
  file: src/api/Features/Patients/WalkinPatientService.cs
  cat: implementation-decision
  type: finding
  severity: HIGH
  issue: FR-025 partial — patient record created but no account credentials delivered
  cause: Task spec scoped to minimal Patient row only; no User entity, no temp password, no credentials email per FR-025 MUST
  date: 2026-05-21
  workflow: implement-tasks

- id: F002
  file: src/api/Features/Bookings/WalkinBookingController.cs
  cat: implementation-decision
  type: finding
  severity: HIGH
  issue: FR-028 gap — walk-in booking does not broadcast via SignalR
  cause: SignalR hub absent from codebase; UC-021 model requires BroadcastQueueUpdate; no infrastructure exists to call
  date: 2026-05-21
  workflow: implement-tasks

- id: F003
  file: src/api/Features/Bookings/WalkinBookingService.cs
  cat: implementation-decision
  type: decision
  severity: HIGH
  issue: Walk-in booking status set to Confirmed not WalkIn
  cause: AC-003 explicitly states status=Confirmed; model ERD defines WalkIn as separate status; AC takes precedence; CreatedByStaffId differentiates walk-ins
  date: 2026-05-21
  workflow: implement-tasks

- id: F004
  file: src/api/Features/Queue/QueueService.cs
  cat: implementation-decision
  type: decision
  severity: HIGH
  issue: PatientName from plain strings not IPhiEncryptionService.Decrypt
  cause: Task plan assumed encrypted FullNameEncrypted column; actual Patient entity stores FirstName/LastName as plain string columns; IPhiEncryptionService not needed for name fields
  date: 2026-05-21
  workflow: implement-tasks

- id: F005
  file: src/api/Data/Entities/Booking.cs
  cat: implementation-decision
  type: decision
  severity: HIGH
  issue: CheckedInAt column added in us_031 not us_030 as dependency stated
  cause: us_030 task_001 did not add CheckedInAt or QueuePosition; added CheckedInAt here via migration 20260521170000; QueuePosition computed dynamically as row index
  date: 2026-05-21
  workflow: implement-tasks

- id: F006
  file: frontend/src/pages/QueuePage.tsx
  cat: implementation-decision
  type: decision
  severity: HIGH
  issue: Implemented in QueuePage.tsx not QueueDashboardPage.tsx; task path src/web/src/ does not match actual frontend/src/
  cause: Router already imports QueuePage from pages/QueuePage; task Expected Changes listed wrong path (src/web/src/); implemented stub replacement at correct path to avoid creating dead unreferenced code
  date: 2026-05-21
  workflow: implement-tasks

- id: F007
  file: src/api/Features/Queue/QueueService.cs
  cat: implementation-decision
  type: decision
  severity: HIGH
  issue: MarkArrivedAsync uses status "CheckedIn" not "Arrived"; idempotent check is booking.Status == "CheckedIn"
  cause: Task plan referenced BookingStatus.Arrived but no BookingStatus enum exists; CheckedIn IS the arrived state based on existing column name CheckedInAt and GET queue filter from us_031
  date: 2026-05-21
  workflow: implement-tasks

- id: F008
  file: src/api/Features/Queue/QueueController.cs
  cat: implementation-decision
  type: decision
  severity: HIGH
  issue: Route uses int bookingId not Guid queueEntryId
  cause: Task plan assumed Guid PK but Booking.Id is int (established in us_031 session); route constraint [HttpPatch("{bookingId:int}/arrived")] enforces int type for OWASP A03
  date: 2026-05-21
  workflow: implement-tasks

- id: F009
  file: frontend/src/pages/QueuePage.tsx
  cat: implementation-decision
  type: decision
  severity: HIGH
  issue: Button condition is entry.status === 'Confirmed' not 'Waiting'; success updates to 'CheckedIn' not 'Arrived'
  cause: GET /api/queue returns status strings 'Confirmed' and 'CheckedIn' (established us_031); task plan assumed 'Waiting'/'Arrived' status values that do not exist in the API contract
  date: 2026-05-21
  workflow: implement-tasks

- id: F010
  file: src/api/Features/Queue/QueueEntryDto.cs
  cat: implementation-decision
  type: decision
  severity: HIGH
  issue: int Id field added to QueueEntryDto (and QueueEntry frontend interface) not specified in us_031
  cause: Frontend needs booking PK to construct PATCH /api/queue/{id}/arrived URL; us_031 tasks did not expose Id in the DTO; added as minimal required field for us_032 PATCH to work
  date: 2026-05-21
  workflow: implement-tasks

- id: F011
  file: src/api/Features/Queue/QueueService.cs
  cat: implementation-decision
  type: decision
  severity: HIGH
  issue: since filter uses BookedAt + CheckedInAt as surrogates; Booking entity has no UpdatedAt column
  cause: Task plan referenced b.UpdatedAt >= since but Booking entity has no UpdatedAt; BookedAt is the creation timestamp; CheckedInAt is the status-change timestamp; used b.BookedAt >= sinceUtc || (b.CheckedInAt != null && b.CheckedInAt >= since.Value) as equivalent filter
  date: 2026-05-21
  workflow: implement-tasks

- id: F012
  file: src/api/Features/Admin/AdminMetricsService.cs
  cat: implementation-decision
  type: decision
  severity: HIGH
  issue: Task plan used b.Slot.StartTime but actual nav property is b.AppointmentSlot.SlotStart; BookingStatus enum does not exist — used string literals
  cause: Task plan incorrectly named the AppointmentSlot navigation property and StartTime column; actual EF Core entity uses AppointmentSlot as nav property and SlotStart as DateTime column; status is string not enum (established F007)
  date: 2026-05-21
  workflow: implement-tasks

- id: F013
  file: src/api/Features/Admin/AdminMetricsService.cs
  cat: implementation-decision
  type: decision
  severity: HIGH
  issue: Booking.IsWalkin does not exist; used CreatedByStaffId != null as walk-in proxy
  cause: Task plan referenced b.IsWalkin boolean but Booking entity has no such column; CreatedByStaffId is populated only for walk-in bookings created by staff (established in us_030/F003)
  date: 2026-05-21
  workflow: implement-tasks

- id: F014
  file: src/api/Features/Queue/QueueService.cs (MarkArrivedAsync)
  cat: implementation-decision
  type: decision
  severity: HIGH
  issue: QueueEntryUpdated broadcast DTO uses Position=0; frontend QueueEntryUpdated handler merges only status + arrivalTime to preserve existing position
  cause: MarkArrivedAsync has no access to the 1-based queue position (which is computed via row-number ordering in GetQueueAsync); computing position post-save requires an extra COUNT query; position does not change when status changes; frontend handler { ...r, status, arrivalTime } preserves all other fields including position
  date: 2026-05-21
  workflow: implement-tasks
```
