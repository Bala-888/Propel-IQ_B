# Task - TASK_002

## Requirement Reference
- **User Story:** us_013
- **Story Location:** .propel/context/tasks/EP-002/us_013/us_013.md
- **Acceptance Criteria:**
  - AC-005: When three HTTP 403 responses are generated from the same source IP within 10 minutes, an `AdminNotification` record with `AlertType: RepeatedUnauthorizedAccess` and `SourceIp` is inserted — the record must be visible in the admin KPI dashboard
- **Edge Cases:**
  - No additional database-layer edge cases; the 10-minute window and threshold logic reside in the backend service (`RepeatedUnauthorizedAccessTracker` — task_001)

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
| Database | PostgreSQL | 15.3+ | TR-003 (stores AdminNotification rows; append-only by convention — no UPDATE/DELETE) |
| Backend | EF Core + Npgsql.EntityFrameworkCore.PostgreSQL | 8.x | TR-002 (ORM for AdminNotification entity; migration-managed schema change) |

---

## Task Overview

Create the `AdminNotification` EF Core entity, add it to `AppDbContext`, generate the migration, and implement `IAdminNotificationRepository` with an `InsertAsync` method. This provides the persistence layer that `task_001`'s `RepeatedUnauthorizedAccessTracker` depends on to store repeated-unauthorized-access alerts. The table is append-only; no UPDATE or DELETE operations are exposed by the repository.

---

## Dependent Tasks
- task_002 (us_005) — `AppDbContext` with EF Core + Npgsql must be initialised; PostgreSQL schema must exist
- task_002 (us_005) — EF Core naming conventions (`UseSnakeCaseNamingConventions`) must already be applied so column names are consistent

---

## Impacted Components
- `src/api/Entities/AdminNotification.cs` — new EF Core entity
- `src/api/Data/AppDbContext.cs` — modified: add `DbSet<AdminNotification> AdminNotifications`
- `src/api/Repositories/IAdminNotificationRepository.cs` — new interface
- `src/api/Repositories/AdminNotificationRepository.cs` — new implementation
- `src/api/Migrations/` — new migration file: `AddAdminNotificationTable`
- `src/api/Program.cs` — modified: register `IAdminNotificationRepository` as scoped

---

## Implementation Plan
1. Create `AdminNotification` entity class in `src/api/Entities/AdminNotification.cs`: `Guid Id` (default `Guid.NewGuid()`), `string AlertType`, `string SourceIp`, `Guid? ActorId`, `string? ActorRole`, `DateTime CreatedAt` (UTC); no navigation properties — the table is standalone and append-only (AC-005)
2. Add `public DbSet<AdminNotification> AdminNotifications { get; set; }` to `AppDbContext`; in `OnModelCreating` configure: `HasKey(x => x.Id)`, `Property(x => x.AlertType).HasMaxLength(64).IsRequired()`, `Property(x => x.SourceIp).HasMaxLength(45).IsRequired()` (IPv6 max length), `Property(x => x.CreatedAt).HasDefaultValueSql("now()")` (AC-005)
3. Run `dotnet ef migrations add AddAdminNotificationTable --project src/api`; open the generated migration file and verify it creates only the `admin_notifications` table without altering any other table; ensure `Down()` drops the table cleanly (AC-005 — migration hygiene)
4. Create `IAdminNotificationRepository` with single method `Task InsertAsync(AdminNotification notification, CancellationToken ct = default)`; create `AdminNotificationRepository : IAdminNotificationRepository` that calls `_context.AdminNotifications.Add(notification); await _context.SaveChangesAsync(ct)` — `SaveChangesAsync` is scoped to this insert only; no shared unit-of-work with other repositories to ensure the alert is persisted even if a larger transaction rolls back (AC-005 — isolation)
5. Register `services.AddScoped<IAdminNotificationRepository, AdminNotificationRepository>()` in `Program.cs` (AC-005)

---

## Current Project State
```
src/
└── api/
    ├── Entities/
    │   └── AdminNotification.cs                     (CREATE)
    ├── Data/
    │   └── AppDbContext.cs                          (MODIFY — DbSet<AdminNotification>)
    ├── Repositories/
    │   ├── IAdminNotificationRepository.cs          (CREATE)
    │   └── AdminNotificationRepository.cs           (CREATE)
    ├── Migrations/
    │   └── <timestamp>_AddAdminNotificationTable.cs (CREATE — generated by EF CLI)
    └── Program.cs                                   (MODIFY — register IAdminNotificationRepository)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | src/api/Entities/AdminNotification.cs | EF Core entity: Id, AlertType, SourceIp, ActorId?, ActorRole?, CreatedAt |
| MODIFY | src/api/Data/AppDbContext.cs | Add DbSet<AdminNotification>; configure column constraints in OnModelCreating |
| CREATE | src/api/Repositories/IAdminNotificationRepository.cs | Interface: InsertAsync |
| CREATE | src/api/Repositories/AdminNotificationRepository.cs | Implementation: Add + SaveChangesAsync (isolated) |
| CREATE | src/api/Migrations/<timestamp>_AddAdminNotificationTable.cs | EF Core migration: creates admin_notifications table |
| MODIFY | src/api/Program.cs | Register IAdminNotificationRepository as scoped |

---

## External References
- https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/?tabs=dotnet-core-cli (EF Core 8 migrations — generating and verifying migration files)
- https://www.npgsql.org/efcore/index.html (Npgsql EF Core provider — snake_case naming and PostgreSQL-specific column types)
- https://learn.microsoft.com/en-us/ef/core/modeling/entity-types?tabs=data-annotations (EF Core 8 entity configuration — fluent API for column constraints)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] After running the migration, inspect the PostgreSQL schema; verify the `admin_notifications` table exists with columns `id`, `alert_type`, `source_ip`, `actor_id`, `actor_role`, `created_at` in snake_case (AC-005 — EF naming conventions)
- [ ] Call `IAdminNotificationRepository.InsertAsync` with a test notification; verify the row is present in `admin_notifications` with the correct field values (AC-005)
- [ ] Roll back the migration with `dotnet ef database update <previous>`; verify the `admin_notifications` table is removed and no other tables are affected (AC-005 — migration Down() integrity)
- [ ] Confirm the migration `Up()` SQL contains only a `CREATE TABLE admin_notifications` statement — no `ALTER TABLE` on existing tables (AC-005 — schema isolation)

---

## Implementation Checklist
- [ ] `AdminNotification.SourceIp` column max length is 45 characters — accommodates full IPv6 addresses (e.g. `2001:0db8:85a3:0000:0000:8a2e:0370:7334`) as well as IPv4-mapped IPv6 addresses (AC-005; OWASP A05 — no truncation of source IP data)
- [ ] `AdminNotificationRepository.InsertAsync` calls its own `SaveChangesAsync` in isolation — the notification insert is not batched with any other DB operation so a failure in a surrounding transaction cannot suppress the security alert (AC-005; OWASP A09 — audit record must persist)
- [ ] `AdminNotification` entity has no DELETE or UPDATE operations exposed through the repository interface — the table is append-only to prevent tampering with the security alert history (AC-005; OWASP A09 — tamper-evident log)
- [ ] `CreatedAt` uses `HasDefaultValueSql("now()")` — the timestamp is set by the database server, not the application, preventing clock-skew manipulation (AC-005; OWASP A09)
- [ ] The migration `Down()` method contains only `migrationBuilder.DropTable("admin_notifications")` — no references to other tables; verified before applying to any shared environment (AC-005 — reversibility)
