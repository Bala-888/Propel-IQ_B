# Task - TASK_002

## Requirement Reference
- **User Story:** us_014
- **Story Location:** .propel/context/tasks/EP-002/us_014/us_014.md
- **Acceptance Criteria:**
  - AC-001: `audit_logs` table contains `ip_address` and `user_agent` columns that are populated on every audit INSERT (these columns are required by the `PostgresAuditLogger` in task_001)
  - AC-004: `app_user` database role cannot execute `DELETE FROM audit_logs` or `UPDATE audit_logs SET ...` — PostgreSQL raises a permission error; the restriction is enforced at the DB layer, independent of application code
- **Edge Cases:**
  - Concurrent writes under load: the `audit_logs` primary key must be a PostgreSQL `BIGSERIAL` (identity sequence) rather than `UUID v4` to eliminate B-tree index fragmentation caused by random UUID inserts under 50+ concurrent write load

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
| Database | PostgreSQL | 15.3+ | TR-003 (BIGSERIAL sequence PK; REVOKE statement for app_user; append-only enforcement) |
| Backend | EF Core + Npgsql.EntityFrameworkCore.PostgreSQL | 8.x | TR-002 (AuditLog entity PK type change to long; migration generation) |

---

## Task Overview

Update the `audit_logs` schema to meet the concurrency and column requirements of us_014. If the `id` column is currently `UUID`, migrate it to `BIGSERIAL` (PostgreSQL identity sequence) to prevent B-tree fragmentation under concurrent inserts. Add `ip_address varchar(45)` and `user_agent varchar(512)` columns required by AC-001. Re-enforce `app_user` REVOKE in the migration SQL to guarantee that no application path — present or future — can DELETE or UPDATE audit rows. Update the `AuditLog` EF Core entity to reflect the `long` primary key.

---

## Dependent Tasks
- task_002 (us_006) — `audit_logs` table with INSERT-only `app_user` privilege must already exist; this migration extends it rather than creating it from scratch
- task_001 (us_005) — `AppDbContext` and EF Core + Npgsql must be initialised; `UseSnakeCaseNamingConventions()` must be in place

---

## Impacted Components
- `src/api/Entities/AuditLog.cs` — modified: change `Id` type from `Guid` to `long`; add `IpAddress` and `UserAgent` string properties
- `src/api/Data/AppDbContext.cs` — modified: configure `AuditLog.Id` as `NpgsqlValueGenerationStrategy.IdentityByDefaultColumn`; set column max-length constraints
- `src/api/Migrations/<timestamp>_AuditLogSchemaV2.cs` — new migration: BIGSERIAL PK, new columns, REVOKE statement

---

## Implementation Plan
1. Update `AuditLog` entity: change `public Guid Id` to `public long Id`; add `public string IpAddress { get; set; } = string.Empty` and `public string UserAgent { get; set; } = string.Empty`; existing properties `ActorId`, `ActorRole`, `ActionType`, `ResourceType`, `ResourceId`, `OccurredAt` remain unchanged (AC-001; Edge: BIGSERIAL)
2. Update `AppDbContext.OnModelCreating` for `AuditLog`: add `.HasAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)` on the `Id` property to map it to `BIGSERIAL`; configure `Property(x => x.IpAddress).HasMaxLength(45).IsRequired()` and `Property(x => x.UserAgent).HasMaxLength(512).IsRequired(false)` (AC-001; Edge: BIGSERIAL)
3. Run `dotnet ef migrations add AuditLogSchemaV2 --project src/api`; open the generated migration file and verify:
   - If Id was previously UUID: the `Up()` method alters the column type or drops + re-creates the PK as BIGSERIAL
   - `ip_address varchar(45)` and `user_agent varchar(512)` columns are added
   - No other tables are altered (AC-001; Edge: BIGSERIAL — migration hygiene)
