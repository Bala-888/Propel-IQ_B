# Bug Fix Task - bug_document_upload_500

## Bug Report Reference

- Bug ID: `document_upload_500`
- Source: Direct user-reported error — `POST /api/documents/upload` returns 500 Internal Server Error for an authenticated patient

---

## Bug Summary

### Issue Classification

- **Priority**: Critical
- **Severity**: Document upload is completely non-functional for all patients — any attempt to upload a PDF or DOCX results in a 500 with no useful client-facing error
- **Affected Version**: HEAD — branch `Propel-IQ_Bs`
- **Environment**: All environments. Two independent root causes; either one alone is sufficient to produce a 500.

### Steps to Reproduce

1. Register/log in as a Patient (e.g., `patient@clinic.com`)
2. Navigate to `/documents/upload`, select a valid PDF, and click Upload
3. **Expected**: `POST /api/documents/upload` → `201 Created` with `{ documentId, status: "Uploaded" }`
4. **Actual**: `POST /api/documents/upload` → `500 Internal Server Error`

---

## Root Cause Analysis (Three Distinct Issues)

### Issue 1 — `DOCUMENT_BLOB_DIR` environment variable not set

- **File**: `src/api/Properties/launchSettings.json`
- **Cause**: `DocumentUploadService` constructor reads `configuration["DOCUMENT_BLOB_DIR"]` and throws `InvalidOperationException("DOCUMENT_BLOB_DIR environment variable is not configured")` if the value is null or empty. This exception escapes DI resolution and causes a 500 on **every request** to the controller — before any file I/O occurs.

  The `launchSettings.json` file defined all other environment variables (`JWT_SECRET`, `PHI_ENCRYPTION_KEY`, `DOCUMENT_MASTER_KEY`, `POSTGRES_CONNECTION_STRING`, `OLLAMA_BASE_URL`) but omitted `DOCUMENT_BLOB_DIR` entirely.

  **Fix**: Added `"DOCUMENT_BLOB_DIR": "C:\\Temp\\upacip-docs"` to both the `Api` and `IIS Express` profiles in `launchSettings.json`. Also created the directory `C:\Temp\upacip-docs` on the development machine.

### Issue 2 — `document_records` table missing from the database

- **File**: Database (`upacip`) / `src/api/Data/AppDbContext.cs` / `src/api/Features/Documents/DocumentRecord.cs`
- **Cause**: The EF Core entity `DocumentRecord` is mapped to the `document_records` table (snake_case convention) via `AppDbContext.DocumentRecords`. The `DocumentUploadService` calls `_db.DocumentRecords.Add(...)` and `_db.SaveChangesAsync()` after writing the encrypted blob to disk. Because no EF migration was applied to the database, the table did not exist → `SaveChangesAsync` threw `PostgresException: 42P01: relation "document_records" does not exist` → unhandled 500.

  Similarly, the `document_chunks` table (used by the downstream text-extraction background worker) was also absent.

  **Fix**: Created both tables manually with column definitions matching the EF entity and `OnModelCreating` configuration:

  ```sql
  CREATE TABLE document_records (
      id                    UUID         PRIMARY KEY,
      patient_id            INT          NOT NULL REFERENCES patients(id),
      original_filename     TEXT         NOT NULL,
      mime_type             TEXT         NOT NULL,
      size_bytes            BIGINT       NOT NULL,
      upload_timestamp      TIMESTAMPTZ  NOT NULL,
      status                TEXT         NOT NULL,
      failure_reason        VARCHAR(500) NULL,
      blob_path             TEXT         NOT NULL,
      wrapped_key           BYTEA        NOT NULL,
      processing_started_at TIMESTAMPTZ  NOT NULL
  );

  CREATE TABLE document_chunks (
      id            UUID  PRIMARY KEY,
      document_id   UUID  NOT NULL REFERENCES document_records(id) ON DELETE CASCADE,
      chunk_index   INT   NOT NULL,
      content       TEXT  NOT NULL,
      token_count   INT   NOT NULL DEFAULT 0
  );

  CREATE INDEX ix_document_chunks_document_id
      ON document_chunks(document_id);
  CREATE UNIQUE INDEX ix_document_chunks_document_id_chunk_index
      ON document_chunks(document_id, chunk_index);
  ```

### Issue 3 — `patientId` read from `sub` claim (user ID) instead of `pid` claim (patient ID)

- **File**: `src/api/Features/Documents/DocumentUploadController.cs`
- **Cause**: The controller read `User.FindFirstValue("sub")` and used the result as `patientId`. The `sub` claim contains `users.id` (e.g., `8`), but `document_records.patient_id` is a FK to `patients.id` (e.g., `6`) — a separate auto-increment sequence. This is the same class of bug fixed in `ManualIntakeController`, `BookingsController`, `ModeSwitchController`, and `DocumentsController` (see `bug_manual_intake_401_unauthorized`). Inserting with the wrong patient ID would violate the FK constraint and produce a `23503` FK violation 500.

  **Fix**: Changed claim read to `User.FindFirstValue("pid") ?? User.FindFirstValue("sub")` — reads the `pid` claim (set during login to `patients.id`) with `sub` as a fallback for tokens issued before the `pid` claim was added.

---

## Impact Assessment

- **Affected Features**: Document upload (`POST /documents/upload`), document status polling (`GET /documents/{id}/status`)
- **User Impact**: Patients cannot upload any documents — the feature is 100% non-functional end-to-end
- **Data Integrity Risk**: Low — requests failed before persistent state was created (Issue 1 fails before any I/O; Issue 2 fails after blob write but the service cleans up the orphaned file in its `finally` block)
- **Security Implications**: None introduced by the fix; `patientId` is always sourced from the server-signed JWT, never from the request body (OWASP A01)

---

## Fix Summary

| File | Change |
|---|---|
| `src/api/Properties/launchSettings.json` | Added `DOCUMENT_BLOB_DIR` to `Api` and `IIS Express` profiles |
| `src/api/Features/Documents/DocumentUploadController.cs` | `patientId` reads `pid ?? sub` instead of only `sub` |
| Database (`upacip`) | Created `document_records` and `document_chunks` tables with FK/index constraints |

---

## Verification

After all three fixes, `POST /documents/upload` with a valid PDF (magic bytes `%PDF`) and a patient JWT returns:

```json
HTTP 201 Created
{ "documentId": "0350a9e7-898b-423d-8a1a-e137111d4031", "status": "Uploaded" }
```

Database row:

```
patient_id = 6   ← patients.id (correct)
status     = ExtractionFailed  ← expected; Ollama not running in dev, but upload pipeline succeeded
```

The `ExtractionFailed` status is expected in development — Ollama (`OLLAMA_BASE_URL`) is not running, so the background `DocumentTextExtractionWorker` cannot process the file. The HTTP layer (upload + DB insert + blob write) is fully functional.
