# Task - TASK_001

## Requirement Reference
- **User Story:** us_042
- **Story Location:** .propel/context/tasks/EP-007-II/us_042/us_042.md
- **Acceptance Criteria:**
  - AC-002: `PATCH /clinical-conflicts/{id}/resolve` with `{"resolution": "Resolved", "note": "<text>"}` returns HTTP 200; updates `clinical_conflicts.status = "Resolved"`, `resolved_by = actingUserId`, `resolved_at = UtcNow`; writes audit log entry `ActionType: ConflictResolved`
  - AC-003: `PATCH /clinical-conflicts/{id}/resolve` with `{"resolution": "Dismissed"}` updates status to `"Dismissed"` with optional note; writes audit log entry `ActionType: ConflictDismissed`
  - AC-004: No code change required — `GET /patients/{id}/summary` already filters by `status = "Open"` (established in us_041); resolved/dismissed conflicts are automatically excluded from the next call
- **Edge Cases:**
  - Already resolved/dismissed: if `clinical_conflicts.status != "Open"`, return HTTP 409 `{"error": "This conflict has already been resolved or dismissed."}`
  - Note exceeds 1,000 characters: return HTTP 400 `{"error": "Resolution note must be 1,000 characters or fewer."}`

---

## Design References
| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes — provides `PATCH /clinical-conflicts/{id}/resolve` consumed by task_002 (MOD-005 drawer) |
| **Figma URL** | N/A |
| **Wireframe Status** | AVAILABLE |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | .propel/context/wireframes/Hi-Fi/wireframe-MOD-005-resolve-conflict-drawer.html |
| **Screen Spec** | MOD-005 (Resolve Conflict Drawer) |
| **UXR Requirements** | N/A |
| **Design Tokens** | N/A |

---

