# Task - TASK_002

## Requirement Reference
- **User Story:** us_006
- **Story Location:** .propel/context/tasks/EP-DATA/us_006/us_006.md
- **Acceptance Criteria:**
  - AC-001: PHI columns (`email`, `phone`) in `patients` table store pgcrypto-format ciphertext (`bytea`) — no plaintext present when queried directly as `app_user`
  - AC-002: `PatientRepository.GetByIdAsync(patientId)` returns a `PatientDto` with original plaintext values for `email`, `phone`, `dateOfBirth`, `insuranceProvider`, and `insuranceId`
  - AC-004: `PHI_ENCRYPTION_KEY` read from environment variable into `ReadOnlyMemory<byte>` — no plaintext key in logs, config files, or source code
- **Edge Cases:**
  - Key rotation: If `PHI_ENCRYPTION_KEY` changes between deployments and decryption fails, the API must return HTTP 503 with body `{ "error": "PHI decryption failed — key rotation required" }` — not garbled data
  - Null PHI field: If a nullable PHI column (e.g., `insuranceId`) is NULL, `Encrypt(null)` must return `null` and `pgp_sym_encrypt` must not be called — column remains NULL in the database

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
| Backend | ASP.NET Core Web API (C#) | .NET 8.0 | TR-002 (backend runtime); IPhiEncryptionService and PatientRepository are C# classes in the API project |
| Backend | EF Core | 8.x | TR-003 (value converters transparently apply encryption/decryption on PHI columns during EF Core reads/writes) |
| Backend | Npgsql.EntityFrameworkCore.PostgreSQL | 8.x | TR-003 (maps `byte[]` EF Core properties to `bytea` PostgreSQL columns used for PHI ciphertext storage) |
| Backend | BouncyCastle.Cryptography | 2.x (latest stable) | DR-001 (AES-256 symmetric encryption producing pgcrypto-compatible PGP symmetric ciphertext; free OSS — NFR-005) |
| Database | PostgreSQL pgcrypto | built-in with 15.3+ | DR-001 (pgcrypto `pgp_sym_encrypt` bytea format is the wire format stored in PHI columns; BouncyCastle output must be compatible) |

---

## Task Overview

Implement `IPhiEncryptionService` and `PhiEncryptionService` using BouncyCastle's PGP symmetric AES-256 encryption to produce bytea-compatible ciphertext matching PostgreSQL's pgcrypto `pgp_sym_encrypt` format. Configure EF Core value converters on the five PHI columns of the `Patient` entity so encryption/decryption is transparent to all repositories. Create `PatientRepository` with `GetByIdAsync`. Add a global exception middleware that maps `PhiDecryptionException` to HTTP 503. Guard all null inputs in the service to prevent encrypting null PHI fields.

---

## Dependent Tasks
- task_001 (us_005) — `Patient` entity class and `AppDbContext` must exist (entity model is the target for value converter configuration)
- task_001 (us_003) — `Program.cs` middleware pipeline must exist for registering the exception handler and DI services

---

## Impacted Components
- `src/api/Api.csproj` — add `BouncyCastle.Cryptography` 2.x NuGet reference
- `src/api/Services/IPhiEncryptionService.cs` — new interface
- `src/api/Services/PhiEncryptionService.cs` — new implementation
- `src/api/Exceptions/PhiDecryptionException.cs` — new custom exception
- `src/api/Data/AppDbContext.cs` — modified to configure value converters on Patient PHI columns
- `src/api/Repositories/IPatientRepository.cs` — new interface
- `src/api/Repositories/PatientRepository.cs` — new repository class
- `src/api/Program.cs` — modified to register IPhiEncryptionService, IPatientRepository, and exception middleware

---

## Implementation Plan
1. Add `<PackageReference Include="BouncyCastle.Cryptography" Version="2.*" />` to `Api.csproj`
2. Create `src/api/Exceptions/PhiDecryptionException.cs` as a custom exception class extending `Exception` — used to signal key rotation failures without exposing cipher details
3. Create `src/api/Services/IPhiEncryptionService.cs` with `byte[]? Encrypt(string? plaintext)` and `string? Decrypt(byte[]? ciphertext)` — nullable signatures enforce null-passthrough at the interface contract level
4. Create `src/api/Services/PhiEncryptionService.cs`: constructor reads `PHI_ENCRYPTION_KEY` from `Environment.GetEnvironmentVariable("PHI_ENCRYPTION_KEY")` into `ReadOnlyMemory<byte>` and throws `InvalidOperationException("PHI_ENCRYPTION_KEY environment variable is not set")` if absent; `Encrypt` returns null if input is null, otherwise applies BouncyCastle PGP symmetric AES-256; `Decrypt` returns null if input is null, otherwise decrypts — on cipher failure throws `PhiDecryptionException("PHI decryption failed — key rotation required")`
5. In `AppDbContext.OnModelCreating`, configure EF Core value converters on the `Patient` entity for five columns — `Email`, `Phone`, `DateOfBirth`, `InsuranceProvider`, `InsuranceId` — using a `ValueConverter<string?, byte[]?>` that calls `IPhiEncryptionService.Encrypt` on write and `Decrypt` on read; pass `IPhiEncryptionService` into `AppDbContext` via constructor injection
6. Create `src/api/Repositories/IPatientRepository.cs` with `Task<PatientDto?> GetByIdAsync(int patientId)` and `src/api/Repositories/PatientRepository.cs` implementing it via `AppDbContext` — EF Core value converters handle decryption transparently; map the decrypted entity to `PatientDto` before returning
7. In `Program.cs`, add exception-handling middleware (or `UseExceptionHandler`) that catches `PhiDecryptionException` and writes a JSON response with status code 503 and body `{ "error": "PHI decryption failed — key rotation required" }` — placed before any controller middleware (Edge: key rotation)
8. Register `builder.Services.AddScoped<IPhiEncryptionService, PhiEncryptionService>()` and `builder.Services.AddScoped<IPatientRepository, PatientRepository>()` in `Program.cs`; confirm `PHI_ENCRYPTION_KEY` is set in the `docker-compose.yml` `api` service environment block as a reference to a Docker secret or `.env` file variable — never a literal value (AC-004; OWASP A02)

---

## Current Project State
```
src/
└── api/
    ├── Api.csproj                          (MODIFY — add BouncyCastle.Cryptography)
    ├── Program.cs                          (MODIFY — register services + exception middleware)
    ├── Data/
    │   └── AppDbContext.cs                 (MODIFY — add value converters on Patient PHI columns)
    ├── Exceptions/
    │   └── PhiDecryptionException.cs       (CREATE)
    ├── Services/
    │   ├── IPhiEncryptionService.cs        (CREATE)
    │   └── PhiEncryptionService.cs         (CREATE)
    └── Repositories/
        ├── IPatientRepository.cs           (CREATE)
        └── PatientRepository.cs            (CREATE)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | src/api/Api.csproj | Add `BouncyCastle.Cryptography` 2.x NuGet reference |
| CREATE | src/api/Exceptions/PhiDecryptionException.cs | Custom exception signalling PHI decryption failure due to key mismatch |
| CREATE | src/api/Services/IPhiEncryptionService.cs | Interface with nullable `Encrypt` / `Decrypt` contract |
| CREATE | src/api/Services/PhiEncryptionService.cs | BouncyCastle AES-256 PGP symmetric implementation; null-passthrough; key from env var; throws `PhiDecryptionException` on cipher failure |
| MODIFY | src/api/Data/AppDbContext.cs | Add EF Core `ValueConverter<string?, byte[]?>` on `Patient.Email`, `Patient.Phone`, `Patient.DateOfBirth`, `Patient.InsuranceProvider`, `Patient.InsuranceId`; inject `IPhiEncryptionService` via constructor |
| CREATE | src/api/Repositories/IPatientRepository.cs | Repository interface with `GetByIdAsync(int patientId)` |
| CREATE | src/api/Repositories/PatientRepository.cs | Repository implementation using `AppDbContext`; maps decrypted entity to `PatientDto` |
| MODIFY | src/api/Program.cs | Register `IPhiEncryptionService`, `IPatientRepository`; add exception middleware for `PhiDecryptionException` → HTTP 503 |

---

## External References
- https://www.bouncycastle.org/csharp/ (BouncyCastle.Cryptography 2.x — PgpEncryptedDataGenerator for AES-256 symmetric encryption)
- https://www.postgresql.org/docs/15/pgcrypto.html (PostgreSQL 15 pgcrypto — pgp_sym_encrypt / pgp_sym_decrypt bytea wire format)
- https://learn.microsoft.com/en-us/ef/core/modeling/value-conversions (EF Core 8 value converters — ValueConverter<TModel, TProvider> with IPhiEncryptionService)
- https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling?view=aspnetcore-8.0 (ASP.NET Core 8 exception handling middleware — UseExceptionHandler)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] Create a Patient via the API with `email = "test@example.com"`; query `SELECT email FROM patients WHERE id = <id>` directly as `app_user` — column must return bytea ciphertext, not the literal string (AC-001)
- [ ] Call `PatientRepository.GetByIdAsync(id)` in an integration test — returned `PatientDto.Email` must equal `"test@example.com"` (AC-002)
- [ ] In a unit test, call `PhiEncryptionService.Encrypt(null)` and `PhiEncryptionService.Decrypt(null)` — both must return `null` without throwing (Edge: null PHI field)
- [ ] In a unit test, initialise `PhiEncryptionService` with key A, encrypt a string, then initialise a second instance with key B and attempt to decrypt — must throw `PhiDecryptionException` with the required message (Edge: key rotation)

---

## Implementation Checklist
- [ ] `Api.csproj` references `BouncyCastle.Cryptography` 2.x for PGP symmetric AES-256 encryption producing pgcrypto-compatible bytea format (AC-001 — ciphertext format must match pgcrypto `pgp_sym_encrypt` output)
- [ ] `PhiEncryptionService` constructor reads `PHI_ENCRYPTION_KEY` exclusively from `Environment.GetEnvironmentVariable("PHI_ENCRYPTION_KEY")` and stores it as `ReadOnlyMemory<byte>` — key is never written to `ILogger`, `Console`, `appsettings.json`, or any file (AC-004; OWASP A02)
- [ ] `IPhiEncryptionService.Encrypt(string? plaintext)` returns `null` when `plaintext` is `null` — `pgp_sym_encrypt` is never called with a null argument (Edge: null PHI field)
- [ ] `PhiEncryptionService.Decrypt` catches `InvalidCipherTextException` (or equivalent BouncyCastle exception) and re-throws as `PhiDecryptionException("PHI decryption failed — key rotation required")` — raw cipher errors are never exposed to API callers (Edge: key rotation)
- [ ] EF Core value converters are configured on all five PHI columns of the `Patient` entity: `Email`, `Phone`, `DateOfBirth`, `InsuranceProvider`, `InsuranceId` — each uses `ValueConverter<string?, byte[]?>` wrapping `IPhiEncryptionService` (AC-001, AC-002)
- [ ] `PatientRepository.GetByIdAsync` returns a `PatientDto` with plaintext PHI fields populated from the decrypted entity — value converters handle decryption so the repository does not call `IPhiEncryptionService` directly (AC-002 — separation of concerns)
- [ ] Exception middleware (placed before controller routing) catches `PhiDecryptionException` and returns `HTTP 503` with JSON body `{ "error": "PHI decryption failed — key rotation required" }` — no stack trace or cipher details exposed in the response (Edge: key rotation; OWASP A09 — insufficient logging guard)
- [ ] `docker-compose.yml` `api` service references `PHI_ENCRYPTION_KEY` via an environment variable pointing to a `.env` file entry — the literal key value is never committed to source (AC-004; OWASP A02 — no hardcoded secrets)
