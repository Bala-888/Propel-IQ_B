# Task - TASK_001

## Requirement Reference
- **User Story:** us_009
- **Story Location:** .propel/context/tasks/EP-001/us_009/us_009.md
- **Acceptance Criteria:**
  - N/A — this task creates the schema prerequisite for the JWT refresh token feature; no AC maps directly to the schema shape, but the schema design is driven by the replay attack edge case requirement
- **Edge Cases:**
  - Refresh token replay attack: the `refresh_tokens` table must include a `family_id` column (GUID) so the `POST /auth/refresh` endpoint in task_002 can invalidate all tokens in a replay-detected chain with a single `UPDATE ... WHERE family_id = <id>` — without `family_id`, family-level revocation is impossible

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
| Database | PostgreSQL | 15.3+ | TR-003 (target database for refresh_tokens table; `refresh_tokens` is not in the 12 domain tables created by us_005 — a new migration is required) |
| Backend | EF Core (dotnet ef CLI) | 8.x | TR-003 (AddRefreshTokens migration scaffolded from the new entity class; applied via `dotnet ef database update`) |
| Backend | Npgsql.EntityFrameworkCore.PostgreSQL | 8.x | TR-003 (maps `Guid` to `uuid`, `bool` to `boolean`, `DateTime` to `timestamp with time zone` in PostgreSQL) |

---

## Task Overview

Add a `RefreshToken` entity class to the EF Core model and scaffold an `AddRefreshTokens` migration to create the `refresh_tokens` table. The entity must include a `FamilyId` GUID column that groups tokens issued in the same refresh chain. This column enables the family-level revocation logic in task_002: when a replay is detected, a single `UPDATE` sets `is_revoked = true` for all rows sharing the same `family_id`, invalidating the entire token lineage.

---

## Dependent Tasks
- task_001 (us_005) — `AppDbContext` and `User` entity must exist; `InitialSchema` migration must be applied so the `users` table exists for the FK reference
- task_002 (us_007) — `AddSchemaFieldsV2` migration must be applied so the migration history is contiguous before appending `AddRefreshTokens`

---

## Impacted Components
- `src/api/Data/Entities/RefreshToken.cs` — new entity class
- `src/api/Data/AppDbContext.cs` — add `DbSet<RefreshToken>` and Fluent API configuration
- `src/api/Migrations/YYYYMMDDHHMMSS_AddRefreshTokens.cs` — new EF Core migration (auto-generated)
- `src/api/Migrations/AppDbContextModelSnapshot.cs` — updated model snapshot (auto-generated)

---

## Implementation Plan
1. Create `src/api/Data/Entities/RefreshToken.cs` with properties: `Guid Id`, `Guid UserId`, `string Token`, `DateTime ExpiresAt`, `bool IsRevoked`, `Guid FamilyId`, `DateTime CreatedAt`; `FamilyId` groups all tokens issued in the same rotation chain
2. In `AppDbContext`, add `public DbSet<RefreshToken> RefreshTokens { get; set; }` and configure in `OnModelCreating`: `HasOne<User>().WithMany().HasForeignKey(rt => rt.UserId).OnDelete(DeleteBehavior.Cascade)`, unique index on `Token` column
3. Run `dotnet ef migrations add AddRefreshTokens --project src/api` to scaffold the migration; verify the generated file creates the `refresh_tokens` table with all columns and the unique index on `token`
4. Apply with `dotnet ef database update`; confirm `SELECT table_name FROM information_schema.tables WHERE table_name = 'refresh_tokens'` returns one row

---

## Current Project State
```
src/
└── api/
    ├── Data/
    │   ├── AppDbContext.cs               (MODIFY — add DbSet<RefreshToken> + Fluent API)
    │   └── Entities/
    │       └── RefreshToken.cs           (CREATE)
    └── Migrations/
        ├── AppDbContextModelSnapshot.cs  (UPDATE — auto-generated)
        └── YYYYMMDDHHMMSS_AddRefreshTokens.cs (SCAFFOLD)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | src/api/Data/Entities/RefreshToken.cs | `RefreshToken` entity with Id, UserId, Token, ExpiresAt, IsRevoked, FamilyId, CreatedAt |
| MODIFY | src/api/Data/AppDbContext.cs | Add `DbSet<RefreshToken>`, FK to User with cascade delete, unique index on Token |
| CREATE (scaffold) | src/api/Migrations/YYYYMMDDHHMMSS_AddRefreshTokens.cs | EF Core migration creating `refresh_tokens` table |
| UPDATE (scaffold) | src/api/Migrations/AppDbContextModelSnapshot.cs | Auto-regenerated model snapshot |

---

## External References
- https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/?tabs=dotnet-core-cli (EF Core 8 migrations — add, apply)
- https://www.npgsql.org/efcore/mapping/basic.html (Npgsql — Guid → uuid, DateTime → timestamptz mapping)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] After `dotnet ef database update`, run `SELECT column_name, data_type FROM information_schema.columns WHERE table_name = 'refresh_tokens'` — must include `id` (uuid), `token` (text), `family_id` (uuid), `is_revoked` (boolean), `expires_at` (timestamptz) (Edge: family-level revocation schema)
- [ ] Verify a unique index exists: `SELECT indexname FROM pg_indexes WHERE tablename = 'refresh_tokens' AND indexname LIKE '%token%'` — must return one row (ensures duplicate tokens are rejected at the DB level)

---

## Implementation Checklist
- [x] `RefreshToken` entity includes `Guid FamilyId` column — required by task_002's family-level revocation logic; without this column the replay attack edge case cannot be implemented (Edge: refresh token replay attack)
- [x] Unique index on `Token` is configured in `OnModelCreating` with `.HasIndex(rt => rt.Token).IsUnique()` — prevents two rows with the same token string from being stored (security invariant)
- [x] FK to `User` is configured with `OnDelete(DeleteBehavior.Cascade)` — refresh tokens for a deleted user are automatically removed (data integrity)
- [x] Generated migration DDL includes `family_id uuid NOT NULL` — confirmed before applying that the column is present and not nullable (Edge: family-level revocation requires non-null FamilyId for reliable WHERE clause)