## AI References
| Reference Type | Value |
|----------------|-------|
| **AI Impact** | No |
| **AIR Requirements** | N/A — this task records the human resolution of an AI-flagged conflict; no Ollama calls are made |
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
| Backend | ASP.NET Core Web API (C#) | .NET 8.0 | TR-005 — `ClinicalConflictsController` with `PATCH /{id}/resolve` action; `[Authorize(Roles)]` RBAC; data annotations for DTO validation (AC-002, AC-003) |
| ORM | EF Core + Npgsql.EntityFrameworkCore.PostgreSQL | 8.x | TR-006 — `FindAsync` for existence + status check; `ExecuteUpdateAsync` with Status == "Open" race-condition guard for update; `IsolationLevel` not required — single row update is atomic (AC-002, AC-003; OWASP A04) |
| Database | PostgreSQL 15.3+ | 15.3+ | TR-007 — three nullable columns added to `clinical_conflicts`: `resolved_by uuid`, `resolved_at timestamptz`, `resolution_note text` (AC-002, AC-003) |
| Audit | IAuditLogService (us_014) | .NET 8.0 | TR-011 — `LogAsync(ActionType, EntityId, EntityType, PerformedBy, Note)` called after successful update; resolution note stored in audit log DB only — never in ILogger (OWASP A02) |
| Logging | Serilog + Seq | .NET 8.0 compatible / 2023.4+ | TR-011 — only `conflictId` (UUID) logged in error paths; resolution note, entity values, and patient names never in ILogger calls (OWASP A02) |

---

## Task Overview

Implement `PATCH /clinical-conflicts/{id}/resolve` in `ClinicalConflictsController`. The action validates the request DTO (resolution enum, note length, note required for Resolved), checks that the conflict exists and is still Open, atomically updates the three resolution columns with a race-condition guard, and writes an audit log entry via `IAuditLogService`. An EF Core migration adds the nullable resolution columns to `clinical_conflicts`. Resolution notes are never written to ILogger.

---

## Dependent Tasks
- task_001 (us_041) — `clinical_conflicts` table and `ClinicalConflict` EF Core entity must exist; `IAuditLogService` must be registered
- task_001 (us_014) — `IAuditLogService` must exist and be injectable

---

## Impacted Components
- `src/api/Controllers/ClinicalConflictsController.cs` — new: `PATCH /{id}/resolve` action
- `src/api/Features/Conflicts/ResolveConflictRequest.cs` — new: `{ string Resolution, string? Note }` with data annotations
- `src/api/Features/Conflicts/ClinicalConflict.cs` — modified (us_041 entity): add `Guid? ResolvedBy`, `DateTimeOffset? ResolvedAt`, `string? ResolutionNote` properties
- EF Core migration `AddConflictResolutionColumns` — new: three nullable columns on `clinical_conflicts`

---

## Implementation Plan
1. `PATCH /clinical-conflicts/{id}/resolve` in `ClinicalConflictsController`: `[Authorize(Roles = "Staff,Admin,Clinician")]`; `[HttpPatch("{id:guid}/resolve")]`; resolverUserId extracted via `User.FindFirstValue(ClaimTypes.NameIdentifier)`; `ResolveConflictRequest` bound from request body with `[FromBody]`; returns `Ok()` on success — no body needed since the drawer closes immediately (AC-002, AC-003; OWASP A01 — role enforced at attribute level)
2. `ResolveConflictRequest` DTO with data annotations: `[Required] string Resolution` validated against `_validResolutions = new HashSet<string>{"Resolved","Dismissed"}`; `[MaxLength(1000)] string? Note`; manual validation in action: if `!_validResolutions.Contains(request.Resolution)` → return `BadRequest(new { error = "Resolution must be 'Resolved' or 'Dismissed'." })`; if `request.Note?.Length > 1000` → return `BadRequest(new { error = "Resolution note must be 1,000 characters or fewer." })`; if `request.Resolution == "Resolved" && string.IsNullOrWhiteSpace(request.Note)` → return `BadRequest(new { error = "Resolution note is required when marking as Resolved." })` (AC-002; Edge: note > 1000; OWASP A03 — validated at boundary)
3. Existence and status guard: `var conflict = await db.ClinicalConflicts.FindAsync(new object[] { id }, ct)` — if null → return `NotFound()`; if `conflict.Status != "Open"` → return `StatusCode(409, new { error = "This conflict has already been resolved or dismissed." })` — 404 for non-existent (no information disclosure about ownership), 409 for already-actioned (Edge: already resolved; OWASP A01 — role-checked before reaching handler)
4. Atomic status update with race-condition guard: `int updated = await db.ClinicalConflicts.Where(cc => cc.Id == id && cc.Status == "Open").ExecuteUpdateAsync(s => s.SetProperty(cc => cc.Status, request.Resolution).SetProperty(cc => cc.ResolvedBy, Guid.Parse(resolverUserId!)).SetProperty(cc => cc.ResolvedAt, DateTimeOffset.UtcNow).SetProperty(cc => cc.ResolutionNote, request.Note), ct)` — the `&& cc.Status == "Open"` guard means a concurrent second call that races past the FindAsync guard will update 0 rows; if `updated == 0` → return `StatusCode(409, new { error = "This conflict has already been resolved or dismissed." })` (AC-002, AC-003; OWASP A04 — TOCTOU race condition guard)
5. Audit log after successful update: `await _auditLogService.LogAsync(new AuditLogEntry { ActionType = request.Resolution == "Resolved" ? "ConflictResolved" : "ConflictDismissed", EntityId = id.ToString(), EntityType = "ClinicalConflict", PerformedBy = resolverUserId!, Timestamp = DateTimeOffset.UtcNow, Note = request.Note })` — `request.Note` is persisted to the audit log DB table only; it is never passed to any `_logger` method (AC-002, AC-003; OWASP A02 — clinical notes may contain PHI or sensitive medication information)
6. EF Core migration `AddConflictResolutionColumns`: `migrationBuilder.AddColumn<Guid?>("resolved_by", "clinical_conflicts", nullable: true)`; `migrationBuilder.AddColumn<DateTimeOffset?>("resolved_at", "clinical_conflicts", nullable: true)`; `migrationBuilder.AddColumn<string?>("resolution_note", "clinical_conflicts", nullable: true, maxLength: null)` — text column; add FK: `migrationBuilder.AddForeignKey("FK_clinical_conflicts_users_resolved_by", "clinical_conflicts", "resolved_by", "users", principalColumn: "id", onDelete: ReferentialAction.SetNull)` — `SET NULL` on user delete preserves audit history without dangling FK errors (AC-002, AC-003)

---

## Current Project State
```
src/
└── api/
    ├── Controllers/
    │   └── (ClinicalConflictsController.cs     — CREATE)
    └── Features/
        └── Conflicts/
            ├── (ResolveConflictRequest.cs       — CREATE)
            └── ClinicalConflict.cs             (MODIFY — add 3 nullable resolution properties)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | src/api/Controllers/ClinicalConflictsController.cs | PATCH /{id}/resolve action with validation, guard, update, and audit log |
| CREATE | src/api/Features/Conflicts/ResolveConflictRequest.cs | DTO { string Resolution, string? Note } with data annotations |
| MODIFY | src/api/Features/Conflicts/ClinicalConflict.cs | Add Guid? ResolvedBy, DateTimeOffset? ResolvedAt, string? ResolutionNote |
| CREATE | src/api/Migrations/AddConflictResolutionColumns.cs | resolved_by, resolved_at, resolution_note nullable columns + FK |

---

## External References
- https://learn.microsoft.com/en-us/ef/core/saving/execute-insert-update-delete#executeupdateasync (EF Core `ExecuteUpdateAsync` with WHERE clause — atomic update with race-condition guard; OWASP A04)
- https://learn.microsoft.com/en-us/aspnet/core/mvc/models/validation (ASP.NET Core model validation — `[Required]`, `[MaxLength]` data annotations on `ResolveConflictRequest`; AC-002 validation)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [x] Call `PATCH /clinical-conflicts/{id}/resolve` with `{"resolution": "Resolved", "note": "Override — clinical rationale documented."}` for an Open conflict; verify HTTP 200, `status = 'Resolved'`, `resolved_by = actingUserId`, `resolved_at ≈ now()`, audit log entry `ActionType = 'ConflictResolved'` with note (AC-002)
- [x] Call `PATCH /clinical-conflicts/{id}/resolve` with `{"resolution": "Dismissed"}` (no note); verify HTTP 200, `status = 'Dismissed'`, audit log `ActionType = 'ConflictDismissed'` (AC-003)
- [x] Call the endpoint twice concurrently for the same Open conflict; verify only one row is updated and both calls return either 200 or 409 (no duplicate updates) (OWASP A04 — race condition)
- [x] Call with a conflict that already has `status = 'Resolved'`; verify HTTP 409 `{"error": "This conflict has already been resolved or dismissed."}` (Edge: already resolved)
- [x] Call with `{"resolution": "Resolved", "note": "x".repeat(1001)}`; verify HTTP 400 `{"error": "Resolution note must be 1,000 characters or fewer."}` (Edge: note > 1000)
- [x] Call with `{"resolution": "Resolved"}` (no note); verify HTTP 400 "Resolution note is required when marking as Resolved." (AC-002 — note required for Resolved)
- [x] Verify Serilog output contains only `conflictId` UUID in error paths; resolution note does not appear in any log entry (OWASP A02)

---

## Implementation Checklist
- [x] Validation in item 2 runs in order: (1) resolution enum check, (2) note length check, (3) note required check — all return early with `BadRequest` before any DB call; no DB round trip is made for invalid input (OWASP A03 — validate at boundary before data access)
- [x] The `ExecuteUpdateAsync` WHERE clause includes `cc.Status == "Open"` in addition to `cc.Id == id` — this is the atomic TOCTOU guard that handles concurrent calls; the result of `updated == 0` is always checked and returns 409 if the status has already changed between the `FindAsync` guard and the update (OWASP A04 — race condition)
- [x] `request.Note` is passed to `_auditLogService.LogAsync(... Note = request.Note)` only — it is never passed to `_logger.LogInformation`, `_logger.LogError`, or any other `ILogger` method in the controller; the audit log persists it to the DB table, not to Seq (OWASP A02 — resolution notes may contain PHI)
- [x] `ResolvedBy` is parsed from `User.FindFirstValue(ClaimTypes.NameIdentifier)` which is the authenticated user's UUID from the JWT — never from the request body (OWASP A01 — the acting user is always the authenticated caller, not a caller-supplied value)
- [x] The FK `resolved_by → users(id)` uses `ON DELETE SET NULL` — if the user account is later deleted, the resolution record is preserved with `resolved_by = NULL` rather than cascade-deleting audit history (data integrity; audit trail preservation)
- [x] `ClinicalConflictsController` does not check patient ownership of the conflict — any authenticated Staff/Admin/Clinician can resolve any patient's conflict; this is intentional (clinical staff act on behalf of the care team) and documented here; RBAC via `[Authorize(Roles)]` is the sole access gate (OWASP A01 — intentional design; documented)