4. Append a raw SQL step to the migration `Up()` method: `migrationBuilder.Sql("REVOKE UPDATE, DELETE ON audit_logs FROM app_user;")` — re-enforces the immutability constraint even if the role was inadvertently granted wider permissions during the schema change; add the corresponding `migrationBuilder.Sql("GRANT UPDATE, DELETE ON audit_logs TO app_user;")` in `Down()` (AC-004 — explicit, migration-managed enforcement)
5. Apply the migration to the local dev database: `dotnet ef database update`; connect to PostgreSQL directly and run `\d audit_logs` to confirm column types and `\dp audit_logs` to confirm `app_user` has only `INSERT` privilege (AC-001, AC-004 — manual verification before CI)

---

## Current Project State
```
src/
└── api/
    ├── Entities/
    │   └── AuditLog.cs                              (MODIFY — Id: long; + IpAddress, UserAgent)
    ├── Data/
    │   └── AppDbContext.cs                          (MODIFY — BIGSERIAL annotation; column constraints)
    └── Migrations/
        └── <timestamp>_AuditLogSchemaV2.cs          (CREATE — generated by EF CLI)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | src/api/Entities/AuditLog.cs | Id: Guid → long; add IpAddress (string) and UserAgent (string) |
| MODIFY | src/api/Data/AppDbContext.cs | BIGSERIAL identity annotation; MaxLength for IpAddress (45) and UserAgent (512) |
| CREATE | src/api/Migrations/<timestamp>_AuditLogSchemaV2.cs | BIGSERIAL PK; new columns; REVOKE UPDATE+DELETE for app_user |

---

## External References
- https://www.npgsql.org/efcore/modeling/generated-properties.html (Npgsql EF Core — NpgsqlValueGenerationStrategy.IdentityByDefaultColumn for BIGSERIAL)
- https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/operations (EF Core 8 — custom migration operations including raw SQL via migrationBuilder.Sql)
- https://www.postgresql.org/docs/15/sql-revoke.html (PostgreSQL 15 REVOKE — removing UPDATE/DELETE from a role)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] Run `\d audit_logs` in psql; verify `id` column type is `bigint` generated by default as identity (BIGSERIAL); verify `ip_address varchar(45)` and `user_agent varchar(512)` columns are present (AC-001; Edge: BIGSERIAL)
- [ ] Run `\dp audit_logs` in psql; verify `app_user` has only `INSERT` privilege — no `UPDATE` or `DELETE` in the privilege list (AC-004)
- [ ] As `app_user`, attempt `DELETE FROM audit_logs WHERE id = 1`; verify PostgreSQL raises `ERROR: permission denied for table audit_logs` (AC-004)
- [ ] As `app_user`, attempt `UPDATE audit_logs SET action_type = 'Modified' WHERE id = 1`; verify the same permission error (AC-004)
- [ ] Roll back the migration with `dotnet ef database update <previous>`; verify the migration `Down()` method restores the schema cleanly without errors (Edge: reversibility)

---

## Implementation Checklist
- [ ] `AuditLog.Id` is changed to `long` and configured with `NpgsqlValueGenerationStrategy.IdentityByDefaultColumn` — PostgreSQL generates a monotonically increasing sequence value, eliminating the random UUID B-tree fragmentation that causes page splits under concurrent inserts (Edge: BIGSERIAL; performance under 50+ concurrent writes)
- [ ] `IpAddress` column is constrained to `varchar(45)` — the maximum length of an IPv6 address including the IPv4-mapped form `::ffff:192.168.0.1`; values longer than 45 characters indicate malformed input and must be truncated or rejected before insert (AC-001; OWASP A03)
- [ ] `REVOKE UPDATE, DELETE ON audit_logs FROM app_user` is applied in the migration `Up()` method, not in a separate script — ensures the permission constraint is version-controlled and applied atomically with the schema change (AC-004; OWASP A09)
- [ ] Migration `Down()` grants back `UPDATE, DELETE` to `app_user` so rollback does not leave the database in a broken state for earlier migrations that may have relied on those privileges (AC-004 — reversibility)
- [ ] Migration `Up()` touches only the `audit_logs` table — verified by inspection of the generated migration file before applying to any shared environment (AC-001, AC-004 — schema isolation)
