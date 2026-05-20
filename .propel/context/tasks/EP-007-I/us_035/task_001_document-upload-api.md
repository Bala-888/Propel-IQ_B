# Task - TASK_001

## Requirement Reference
- **User Story:** us_035
- **Story Location:** .propel/context/tasks/EP-007-I/us_035/us_035.md
- **Acceptance Criteria:**
  - AC-001: Server-side MIME magic byte validation rejects non-PDF and non-DOCX uploads with HTTP 400 `{"error": "Unsupported file type. Only PDF and DOCX are accepted."}` — validated by inspecting the first bytes of the file stream, not the filename extension
  - AC-002: Files larger than 25 MB are rejected with HTTP 413 `{"error": "File exceeds the 25 MB size limit."}` before any file bytes are persisted
  - AC-003: Accepted file bytes are encrypted with AES-256-GCM using a per-document key before being written to blob storage; plaintext bytes are never written to disk
  - AC-004: A `document_records` row is inserted only after the encrypted file is successfully persisted; HTTP 201 returned with `{"documentId": "<uuid>", "status": "Uploaded"}`
- **Edge Cases:**
  - Concurrent uploads by the same patient: each call to `POST /documents/upload` generates an independent `Guid.NewGuid()` as the `documentId` and an independent `document_records` row — no shared mutable state between concurrent requests
  - Upload interrupted mid-stream: cipher text is first written to a temp file; `File.Move` atomically promotes it; if any step fails before `SaveChangesAsync`, the temp and final file are deleted in a `finally` block — no orphaned storage objects, no `document_records` row created

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
| Backend | ASP.NET Core Web API (C#) | .NET 8.0 | TR-005 — `POST /api/documents/upload` with `IFormFile`; MIME validation; size check; 201 response with `documentId` (AC-001–004) |
| ORM | EF Core + Npgsql.EntityFrameworkCore.PostgreSQL | 8.x | TR-006 — `document_records` table insert with all metadata fields; `SaveChangesAsync` only after successful blob write (AC-004; Edge: interrupted upload) |
| Database | PostgreSQL 15.3+ | 15.3+ | TR-007 — `document_records` table; `patient_id`, `blob_path`, `status` columns; unique `document_id` UUID primary key (AC-004) |
| Encryption | BouncyCastle.Cryptography | 2.x | TR-008 — AES-256-GCM per-document encryption via `IDocumentEncryptionService`; per-document random key + nonce; platform master key wrapping (AC-003; OWASP A02) |

---

## Task Overview

Implement `POST /api/documents/upload` restricted to the Patient role. The pipeline enforces size (25 MB) and MIME magic byte checks before any I/O. Accepted file bytes are passed to `IDocumentEncryptionService.Encrypt` which uses AES-256-GCM with a per-document random key. The cipher text is atomically promoted from a temp file path to the final blob path via `File.Move`. A `document_records` row is inserted only after the move succeeds. A `finally` block cleans up temp/final files if the pipeline fails before `SaveChangesAsync`. Each request generates an independent `Guid.NewGuid()` as the document ID, making concurrent uploads safe by construction.

---

## Dependent Tasks
- task_001 (us_006) — `IPhiEncryptionService` (BouncyCastle AES-256) patterns and key management conventions; `IDocumentEncryptionService` follows the same approach
- task_001 (us_009) — `Roles.Patient` constant and JWT auth middleware must be present

---

## Impacted Components
- `src/api/Features/Documents/DocumentUploadController.cs` — new: `POST /api/documents/upload`
- `src/api/Features/Documents/IDocumentUploadService.cs` — new: service interface
- `src/api/Features/Documents/DocumentUploadService.cs` — new: MIME check, size check, encryption, atomic write, DB insert
- `src/api/Features/Documents/IDocumentEncryptionService.cs` — new: encryption abstraction
- `src/api/Features/Documents/DocumentEncryptionService.cs` — new: AES-256-GCM implementation using BouncyCastle
- `src/api/Features/Documents/DocumentRecord.cs` — new: EF Core entity for `document_records` table
- `src/api/Program.cs` — modified: register `IDocumentUploadService`, `IDocumentEncryptionService` as scoped; add `document_records` `DbSet` to `AppDbContext`

---

## Implementation Plan
1. `POST /api/documents/upload` endpoint: `[Authorize(Roles = Roles.Patient)]`; accepts `IFormFile file`; extracts `patientId` from JWT — never from request body; delegates to `IDocumentUploadService.UploadAsync(file, patientId)` (AC-004; OWASP A01, A07)
2. Size check in `UploadAsync`: `if (file.Length > 25L * 1024 * 1024)` return 413 `{"error": "File exceeds the 25 MB size limit."}` — evaluated before opening the file stream; if `file.Length` is 0 (stream length unknown), check after buffering the first 25 MB + 1 byte (AC-002; OWASP A04)
3. MIME magic byte validation: open the file stream; read the first 8 bytes; reset stream position to 0; check for PDF signature (`[0x25, 0x50, 0x44, 0x46]` = `%PDF`) or DOCX/ZIP signature (`[0x50, 0x4B, 0x03, 0x04]` = `PK\x03\x04`); if neither → return 400 `{"error": "Unsupported file type. Only PDF and DOCX are accepted."}`; extension is not consulted (AC-001; OWASP A03 — server-side content-type validation, not trusting client headers)
4. AES-256-GCM encryption via `IDocumentEncryptionService.Encrypt(Stream plaintext)`: generate a random 32-byte key and 12-byte nonce using `RandomNumberGenerator.GetBytes`; encrypt using BouncyCastle `GcmBlockCipher`; return `EncryptedDocument { CipherBytes, WrappedKey }` where `WrappedKey` is the per-document key encrypted by the platform master key from env var `DOCUMENT_MASTER_KEY`; plaintext stream is not buffered in memory longer than necessary (AC-003; OWASP A02 — plaintext never written to disk or logs)
5. Atomic blob write: `documentId = Guid.NewGuid()`; `blobDir` from env var `DOCUMENT_BLOB_DIR`; `tempPath = Path.Combine(blobDir, $"{documentId}.tmp")`; `finalPath = Path.Combine(blobDir, $"{documentId}.enc")`; write cipher text to `tempPath` using `FileStream` with `FileOptions.WriteThrough`; call `File.Move(tempPath, finalPath, overwrite: false)`; wrap entire write in `try/finally`: `finally { if (File.Exists(tempPath)) File.Delete(tempPath); if (writeFailedBeforeMove && File.Exists(finalPath)) File.Delete(finalPath); }` (AC-003; Edge: interrupted upload)
6. `document_records` insert: only after `File.Move` succeeds; `dbContext.DocumentRecords.Add(new DocumentRecord { Id = documentId, PatientId = patientId, OriginalFilename = file.FileName, MimeType = detectedMimeType, SizeBytes = file.Length, UploadTimestamp = DateTimeOffset.UtcNow, Status = "Uploaded", BlobPath = finalPath, WrappedKey = encryptedDoc.WrappedKey })`; `await dbContext.SaveChangesAsync()`; return 201 `{"documentId": documentId, "status": "Uploaded"}` (AC-004; Edge: concurrent — `Guid.NewGuid()` is generated independently per request)
7. No sensitive data in structured logs: `OriginalFilename`, `BlobPath`, `WrappedKey`, and any bytes from the file stream must not appear in any `ILogger` call; log only `documentId` (non-PHI UUID) and sanitised status (e.g., `"Document uploaded successfully for patient {PatientId}"` where `PatientId` is the UUID from JWT — acceptable as a non-PHI identifier for audit, consistent with project convention) (OWASP A02)

---

## Current Project State
```
src/
└── api/
    └── Features/
        └── Documents/
            ├── (DocumentUploadController.cs    — CREATE)
            ├── (IDocumentUploadService.cs      — CREATE)
            ├── (DocumentUploadService.cs       — CREATE)
            ├── (IDocumentEncryptionService.cs  — CREATE)
            ├── (DocumentEncryptionService.cs   — CREATE)
            └── (DocumentRecord.cs             — CREATE)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | src/api/Features/Documents/DocumentUploadController.cs | POST /api/documents/upload with Patient RBAC |
| CREATE | src/api/Features/Documents/IDocumentUploadService.cs | Upload service interface |
| CREATE | src/api/Features/Documents/DocumentUploadService.cs | MIME check, size check, AES-256-GCM, atomic write, DB insert |
| CREATE | src/api/Features/Documents/IDocumentEncryptionService.cs | Encryption abstraction |
| CREATE | src/api/Features/Documents/DocumentEncryptionService.cs | BouncyCastle AES-256-GCM implementation |
| CREATE | src/api/Features/Documents/DocumentRecord.cs | EF Core entity for document_records |
| MODIFY | src/api/Program.cs | Register IDocumentUploadService, IDocumentEncryptionService; add DocumentRecords DbSet |

---

## External References
- https://www.bouncycastle.org/csharp/ (BouncyCastle.Cryptography — `GcmBlockCipher` AES-256-GCM; per-document random key/nonce; AC-003; OWASP A02)
- https://learn.microsoft.com/en-us/aspnet/core/mvc/models/file-uploads?view=aspnetcore-8.0 (ASP.NET Core 8 — `IFormFile` upload; `RequestSizeLimit`; stream handling; AC-001, AC-002)
- https://learn.microsoft.com/en-us/dotnet/standard/io/how-to-copy-directories (File.Move atomicity on same-volume paths — temp file promotion pattern; Edge: interrupted upload; AC-003)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] Upload a valid PDF under 25 MB; verify 201 with `documentId`; verify `document_records` row in DB with all fields; verify the blob file exists at `finalPath` and is not plaintext (AC-003, AC-004)
- [ ] Upload a PNG file; verify 400 `{"error": "Unsupported file type. Only PDF and DOCX are accepted."}` and no DB row created (AC-001)
- [ ] Rename a PNG to `test.pdf`; upload it; verify 400 (AC-001 — extension is not trusted, magic bytes checked)
- [ ] Upload a file > 25 MB; verify 413 and no DB row created (AC-002)
- [ ] Simulate two concurrent uploads from the same patient; verify two independent `document_records` rows with different `documentId` values (Edge: concurrent)
- [ ] Kill the connection mid-write; verify no `document_records` row in DB and no orphaned `.tmp` or `.enc` file in blob dir (Edge: interrupted upload)
- [ ] Call `POST /api/documents/upload` as Staff role; verify 403 (OWASP A01)
- [ ] Verify Serilog output contains no `OriginalFilename`, `BlobPath`, or key bytes (OWASP A02)

---

## Implementation Checklist
- [ ] MIME validation reads the first 8 bytes of the file stream and resets the stream position to 0 before further processing — the file extension from `IFormFile.FileName` is never used as the sole MIME gate (AC-001; OWASP A03)
- [ ] Size check is performed using `file.Length` before the file stream is opened for encryption — file bytes are not buffered into memory purely to determine size (AC-002; OWASP A04)
- [ ] `IDocumentEncryptionService.Encrypt` generates a fresh `RandomNumberGenerator.GetBytes(32)` key and `RandomNumberGenerator.GetBytes(12)` nonce for every invocation — keys are never reused across documents (AC-003; OWASP A02)
- [ ] The `finally` block in `UploadAsync` always deletes the temp file if it exists; it deletes the final file only if the failure occurred after `File.Move` but before `SaveChangesAsync` — preventing orphaned `.enc` files on DB write failure (Edge: interrupted upload; AC-003)
- [ ] `document_records` insert is executed only after `File.Move` returns successfully — if `SaveChangesAsync` throws, the `.enc` file is cleaned up in the `finally` block and no partial record persists (AC-004; Edge: interrupted upload)
- [ ] `patientId` is extracted exclusively from the JWT claim; the endpoint does not accept a `patientId` field in the multipart form body (OWASP A01; A07)
- [ ] `DOCUMENT_BLOB_DIR` and `DOCUMENT_MASTER_KEY` are read from environment variables via `IConfiguration`; they are never hard-coded or logged (OWASP A02 — no secrets in code or logs)
