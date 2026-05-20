# Task - TASK_002

## Requirement Reference
- **User Story:** us_007
- **Story Location:** .propel/context/tasks/EP-DATA/us_007/us_007.md
- **Acceptance Criteria:**
  - AC-002: `bookings.no_show_risk_score` integer column with `CHECK (no_show_risk_score BETWEEN 0 AND 100)` constraint — enforced at the database level
  - AC-003: `bookings.risk_factors` `jsonb` column present in the schema
  - AC-004: `medical_code_suggestions.review_status` column with `DEFAULT 'Pending'` — plain INSERT without `review_status` returns `Pending`
  - AC-005: `clinical_documents.file_hash` `character varying(64)` column present in the schema
- **Edge Cases:**
  - Invalid `review_status` value: PostgreSQL must raise a check constraint violation if the inserted value is not in `{Pending, Accepted, Rejected}` — not silently stored

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
| Database | PostgreSQL | 15.3+ | TR-003, DR-004 (CHECK constraints, JSONB type, and column defaults are PostgreSQL 15.3+ DDL features) |
| Backend | EF Core (dotnet ef CLI) | 8.x | TR-003 (migration scaffolding; `dotnet ef migrations add` generates column DDL from task_001 model snapshot) |
| Backend | Npgsql.EntityFrameworkCore.PostgreSQL | 8.x | TR-003 (Npgsql generates correct `jsonb` column DDL when `HasColumnType("jsonb")` is configured) |

---

## Task Overview

With the entity model updated in task_001, scaffold the `AddSchemaFieldsV2` EF Core migration to generate `AddColumn` DDL for the four new columns. Manually edit the generated `Up()` method to append two raw SQL `ALTER TABLE ... ADD CONSTRAINT` statements — one for the `no_show_risk_score` range check and one for the `review_status` allowed-values check — because EF Core 8's Fluent API does not support arbitrary column-level CHECK constraints without raw SQL. Edit `Down()` symmetrically to drop both constraints before removing the columns.

---

## Dependent Tasks
- task_001 (us_007) — Entity model updates must be complete before `dotnet ef migrations add` can snapshot the new properties
- task_002 (us_005) — `InitialSchema` migration must already be applied so the target tables exist for `ALTER TABLE ADD COLUMN`

---

## Impacted Components
- `src/api/Migrations/YYYYMMDDHHMMSS_AddSchemaFieldsV2.cs` — new EF Core migration file (scaffolded then manually edited)
- `src/api/Migrations/AppDbContextModelSnapshot.cs` — updated model snapshot (auto-generated, do not hand-edit)

---

