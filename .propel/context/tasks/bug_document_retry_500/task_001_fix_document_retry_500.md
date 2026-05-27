# Bug Fix Task - bug_document_retry_500

## Bug Report Reference

- Bug ID: `document_retry_500`
- Source: Direct user-reported error — `POST /api/documents/{id}/retry` returns 500 Internal Server Error after a document shows `ExtractionFailed` status

---

## Bug Summary

### Issue Classification

- **Priority**: Critical
- **Severity**: Document retry is completely non-functional — patients cannot re-trigger extraction after a failure, leaving them permanently stuck with failed documents
- **Affected Version**: HEAD — branch `Propel-IQ_Bs`
- **Environment**: All environments. The `patient_entities` table was never created in the database.

### Steps to Reproduce

1. Log in as a Patient and upload a PDF via `POST /documents/upload`
2. The document reaches `ExtractionFailed` status (expected in dev — Ollama not running)
3. Navigate to the document status page and click **"Try Again"**
4. **Expected**: `POST /api/documents/{id}/retry` → `200 OK { "message": "Retry enqueued." }`
5. **Actual**: `POST /api/documents/{id}/retry` → `500 Internal Server Error`

---

## Root Cause Analysis

### Missing `patient_entities` and `clinical_conflicts` tables

- **File**: Database (`upacip`) / `src/api/Migrations/20260521152232_AddPatientEntitiesTable.cs` / `src/api/Migrations/20260522110000_AddClinicalConflictsTable.cs`
- **Cause**: The `RetryDocument` action in `DocumentsController` executes a raw SQL delete against `patient_entities` as the first cleanup step before resetting document status:

  ```csharp
  // patient_entities first (no CASCADE from document_records to patient_entities)
  await _db.Database.ExecuteSqlAsync(
      $"DELETE FROM patient_entities WHERE document_id = {id}", ct);
  ```

  Because EF Core migrations were never applied to the database, the `patient_entities` table did not exist. `ExecuteSqlAsync` threw `PostgresException: 42P01: relation "patient_entities" does not exist` — an unhandled exception that escaped the controller → 500.

  `clinical_conflicts` was also absent; it has FK references to `patient_entities` and would block any `patient_entities` deletion once clinical data exists.

  **Tables already present**: `document_records`, `document_chunks`, `patients`, `users`, `bookings`, `appointment_slots`, `intake_records`, `refresh_tokens`

  **Tables missing (required for retry)**:
  - `patient_entities` — referenced by `DocumentsController.RetryDocument` (first DELETE)
  - `clinical_conflicts` — FK to `patient_entities` (would cause FK error on conflict cleanup)

  **Fix**: Created both tables manually matching the EF migration schema:

  ```sql
  CREATE TABLE patient_entities (
      id             UUID             PRIMARY KEY,
      patient_id     INT              NOT NULL REFERENCES patients(id) ON DELETE CASCADE,
      document_id    UUID             NOT NULL REFERENCES document_records(id) ON DELETE CASCADE,
      type           VARCHAR(50)      NOT NULL,
      value          VARCHAR(500)     NOT NULL,
      confidence     DOUBLE PRECISION NOT NULL,
      low_confidence BOOLEAN          NOT NULL,
      created_at     TIMESTAMPTZ      NOT NULL,
      last_seen_at   TIMESTAMPTZ      NOT NULL
  );
  CREATE UNIQUE INDEX ix_patient_entities_patient_id_type_value
      ON patient_entities(patient_id, type, value);

  CREATE TABLE clinical_conflicts (
      id            UUID          PRIMARY KEY,
      patient_id    INT           NOT NULL REFERENCES patients(id) ON DELETE CASCADE,
      entity_a_id   UUID          NOT NULL REFERENCES patient_entities(id) ON DELETE CASCADE,
      entity_b_id   UUID          NOT NULL REFERENCES patient_entities(id) ON DELETE CASCADE,
      conflict_type VARCHAR(50)   NOT NULL,
      description   VARCHAR(1000) NOT NULL,
      severity      VARCHAR(20)   NOT NULL,
      status        VARCHAR(20)   NOT NULL DEFAULT 'Open',
      created_at    TIMESTAMPTZ   NOT NULL
  );
  CREATE UNIQUE INDEX uq_clinical_conflicts_patient_entity_pair
      ON clinical_conflicts(patient_id, entity_a_id, entity_b_id);
  CREATE INDEX ix_clinical_conflicts_patient_id
      ON clinical_conflicts(patient_id);
  ```

---

## Impact Assessment

- **Affected Features**: Document retry (`POST /documents/{id}/retry`), downstream entity extraction pipeline
- **User Impact**: Patients cannot retry failed document processing — the "Try Again" button always returns 500, leaving documents permanently stuck in `ExtractionFailed` state
- **Data Integrity Risk**: None — the request failed before any state change occurred
- **Security Implications**: None — `patient_entities` DELETE is scoped to the document ID owned by the authenticated patient (OWASP A01); the raw SQL uses parameterised interpolation via `ExecuteSqlAsync` (OWASP A03)

---

## Fix Summary

| Resource | Change |
|---|---|
| Database (`upacip`) | Created `patient_entities` table with UUID PK, FKs to `patients` and `document_records`, unique index on `(patient_id, type, value)` |
| Database (`upacip`) | Created `clinical_conflicts` table with UUID PK, FKs to `patients` and `patient_entities` (cascade), unique index on entity pair |
| `src/api/Controllers/DocumentsController.cs` | No code change required — controller logic was correct |

---

## Verification

After creating both tables, `POST /documents/{id}/retry` on an `ExtractionFailed` document returns:

```json
HTTP 200 OK
{ "message": "Retry enqueued." }
```

The document status resets to `Uploaded` and `processing_started_at` is updated. The background `DocumentTextExtractionWorker` re-receives the `DocumentUploadedEvent` via the in-memory channel — extraction will proceed once Ollama is available in the environment.
