# Task - TASK_001

## Requirement Reference
- **User Story:** us_029
- **Story Location:** .propel/context/tasks/EP-005/us_029/us_029.md
- **Acceptance Criteria:**
  - AC-002: `PATCH /patients/{id}/preferences` called with changed field only; HTTP 200; updated preference object returned — no page reload required
  - AC-003: Downstream notification services (us_026, us_027) read `PatientPreferences` fields before dispatching; this story defines the schema and defaults that those services consume
  - AC-004: New patient account creation inserts a `patient_preferences` row with `email_notifications_enabled = true`, `sms_notifications_enabled = true`, `slot_swap_notifications_enabled = true`, `google_calendar_sync_enabled = false`, `outlook_calendar_sync_enabled = false`
- **Edge Cases:**
  - PATCH failure is handled entirely on the frontend (revert + toast); the backend must return a well-formed non-200 on validation errors so the frontend can detect failure reliably
  - All channels disabled is a valid persisted state — the backend must not reject a PATCH that sets all five fields to false

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
| Backend | ASP.NET Core Web API (C#) | .NET 8.0 | TR-005 — `PatientPreferencesController` with `PATCH` action; ownership guard via JWT (AC-002) |
| ORM | EF Core + Npgsql.EntityFrameworkCore.PostgreSQL | 8.x | TR-006 — `PatientPreferences` entity with EF Core `HasDefaultValue` for privacy-by-default fields (AC-004); partial update via nullable DTO fields |
| Database | PostgreSQL 15.3+ | 15.3+ | TR-007 — `patient_preferences` table; column defaults enforced at DB level via EF Core migration |
| Audit | AuditDbContext + IAuditService | Project-established | TR-014 — log `PreferenceUpdated` with patientId and changed field name; no preference values in log (OWASP A02; AC-002 audit) |

---

## Task Overview

Implement `PATCH /api/patients/{id}/preferences` to persist individual notification and calendar sync preference changes. The endpoint accepts a partial update DTO containing only the changed field(s) with nullable booleans. Ownership is enforced via JWT — a patient can only update their own preferences. EF Core column-level defaults ensure all new accounts receive the correct opt-in/opt-out starting state. The endpoint returns the full updated preferences object on 200, enabling the frontend to confirm the persisted value.

---

## Dependent Tasks
- task_001 (us_009) — `PatientPreferences` entity and `patient_preferences` table may already exist with `email_notifications_enabled` and `sms_notifications_enabled` columns; this task extends it with `slot_swap_notifications_enabled`, `google_calendar_sync_enabled`, `outlook_calendar_sync_enabled` if not already present; verify before adding migration

---

## Impacted Components
- `src/api/Features/Patients/PatientPreferencesController.cs` — new (or modified if partial controller already exists): `PATCH /api/patients/{id}/preferences` action
- `src/api/Features/Patients/PatientPreferencesService.cs` — new: partial update logic with nullable DTO field application
- `src/api/Features/Patients/IPatientPreferencesService.cs` — new: interface with `PatchAsync(Guid patientId, PatchPreferencesDto dto)`
- `src/api/Features/Patients/PatchPreferencesDto.cs` — new: DTO with 5 nullable bool fields; only non-null fields are applied to the entity
- `src/api/Domain/Entities/PatientPreferences.cs` — modified: add `slot_swap_notifications_enabled`, `google_calendar_sync_enabled`, `outlook_calendar_sync_enabled` bool properties if absent
- `src/api/Infrastructure/Persistence/AppDbContext.cs` — modified: configure default values for all 5 preference fields in `OnModelCreating`; generate EF Core migration

---

## Implementation Plan
1. Verify `PatientPreferences` entity from us_009; add missing bool properties `SlotSwapNotificationsEnabled`, `GoogleCalendarSyncEnabled`, `OutlookCalendarSyncEnabled` if not present; configure EF Core defaults: `HasDefaultValue(true)` for the three notification fields, `HasDefaultValue(false)` for the two calendar sync fields; generate migration (AC-004)
2. Create `PatchPreferencesDto`: `bool? EmailNotificationsEnabled`, `bool? SmsNotificationsEnabled`, `bool? SlotSwapNotificationsEnabled`, `bool? GoogleCalendarSyncEnabled`, `bool? OutlookCalendarSyncEnabled` — all nullable; a field set to `null` means "not changed" (AC-002; OWASP A03 — only these 5 fields accepted)
3. Create `PATCH /api/patients/{id}/preferences` action in `PatientPreferencesController`: `[Authorize(Roles = Roles.Patient)]`; extract `patientId` from JWT claim; if `patientId != id` → 403 Forbidden; call `IPatientPreferencesService.PatchAsync(patientId, dto)` (OWASP A01; AC-002)
4. `PatientPreferencesService.PatchAsync`: load `PatientPreferences` by `PatientId`; apply only non-null DTO fields to the entity using conditional assignments (`if (dto.SmsNotificationsEnabled.HasValue) prefs.SmsNotificationsEnabled = dto.SmsNotificationsEnabled.Value`); `await dbContext.SaveChangesAsync()` (AC-002; OWASP A03 — no mass-assignment of non-preference fields)
5. Return `200 OK` with a `PatientPreferencesResponse` DTO containing all 5 current field values — the frontend uses this to confirm the persisted value and does not rely on the local state after a successful PATCH (AC-002)
6. Audit log: `await _auditService.LogAsync(ActionType.PreferenceUpdated, patientId.ToString(), changedFieldName)` where `changedFieldName` is the name of the patched field — only the field name is logged, not its value; disabling all channels is a valid persisted state and does not generate a warning or rejection (AC-003; OWASP A02; Edge: all channels disabled)

---

## Current Project State
```
src/
└── api/
    ├── Domain/
    │   └── Entities/
    │       └── PatientPreferences.cs               (from us_009 — MODIFY: add 3 new bool properties)
    └── Features/
        └── Patients/
            ├── (IPatientPreferencesService.cs      — CREATE)
            ├── (PatientPreferencesService.cs       — CREATE)
            ├── (PatientPreferencesController.cs    — CREATE or MODIFY)
            └── (PatchPreferencesDto.cs             — CREATE)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | src/api/Domain/Entities/PatientPreferences.cs | Add SlotSwapNotificationsEnabled, GoogleCalendarSyncEnabled, OutlookCalendarSyncEnabled if absent |
| CREATE | src/api/Features/Patients/PatchPreferencesDto.cs | 5 nullable bool fields for partial update |
| CREATE | src/api/Features/Patients/IPatientPreferencesService.cs | Interface with PatchAsync |
| CREATE | src/api/Features/Patients/PatientPreferencesService.cs | Nullable field conditional apply + SaveChangesAsync |
| CREATE/MODIFY | src/api/Features/Patients/PatientPreferencesController.cs | PATCH action with JWT ownership guard |
| MODIFY | src/api/Infrastructure/Persistence/AppDbContext.cs | HasDefaultValue for all 5 fields; EF Core migration |

---

## External References
- https://learn.microsoft.com/en-us/ef/core/modeling/generated-properties?view=efcore-8.0 (EF Core HasDefaultValue — column-level default values enforced at DB level via migration; AC-004)
- https://learn.microsoft.com/en-us/aspnet/core/web-api/?view=aspnetcore-8.0 (ASP.NET Core 8 Web API — partial update pattern with nullable DTO; AC-002 PATCH semantics)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] `PATCH /api/patients/{id}/preferences` with `{"sms_notifications_enabled": false}`; verify 200 response with `sms_notifications_enabled = false` in body; verify DB row updated (AC-002)
- [ ] Send PATCH with only one field; verify no other fields are modified in DB (AC-002 — partial update, no mass-assignment)
- [ ] Create a new patient account; verify `patient_preferences` row has all notification fields `true` and calendar sync fields `false` (AC-004)
- [ ] Call `PATCH` as patient A with patient B's `{id}` in the route; verify 403 Forbidden (OWASP A01)
- [ ] PATCH all 5 fields to `false`; verify 200 and all fields persisted as false — no rejection or warning (Edge: all channels disabled)
- [ ] Verify audit log contains `ActionType = "PreferenceUpdated"` with `patientId` and changed field name; preference values are not logged (OWASP A02)

---

## Implementation Checklist
- [ ] `PatchPreferencesDto` uses nullable booleans for all 5 fields; `PatientPreferencesService.PatchAsync` applies only fields where `HasValue == true` — no other entity properties are touched by the PATCH path (OWASP A03; AC-002 partial update)
- [ ] EF Core `HasDefaultValue(true)` is configured for `email_notifications_enabled`, `sms_notifications_enabled`, and `slot_swap_notifications_enabled`; `HasDefaultValue(false)` for `google_calendar_sync_enabled` and `outlook_calendar_sync_enabled` — defaults applied at the PostgreSQL column level in the migration (AC-004 privacy-by-default)
- [ ] `patientId` is extracted from `User.FindFirstValue(ClaimTypes.NameIdentifier)` and compared against the `{id}` route parameter; mismatches return 403 before any DB access — no patient can modify another patient's preferences (OWASP A01; A07)
- [ ] The endpoint accepts a state where all 5 fields are `false` and persists it without validation error or warning; no business rule enforces a minimum of one enabled channel (Edge: all channels disabled)
- [ ] Audit log records only the field name (e.g., `"sms_notifications_enabled"`) and patientId; preference boolean values are not included in the log entry (OWASP A02 — no sensitive preference data in immutable audit log)
- [ ] The 200 response body contains the full updated `PatientPreferencesResponse` with all 5 current values — the frontend uses this to confirm persisted state rather than relying on local state alone (AC-002 — contract reliability)