## Implementation Plan
1. From `src/api/`, run `dotnet ef migrations add AddSchemaFieldsV2` — confirm a new migration file is created at `src/api/Migrations/YYYYMMDDHHMMSS_AddSchemaFieldsV2.cs` containing `AddColumn` operations for `no_show_risk_score`, `risk_factors`, `review_status`, and `file_hash`
2. Verify the generated `AddColumn` for `risk_factors` uses `type: "jsonb"` (from task_001's `HasColumnType("jsonb")`) and the column for `review_status` carries `defaultValue: "Pending"`; if either is missing, it indicates a Fluent API configuration error in task_001 — fix before proceeding
3. After the last `AddColumn` call in `Up()`, append: `migrationBuilder.Sql("ALTER TABLE bookings ADD CONSTRAINT chk_no_show_risk_score CHECK (no_show_risk_score BETWEEN 0 AND 100);");` (AC-002)
4. After step 3 in `Up()`, append: `migrationBuilder.Sql("ALTER TABLE medical_code_suggestions ADD CONSTRAINT chk_review_status CHECK (review_status IN ('Pending', 'Accepted', 'Rejected'));");` (AC-004; Edge: invalid review_status)
5. In `Down()`, prepend before any `DropColumn` calls: `migrationBuilder.Sql("ALTER TABLE bookings DROP CONSTRAINT IF EXISTS chk_no_show_risk_score;");` and `migrationBuilder.Sql("ALTER TABLE medical_code_suggestions DROP CONSTRAINT IF EXISTS chk_review_status;");` — ensures the migration is safely reversible
6. Apply with `dotnet ef database update` and verify all four columns appear in `information_schema.columns` with the correct `data_type` entries

---

## Current Project State
```
src/
└── api/
    └── Migrations/
        ├── YYYYMMDDHHMMSS_InitialSchema.cs           (EXISTING — from us_005 task_002)
        ├── AppDbContextModelSnapshot.cs              (UPDATE — auto-generated, do not hand-edit)
        └── YYYYMMDDHHMMSS_AddSchemaFieldsV2.cs       (SCAFFOLD then EDIT)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE (scaffold + edit) | src/api/Migrations/YYYYMMDDHHMMSS_AddSchemaFieldsV2.cs | EF Core migration adding 4 new columns; CHECK constraints for `no_show_risk_score` and `review_status` appended as raw SQL in `Up()`; symmetric DROP CONSTRAINT in `Down()` |
| UPDATE (scaffold) | src/api/Migrations/AppDbContextModelSnapshot.cs | Auto-regenerated model snapshot — do not hand-edit |

---

## External References
- https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/?tabs=dotnet-core-cli (EF Core 8 migrations — add, apply, rollback)
- https://www.postgresql.org/docs/15/ddl-constraints.html#DDL-CONSTRAINTS-CHECK-CONSTRAINTS (PostgreSQL 15 CHECK constraint syntax — BETWEEN and IN operators)
- https://www.npgsql.org/efcore/mapping/json.html (Npgsql EF Core 8.x — verifying jsonb DDL output in generated migration)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] After `dotnet ef database update`, run `SELECT column_name, data_type FROM information_schema.columns WHERE table_name IN ('bookings', 'medical_code_suggestions', 'clinical_documents') AND column_name IN ('no_show_risk_score', 'risk_factors', 'review_status', 'file_hash')` — must return all four rows with expected data types (AC-002, AC-003, AC-004, AC-005)
- [ ] Execute `INSERT INTO bookings (no_show_risk_score, ...) VALUES (150, ...)` — PostgreSQL must reject with CHECK constraint violation (AC-002)
- [ ] Execute `INSERT INTO medical_code_suggestions (...) VALUES (...)` without `review_status` — `SELECT review_status` must return `Pending` (AC-004)
- [ ] Execute `INSERT INTO medical_code_suggestions (review_status, ...) VALUES ('InvalidStatus', ...)` — PostgreSQL must reject with CHECK constraint violation (Edge: invalid review_status)

---

## Implementation Checklist
- [x] Generated `AddColumn` for `risk_factors` specifies `type: "jsonb"` in the migration file — confirm before applying (AC-003; `json` vs `jsonb` is a distinct PostgreSQL column type)
- [x] Generated `AddColumn` for `review_status` carries `defaultValue: "Pending"` — confirms `HasDefaultValue("Pending")` from task_001 was applied correctly (AC-004)
- [x] `Up()` appends `ALTER TABLE bookings ADD CONSTRAINT chk_no_show_risk_score CHECK (no_show_risk_score BETWEEN 0 AND 100)` as `migrationBuilder.Sql(...)` after all `AddColumn` calls (AC-002)
- [x] `Up()` appends `ALTER TABLE medical_code_suggestions ADD CONSTRAINT chk_review_status CHECK (review_status IN ('Pending', 'Accepted', 'Rejected'))` as `migrationBuilder.Sql(...)` (AC-004; Edge: invalid review_status rejection)
- [x] `Down()` prepends `ALTER TABLE bookings DROP CONSTRAINT IF EXISTS chk_no_show_risk_score` and `ALTER TABLE medical_code_suggestions DROP CONSTRAINT IF EXISTS chk_review_status` before any `DropColumn` call — use `IF EXISTS` for rollback idempotency (AC-002, AC-004 — rollback safety)
- [ ] `dotnet ef database update` exits with code 0 and `SELECT migration_id FROM "__EFMigrationsHistory"` returns a second row ending in `_AddSchemaFieldsV2` (AC-002, AC-003, AC-004, AC-005 — migration applied)
