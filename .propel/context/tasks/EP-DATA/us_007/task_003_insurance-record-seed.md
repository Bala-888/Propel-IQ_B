# Task - TASK_003

## Requirement Reference
- **User Story:** us_007
- **Story Location:** .propel/context/tasks/EP-DATA/us_007/us_007.md
- **Acceptance Criteria:**
  - AC-001: `SELECT COUNT(*) FROM insurance_records` returns ≥ 10 after the seed script runs; at least one record has `provider_name = 'BlueCross BlueShield'`
- **Edge Cases:**
  - Duplicate seed on re-run: Running `dotnet run --project src/seed` a second time must not fail with a unique constraint violation — `INSERT ... ON CONFLICT DO NOTHING` must be used for all seed rows

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
| Backend | ASP.NET Core (.NET 8.0 console) | .NET 8.0 | TR-002 (seed project is a .NET 8.0 console app invoked via `dotnet run --project src/seed`) |
| Backend | Npgsql | 8.x | TR-003 (direct Npgsql `NpgsqlCommand` is used for `INSERT ... ON CONFLICT DO NOTHING` raw SQL, which EF Core `AddAsync` does not support natively) |
| Database | PostgreSQL | 15.3+ | TR-003, DR-004 (seed target; `ON CONFLICT DO NOTHING` is a PostgreSQL upsert feature) |

---

## Task Overview

Create a `src/seed/` .NET 8 console project that connects to the `app` PostgreSQL database via the `POSTGRES_CONNECTION_STRING` env var and inserts at least 10 `InsuranceRecord` rows using raw `NpgsqlCommand` with `INSERT ... ON CONFLICT (id) DO NOTHING`. The seed data must include diverse provider names with `BlueCross BlueShield` present. The script must be fully idempotent — a second run silently skips already-inserted rows and exits with code 0.

---

## Dependent Tasks
- task_002 (us_005) — `InitialSchema` migration must have created the `insurance_records` table before the seed runs
- task_001 (us_001) — PostgreSQL Docker service must be running and accessible

---

## Impacted Components
- `src/seed/Seed.csproj` — new .NET 8.0 console project
- `src/seed/Program.cs` — new entry point; reads env var, opens connection, calls seeder
- `src/seed/InsuranceRecordSeeder.cs` — new seeder class with static seed data and `NpgsqlCommand` execution

---

## Implementation Plan
1. Create `src/seed/Seed.csproj` as a `<OutputType>Exe</OutputType>` .NET 8.0 project with a `<PackageReference>` to `Npgsql` 8.x — no EF Core dependency needed for a raw-SQL seed project
2. Create `src/seed/InsuranceRecordSeeder.cs` with a static `IReadOnlyList<(string PolicyNumber, string ProviderName, string PlanType)>` array of ≥10 seed rows — providers must include `BlueCross BlueShield`, `Aetna`, `UnitedHealthcare`, `Cigna`, `Humana`, `Anthem`, `CVS Health`, `Molina`, `WellCare`, and `TRICARE`
3. In `InsuranceRecordSeeder.SeedAsync(NpgsqlConnection conn)`, build the INSERT SQL: `INSERT INTO insurance_records (policy_number, provider_name, plan_type) VALUES (@policy_number, @provider_name, @plan_type) ON CONFLICT (policy_number) DO NOTHING` — use `ON CONFLICT (policy_number)` assuming `policy_number` is the natural unique key; iterate over seed rows using `NpgsqlBatch` for efficiency
4. In `src/seed/Program.cs`, read `POSTGRES_CONNECTION_STRING` from `Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")` — throw `InvalidOperationException` at startup if absent; open an `NpgsqlConnection`, call `InsuranceRecordSeeder.SeedAsync`, log the row count before and after (`SELECT COUNT(*) FROM insurance_records`) to confirm ≥10 rows exist
5. Add a `docker-compose.yml` `seed` service entry with `image: mcr.microsoft.com/dotnet/sdk:8.0`, `volumes: [./src/seed:/app]`, `command: dotnet run --project /app`, `depends_on: [db]`, and `profiles: [seed]` — profile-gated so it does not run on every `docker compose up`

---

## Current Project State
```
src/
└── seed/
    ├── Seed.csproj              (CREATE)
    ├── Program.cs               (CREATE)
    └── InsuranceRecordSeeder.cs (CREATE)
docker-compose.yml               (MODIFY — add profile-gated seed service)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | src/seed/Seed.csproj | .NET 8.0 console project with `Npgsql` 8.x reference |
| CREATE | src/seed/Program.cs | Entry point: reads env var, opens NpgsqlConnection, calls seeder, logs before/after counts |
| CREATE | src/seed/InsuranceRecordSeeder.cs | Static seed data array (≥10 rows including `BlueCross BlueShield`); inserts with `ON CONFLICT (policy_number) DO NOTHING` |
| MODIFY | docker-compose.yml | Add profile-gated `seed` service using `dotnet/sdk:8.0` image and `profiles: [seed]` |

---

## External References
- https://www.npgsql.org/doc/basic-usage.html (Npgsql 8.x — NpgsqlConnection, NpgsqlCommand, NpgsqlBatch)
- https://www.postgresql.org/docs/15/sql-insert.html#SQL-ON-CONFLICT (PostgreSQL 15 INSERT ... ON CONFLICT DO NOTHING)
- https://docs.docker.com/compose/profiles/ (Docker Compose profiles — profile-gating optional services)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] Run `dotnet run --project src/seed` (with `POSTGRES_CONNECTION_STRING` set); query `SELECT COUNT(*) FROM insurance_records` — must return ≥ 10 (AC-001)
- [ ] Query `SELECT provider_name FROM insurance_records WHERE provider_name = 'BlueCross BlueShield'` — must return at least one row (AC-001)
- [ ] Run `dotnet run --project src/seed` a second time — must exit with code 0 and count must not increase (Edge: duplicate seed re-run)

---

## Implementation Checklist
- [ ] Seed data array in `InsuranceRecordSeeder.cs` contains ≥ 10 entries and includes a row with `ProviderName = "BlueCross BlueShield"` (AC-001)
- [ ] INSERT statement uses `ON CONFLICT (policy_number) DO NOTHING` so re-running the seed on a pre-seeded database exits cleanly without duplicate-key errors (Edge: duplicate seed re-run)
- [ ] `Program.cs` reads `POSTGRES_CONNECTION_STRING` exclusively from `Environment.GetEnvironmentVariable(...)` and throws at startup if the value is absent — no connection string in source code (OWASP A02)
- [ ] `Program.cs` logs `SELECT COUNT(*) FROM insurance_records` before and after seeding so the operator can confirm ≥10 rows from console output (AC-001 — observable verification)
- [ ] `docker-compose.yml` seed service is gated behind `profiles: [seed]` so it does not execute during normal `docker compose up` (AC-001 — seed is opt-in, not automatic)
