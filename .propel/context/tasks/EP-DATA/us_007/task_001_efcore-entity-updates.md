# Task - TASK_001

## Requirement Reference
- **User Story:** us_007
- **Story Location:** .propel/context/tasks/EP-DATA/us_007/us_007.md
- **Acceptance Criteria:**
  - AC-002: `bookings.no_show_risk_score` column accepts integer values 0–100 — driven by `int?` property on `Booking` entity and a CHECK constraint configured in task_002
  - AC-003: `bookings.risk_factors` JSONB column stores structured key/value data — driven by `JsonDocument?` property on `Booking` mapped to `jsonb` via Npgsql
  - AC-004: `medical_code_suggestions.review_status` defaults to `Pending` — driven by `HasDefaultValue("Pending")` Fluent API configuration on `MedicalCodeSuggestion.ReviewStatus`
  - AC-005: `clinical_documents.file_hash` stores a 64-character SHA-256 hex string — driven by `string?` property with `HasMaxLength(64)` on `ClinicalDocument`
- **Edge Cases:**
  - N/A — CHECK constraint for `review_status` allowed values and `no_show_risk_score` range are enforced via raw SQL in task_002

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
| Backend | ASP.NET Core Web API (C#) | .NET 8.0 | TR-002 (backend runtime); entity class additions are C# in the API project |
| Backend | EF Core | 8.x | TR-003 (ORM); Fluent API configures JSONB type mapping, default values, and max lengths) |
| Backend | Npgsql.EntityFrameworkCore.PostgreSQL | 8.x | TR-003 (Npgsql maps `JsonDocument` to `jsonb` and `string` to `character varying` in PostgreSQL 15.3+) |

---

## Task Overview

Extend the `Booking`, `MedicalCodeSuggestion`, and `ClinicalDocument` entity classes (created in us_005 task_001) with the new properties required by this story, and update `AppDbContext.OnModelCreating` to apply the correct column types, default values, and length constraints via the Fluent API. The resulting model snapshot will drive the `AddSchemaFieldsV2` migration generated in task_002.

---

## Dependent Tasks
- task_001 (us_005) — `Booking`, `MedicalCodeSuggestion`, `ClinicalDocument` entity classes and `AppDbContext` must already exist

---

## Impacted Components
- `src/api/Data/Entities/Booking.cs` — add `NoShowRiskScore int?` and `RiskFactors JsonDocument?` properties
- `src/api/Data/Entities/MedicalCodeSuggestion.cs` — add `ReviewStatus string` property
- `src/api/Data/Entities/ClinicalDocument.cs` — add `FileHash string?` property
- `src/api/Data/AppDbContext.cs` — add Fluent API configuration for the four new columns

---

## Implementation Plan
1. Add `public int? NoShowRiskScore { get; set; }` to `Booking` entity — nullable integer; the CHECK constraint range (0–100) will be applied via raw SQL in task_002 rather than via EF Core Fluent API (EF Core 8 does not support parameterised range check constraints on individual columns via Fluent API without a raw SQL migration)
2. Add `public JsonDocument? RiskFactors { get; set; }` to `Booking` entity — `System.Text.Json.JsonDocument` is the CLR type that Npgsql maps directly to the `jsonb` PostgreSQL column type
3. Add `public string ReviewStatus { get; set; } = "Pending";` to `MedicalCodeSuggestion` entity — C# default initialiser ensures the property is never null when the entity is constructed in application code
4. Add `public string? FileHash { get; set; }` to `ClinicalDocument` entity — nullable string; SHA-256 hex output is always exactly 64 chars, enforced via `HasMaxLength(64)` in Fluent API
5. In `AppDbContext.OnModelCreating`, add `modelBuilder.Entity<Booking>().Property(b => b.RiskFactors).HasColumnType("jsonb")` so Npgsql generates a `jsonb` column (not `json` or `text`) in the migration DDL (AC-003)
6. In `OnModelCreating`, add `modelBuilder.Entity<MedicalCodeSuggestion>().Property(m => m.ReviewStatus).HasDefaultValue("Pending").HasMaxLength(20)` — `HasDefaultValue` instructs EF Core to emit `DEFAULT 'Pending'` in the `AddColumn` DDL (AC-004)
7. In `OnModelCreating`, add `modelBuilder.Entity<ClinicalDocument>().Property(d => d.FileHash).HasMaxLength(64)` — generates `character varying(64)` DDL (AC-005)
8. Run `dotnet build src/api` and confirm zero errors — this validates that `JsonDocument` and other type additions compile correctly against `Npgsql.EntityFrameworkCore.PostgreSQL` 8.x before the migration is scaffolded in task_002 (AC-002, AC-003, AC-004, AC-005)

