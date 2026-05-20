# Task - TASK_001

## Requirement Reference
- **User Story:** us_005
- **Story Location:** .propel/context/tasks/EP-DATA/us_005/us_005.md
- **Acceptance Criteria:**
  - AC-002: All 12 entity tables exist with correct primary keys — driven by entity class definitions and AppDbContext configuration, from which the migration DDL is generated
  - AC-003: `ChunkEmbedding.Embedding` column uses `Vector` type (`vector(1536)`) — prerequisite for the ivfflat cosine index created in task_002
  - AC-004: FK constraints enforced (e.g., `bookings.patient_id → patients.id`) — driven by Fluent API `HasForeignKey` configuration in AppDbContext
- **Edge Cases:**
  - N/A — migration idempotency and pgvector availability edge cases are addressed in task_002

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
| Backend | ASP.NET Core Web API (C#) | .NET 8.0 | TR-002 (backend runtime); entity models are C# classes in the API project |
| Backend | EF Core | 8.x | TR-003 (ORM for PostgreSQL); provides DbContext, Fluent API, and migration tooling |
| Backend | Npgsql.EntityFrameworkCore.PostgreSQL | 8.x | TR-003, DR-003 (Npgsql EF provider is required for PostgreSQL 15.3+ ACID support and pgvector type mapping) |
| Backend | EFCore.NamingConventions | 8.x | DR-003 (UseSnakeCaseNamingConventions produces lowercase snake_case table names matching AC-002 required names) |
| Database | pgvector | 0.5+ | DR-006 (vector embeddings; the `Vector` CLR type from Npgsql maps to the `vector` PostgreSQL column type used by ChunkEmbedding.Embedding) |

---

## Task Overview

Define all 12 EF Core entity classes in `src/api/Data/Entities/`, create `AppDbContext` with correct `DbSet<T>` registrations, configure all FK relationships and the `ChunkEmbedding.Embedding` vector column in `OnModelCreating` using the Fluent API, and register `AppDbContext` in the .NET DI container in `Program.cs`. The model snapshot produced here is the direct input to `dotnet ef migrations add` in task_002.

---

## Dependent Tasks
- task_001 (us_003) — `Api.csproj` and `Program.cs` middleware scaffold must exist before adding EF Core NuGet packages and DI registration

---

## Impacted Components
- `src/api/Api.csproj` — add `Npgsql.EntityFrameworkCore.PostgreSQL` 8.x and `EFCore.NamingConventions` 8.x NuGet references
- `src/api/Program.cs` — add `builder.Services.AddDbContext<AppDbContext>` registration
- `src/api/Data/AppDbContext.cs` — new DbContext subclass with 12 DbSet properties and Fluent API configuration
- `src/api/Data/Entities/User.cs` — new entity class
- `src/api/Data/Entities/Patient.cs` — new entity class
- `src/api/Data/Entities/IntakeRecord.cs` — new entity class
- `src/api/Data/Entities/AppointmentSlot.cs` — new entity class
- `src/api/Data/Entities/Booking.cs` — new entity class (FK to Patient)
- `src/api/Data/Entities/ClinicalDocument.cs` — new entity class
- `src/api/Data/Entities/ExtractedRecord.cs` — new entity class
- `src/api/Data/Entities/ChunkEmbedding.cs` — new entity class (Vector embedding column)
- `src/api/Data/Entities/MedicalCodeSuggestion.cs` — new entity class
- `src/api/Data/Entities/AuditLog.cs` — new entity class
- `src/api/Data/Entities/ReminderSchedule.cs` — new entity class
- `src/api/Data/Entities/InsuranceRecord.cs` — new entity class

---

## Implementation Plan
1. Add `<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="8.*" />` and `<PackageReference Include="EFCore.NamingConventions" Version="8.*" />` to `Api.csproj`
2. Create `src/api/Data/Entities/` directory and add entity classes for `User`, `Patient`, `IntakeRecord`, `AppointmentSlot`, `Booking` — each with a `public int Id { get; set; }` primary key and navigation properties where applicable (e.g., `Booking.PatientId`, `Booking.Patient`)
3. Create entity classes for `ClinicalDocument`, `ExtractedRecord`, `ChunkEmbedding`, `MedicalCodeSuggestion`, `AuditLog`, `ReminderSchedule`, `InsuranceRecord` — using the same PK convention; add `public NpgsqlVector Embedding { get; set; }` on `ChunkEmbedding`
4. Create `src/api/Data/AppDbContext.cs` inheriting from `DbContext`, with one `DbSet<T>` property per entity (12 total), and a constructor accepting `DbContextOptions<AppDbContext>`
5. Override `OnModelCreating` to call `modelBuilder.UseSnakeCaseNamingConventions()` (from `EFCore.NamingConventions`) to produce snake_case table/column names matching AC-002 requirements
6. In `OnModelCreating`, configure FK relationships using Fluent API — at minimum `modelBuilder.Entity<Booking>().HasOne(b => b.Patient).WithMany().HasForeignKey(b => b.PatientId).OnDelete(DeleteBehavior.Restrict)`; repeat for other FK relationships across the 12 entities
7. In `Program.cs`, add `builder.Services.AddDbContext<AppDbContext>(opt => opt.UseNpgsql(Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")!, o => o.UseVector()))` — read the connection string from the `POSTGRES_CONNECTION_STRING` environment variable to avoid storing credentials in source (OWASP A02)

---

## Current Project State
```
src/
└── api/
    ├── Api.csproj               (MODIFY — add Npgsql EF + NamingConventions NuGets)
    ├── Program.cs               (MODIFY — add AddDbContext registration)
    └── Data/
        ├── AppDbContext.cs      (CREATE)
        └── Entities/
            ├── User.cs          (CREATE)
            ├── Patient.cs       (CREATE)
            ├── IntakeRecord.cs  (CREATE)
            ├── AppointmentSlot.cs (CREATE)
            ├── Booking.cs       (CREATE)
            ├── ClinicalDocument.cs (CREATE)
            ├── ExtractedRecord.cs  (CREATE)
            ├── ChunkEmbedding.cs   (CREATE)
            ├── MedicalCodeSuggestion.cs (CREATE)
            ├── AuditLog.cs      (CREATE)
            ├── ReminderSchedule.cs (CREATE)
            └── InsuranceRecord.cs  (CREATE)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | src/api/Api.csproj | Add `Npgsql.EntityFrameworkCore.PostgreSQL` 8.x and `EFCore.NamingConventions` 8.x NuGet references |
| MODIFY | src/api/Program.cs | Register `AddDbContext<AppDbContext>` with `UseNpgsql` and `UseVector()` reading connection string from env var |
| CREATE | src/api/Data/AppDbContext.cs | `AppDbContext : DbContext` with 12 `DbSet<T>` properties and Fluent API FK + naming configuration |
| CREATE | src/api/Data/Entities/User.cs | `User` entity with `int Id` PK |
| CREATE | src/api/Data/Entities/Patient.cs | `Patient` entity with `int Id` PK |
| CREATE | src/api/Data/Entities/IntakeRecord.cs | `IntakeRecord` entity with `int Id` PK |
| CREATE | src/api/Data/Entities/AppointmentSlot.cs | `AppointmentSlot` entity with `int Id` PK |
| CREATE | src/api/Data/Entities/Booking.cs | `Booking` entity with `int Id` PK and `PatientId` FK property |
| CREATE | src/api/Data/Entities/ClinicalDocument.cs | `ClinicalDocument` entity with `int Id` PK |
| CREATE | src/api/Data/Entities/ExtractedRecord.cs | `ExtractedRecord` entity with `int Id` PK |
| CREATE | src/api/Data/Entities/ChunkEmbedding.cs | `ChunkEmbedding` entity with `int Id` PK and `NpgsqlVector Embedding` property |
| CREATE | src/api/Data/Entities/MedicalCodeSuggestion.cs | `MedicalCodeSuggestion` entity with `int Id` PK |
| CREATE | src/api/Data/Entities/AuditLog.cs | `AuditLog` entity with `int Id` PK |
| CREATE | src/api/Data/Entities/ReminderSchedule.cs | `ReminderSchedule` entity with `int Id` PK |
| CREATE | src/api/Data/Entities/InsuranceRecord.cs | `InsuranceRecord` entity with `int Id` PK |

---

## External References
- https://www.npgsql.org/efcore/index.html (Npgsql EF Core 8.x — UseNpgsql, UseVector)
- https://github.com/efcore/EFCore.NamingConventions (EFCore.NamingConventions 8.x — UseSnakeCaseNamingConventions)
- https://github.com/pgvector/pgvector-dotnet (pgvector-dotnet — NpgsqlVector type, UseVector() registration)
- https://learn.microsoft.com/en-us/ef/core/modeling/relationships (EF Core 8 Fluent API FK configuration)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] `dotnet build src/api` completes with 0 errors after adding NuGet packages and all 12 entity classes (AC-002)
- [ ] `AppDbContext.Model.GetEntityTypes()` returns exactly 12 entity types when interrogated via a unit test (AC-002)
- [ ] Fluent API FK configuration for `Booking → Patient` is verified by inspecting `AppDbContext.Model.FindEntityType(typeof(Booking)).GetForeignKeys()` — must include a FK to `Patient` (AC-004)

---

## Implementation Checklist
- [x] `Api.csproj` references `Npgsql.EntityFrameworkCore.PostgreSQL` 8.x and `EFCore.NamingConventions` 8.x (AC-002 — EF Core provider and snake_case naming required for all 12 tables)
- [x] All 12 entity classes exist in `src/api/Data/Entities/` with a `public int Id { get; set; }` primary key and navigation properties for FK relationships (AC-002)
- [x] `ChunkEmbedding` entity includes `public Vector Embedding { get; set; }` (Pgvector.Vector) as the vector property — prerequisite for the ivfflat index SQL in task_002 (AC-003)
- [x] `AppDbContext` uses `UseSnakeCaseNamingConvention()` (via EFCore.NamingConventions `DbContextOptionsBuilder` extension) so generated table names match the 12 names required by AC-002 exactly (AC-002)
- [x] All FK relationships are configured in `OnModelCreating` with explicit `.HasForeignKey()` calls — at minimum `Booking → Patient`; all others mapped per entity domain relationships (AC-004)
- [x] `ChunkEmbedding.Embedding` is configured with `HasColumnType("vector(1536)")` in Fluent API to set the vector dimension used by the ivfflat index in task_002 (AC-003)
- [x] `Program.cs` registers `AddDbContext<AppDbContext>` reading the connection string from `POSTGRES_CONNECTION_STRING` environment variable — not hardcoded in source (AC-002, AC-005; OWASP A02 — no credentials in code)
