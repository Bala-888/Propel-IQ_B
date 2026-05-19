# Task - TASK_002

## Requirement Reference
- **User Story:** us_015
- **Story Location:** .propel/context/tasks/EP-002/us_015/us_015.md
- **Acceptance Criteria:**
  - AC-001: A `pg_dump` of the `patients` table contains no plaintext values for `email`, `phone`, or `dateOfBirth` — all PHI columns must contain only pgcrypto AES-256 ciphertext
  - AC-003: The .NET API connects to PostgreSQL with `Ssl Mode=Require` and `Trust Server Certificate=false`; a packet capture on the Docker bridge shows no plaintext SQL or patient data
- **Edge Cases:**
  - AES key length guard: if the `PHI_ENCRYPTION_KEY` environment variable contains fewer than 32 bytes, the application must throw a startup exception and refuse to start — it must never silently fall back to AES-128
  - Defence-in-depth: `IPhiEncryptionService` must independently validate the key length in its constructor so that even if the startup check is bypassed, the service itself refuses to encrypt with a short key

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
| Backend | ASP.NET Core Web API (C#) | .NET 8.0 | TR-002 (startup validation in `Program.cs`; `IPhiEncryptionService` constructor guard) |
| Backend | EF Core + Npgsql.EntityFrameworkCore.PostgreSQL | 8.x | TR-002 (`Ssl Mode=Require` in the Npgsql connection string; `AppDbContext` PHI value converter coverage audit) |
| Backend | BouncyCastle.Cryptography | 2.x | TR-002 (AES-256 via pgcrypto-compatible `PaddedBufferedBlockCipher`; established in us_006; key length assertion added here) |

---

## Task Overview

Harden the backend encryption posture for HIPAA at-rest and in-transit requirements. Add a startup guard in `Program.cs` that reads `PHI_ENCRYPTION_KEY` and throws before the app begins accepting requests if the key is shorter than 32 bytes. Add a matching defence-in-depth assertion in `PhiEncryptionService`. Audit all Patient PHI columns in `AppDbContext.OnModelCreating` to ensure `HasConversion<PhiEncryptionConverter>()` is applied to every column listed in AC-001. Update the Npgsql connection string to enforce `Ssl Mode=Require;Trust Server Certificate=false` loaded from an environment variable.

---

## Dependent Tasks
- task_002 (us_006) — `IPhiEncryptionService`, `PhiEncryptionService`, and EF Core value converters for PHI columns must already exist; this task adds validation and coverage audit on top of that implementation
- task_001 (us_005) — `AppDbContext` must be initialised with the `Patient` entity and PHI column mappings

---

## Impacted Components
- `src/api/Program.cs` — modified: AES key length startup validation before `builder.Build()`
- `src/api/Services/PhiEncryptionService.cs` — modified: add key length assertion in constructor
- `src/api/Data/AppDbContext.cs` — modified: verify/add `HasConversion<PhiEncryptionConverter>()` on `Email`, `Phone`, `DateOfBirth` columns of the `Patient` entity
- `docker-compose.yml` — modified: update `api` service connection string environment variable to include `Ssl Mode=Require;Trust Server Certificate=false`

---

## Implementation Plan
1. In `Program.cs`, before `builder.Build()`, read `var phiKey = Environment.GetEnvironmentVariable("PHI_ENCRYPTION_KEY") ?? string.Empty;` and execute `if (System.Text.Encoding.UTF8.GetByteCount(phiKey) < 32) { throw new InvalidOperationException("PHI_ENCRYPTION_KEY must be at least 32 bytes for AES-256. The application will not start with a shorter key."); }` — this prevents silent AES-128 downgrade (Edge: AES key length guard; OWASP A02)
2. In `PhiEncryptionService` constructor, add the same key length assertion as a defence-in-depth layer: `ArgumentException.ThrowIfNullOrEmpty(key, nameof(key)); if (Encoding.UTF8.GetByteCount(key) < 32) throw new ArgumentException("PHI_ENCRYPTION_KEY must be at least 32 bytes.", nameof(key));` — ensures the service refuses to initialise even if injected directly in a test context that bypasses `Program.cs` (Edge: defence-in-depth; OWASP A02)
3. Open `AppDbContext.OnModelCreating`; locate the `Patient` entity builder; verify that `Email`, `Phone`, and `DateOfBirth` columns each have `HasConversion<PhiEncryptionConverter>()` applied; add any missing converter calls; if additional PHI columns (e.g., `Address`, `InsuranceNumber`) exist on related entities, add converters for those too (AC-001 — PHI coverage completeness)
4. In `docker-compose.yml`, update the `api` service `POSTGRES_CONNECTION_STRING` environment variable to append `;Ssl Mode=Require;Trust Server Certificate=false` — the connection string is sourced from an environment variable, never committed in plaintext (AC-003; OWASP A02)
5. In the `db` service of `docker-compose.yml`, verify or add PostgreSQL TLS configuration: mount `ssl_cert_file` and `ssl_key_file` volumes; set `POSTGRES_INITDB_ARGS: "--auth-host=scram-sha-256"` if not already present — required for Npgsql `Ssl Mode=Require` to complete the TLS handshake (AC-003)

---

## Current Project State
```
src/
└── api/
    ├── Program.cs                                   (MODIFY — PHI_ENCRYPTION_KEY startup guard)
    ├── Services/
    │   └── PhiEncryptionService.cs                  (MODIFY — key length assertion in constructor)
    └── Data/
        └── AppDbContext.cs                          (MODIFY — verify PHI column converter coverage)
docker-compose.yml                                   (MODIFY — Ssl Mode=Require in connection string; db TLS mounts)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | src/api/Program.cs | Startup guard: PHI_ENCRYPTION_KEY < 32 bytes → InvalidOperationException before app starts |
| MODIFY | src/api/Services/PhiEncryptionService.cs | Constructor assertion: key length < 32 bytes → ArgumentException |
| MODIFY | src/api/Data/AppDbContext.cs | Verify/add HasConversion<PhiEncryptionConverter>() on all Patient PHI columns |
| MODIFY | docker-compose.yml | Add Ssl Mode=Require;Trust Server Certificate=false to POSTGRES_CONNECTION_STRING; add db TLS volumes |

---

## External References
- https://www.npgsql.org/doc/security.html (Npgsql security — Ssl Mode=Require and certificate validation options)
- https://www.bouncycastle.org/csharp/ (BouncyCastle.Cryptography 2.x — AES-256 key length requirements)
- https://learn.microsoft.com/en-us/ef/core/modeling/value-conversions?tabs=fluent-api (EF Core 8 value conversions — HasConversion for PHI column encryption)
- https://www.postgresql.org/docs/15/ssl-tcp.html (PostgreSQL 15 SSL configuration — ssl_cert_file and ssl_key_file)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] Set `PHI_ENCRYPTION_KEY` to a 16-byte value and start the API; verify the process exits immediately with `InvalidOperationException: PHI_ENCRYPTION_KEY must be at least 32 bytes` before any HTTP port is bound (Edge: AES key length guard)
- [ ] Create a patient record via `POST /patients`; run `pg_dump` against the `patients` table; search the dump output for the patient's plaintext email; verify no match is found (AC-001)
- [ ] Inspect `pg_dump` output for `phone` and `dateOfBirth` columns; verify both contain only ciphertext with no recognisable plaintext patterns (AC-001 — full PHI coverage)
- [ ] Start Docker Compose with the updated `docker-compose.yml`; verify the `api` container logs show the Npgsql connection established with TLS — no connection errors (AC-003)
- [ ] Attempt to start the API without `PHI_ENCRYPTION_KEY` set at all; verify the application throws on startup (Edge: key guard — null/empty key)

---

## Implementation Checklist
- [ ] Startup key validation in `Program.cs` uses `Encoding.UTF8.GetByteCount(phiKey)` not `phiKey.Length` — a UTF-8 string with multi-byte characters could pass a character-count check while providing fewer than 32 bytes of key material (Edge: AES key guard; OWASP A02)
- [ ] `PhiEncryptionService` constructor key assertion is identical in logic to the `Program.cs` startup guard — the two checks form a defence-in-depth pair; any change to the 32-byte threshold must be applied to both (Edge: defence-in-depth; OWASP A02)
- [ ] All three Patient PHI columns (`Email`, `Phone`, `DateOfBirth`) have `HasConversion<PhiEncryptionConverter>()` confirmed in `AppDbContext.OnModelCreating`; a code comment marks the block as "PHI COVERAGE — DO NOT REMOVE CONVERTERS" (AC-001; HIPAA §164.312(a)(2)(iv))
- [ ] `PHI_ENCRYPTION_KEY` is loaded exclusively from `Environment.GetEnvironmentVariable`; it must never appear in `appsettings.json`, `appsettings.Development.json`, or any committed config file (AC-001; OWASP A02)
- [ ] `Trust Server Certificate=false` is set alongside `Ssl Mode=Require` in the Npgsql connection string — prevents man-in-the-middle attacks where a self-signed certificate could be accepted on the Docker bridge (AC-003; OWASP A02)