---

## Current Project State
```
src/
└── api/
    └── Data/
        ├── AppDbContext.cs                  (MODIFY — add Fluent API for 4 new columns)
        └── Entities/
            ├── Booking.cs                   (MODIFY — add NoShowRiskScore, RiskFactors)
            ├── MedicalCodeSuggestion.cs     (MODIFY — add ReviewStatus)
            └── ClinicalDocument.cs          (MODIFY — add FileHash)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | src/api/Data/Entities/Booking.cs | Add `int? NoShowRiskScore` and `JsonDocument? RiskFactors` properties |
| MODIFY | src/api/Data/Entities/MedicalCodeSuggestion.cs | Add `string ReviewStatus { get; set; } = "Pending"` property |
| MODIFY | src/api/Data/Entities/ClinicalDocument.cs | Add `string? FileHash` property |
| MODIFY | src/api/Data/AppDbContext.cs | Add `HasColumnType("jsonb")` on `Booking.RiskFactors`; `HasDefaultValue("Pending")` and `HasMaxLength(20)` on `MedicalCodeSuggestion.ReviewStatus`; `HasMaxLength(64)` on `ClinicalDocument.FileHash` |

---

## External References
- https://www.npgsql.org/efcore/mapping/json.html (Npgsql EF Core 8.x — JsonDocument mapping to `jsonb`)
- https://learn.microsoft.com/en-us/ef/core/modeling/entity-properties#column-data-types (EF Core 8 HasColumnType, HasMaxLength, HasDefaultValue)
- https://learn.microsoft.com/en-us/dotnet/api/system.text.json.jsondocument (System.Text.Json JsonDocument — used for the `risk_factors` jsonb property)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] `dotnet build src/api` produces zero errors after entity and DbContext changes (AC-002, AC-003, AC-004, AC-005 — model must compile before migration scaffold)
- [ ] Inspect `AppDbContext.Model.FindEntityType(typeof(Booking)).FindProperty("RiskFactors").GetColumnType()` — must return `jsonb` (AC-003)
- [ ] Inspect `AppDbContext.Model.FindEntityType(typeof(MedicalCodeSuggestion)).FindProperty("ReviewStatus").GetDefaultValue()` — must return `"Pending"` (AC-004)

---

## Implementation Checklist
- [x] `Booking` entity has `public int? NoShowRiskScore { get; set; }` and `public JsonDocument? RiskFactors { get; set; }` — both nullable matching AC-002/AC-003 column definitions (AC-002, AC-003)
- [x] `MedicalCodeSuggestion` entity has `public string ReviewStatus { get; set; } = "Pending";` with a C# default initialiser so the property is never null when inserting without an explicit value (AC-004)
- [x] `ClinicalDocument` entity has `public string? FileHash { get; set; }` — nullable string, length enforcement via Fluent API (AC-005)
- [x] `AppDbContext.OnModelCreating` calls `HasColumnType("jsonb")` on `Booking.RiskFactors` — without this, Npgsql generates `json` (text-backed) not `jsonb` (binary-backed) (AC-003)
- [x] `AppDbContext.OnModelCreating` calls `HasDefaultValue("Pending")` on `MedicalCodeSuggestion.ReviewStatus` so the EF Core migration DDL emits `DEFAULT 'Pending'` — a plain INSERT without `review_status` therefore returns `Pending` from the database (AC-004)
- [x] `AppDbContext.OnModelCreating` calls `HasMaxLength(64)` on `ClinicalDocument.FileHash` — generates `character varying(64)` in the migration DDL (AC-005)
- [x] `using System.Text.Json;` is added to `Booking.cs` for the `JsonDocument` type reference — no third-party JSON libraries introduced (AC-003; NFR consistent with .NET 8 built-ins)
- [x] `dotnet build src/api` exits with code 0 — confirms all type references resolve before task_002 scaffolds the migration (AC-002, AC-003, AC-004, AC-005)
