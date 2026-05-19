# Task - TASK_001

## Requirement Reference
- **User Story:** us_016-I
- **Story Location:** .propel/context/tasks/EP-003/us_016-I/us_016-I.md
- **Acceptance Criteria:**
  - AC-001: `POST /intake/ai/start` returns HTTP 200 with `{"sessionId": "<uuid>", "message": "<opening question>"}` within 3 seconds of the request; the session is initialised with empty state for all 5 required field groups
  - AC-002: `POST /intake/ai/message` processes successive patient responses; when all 5 field groups are populated, the response includes `{"allFieldsCollected": true, "summary": {...}}` with non-null values for demographics, medical history, current medications, allergies, and chief complaint
  - AC-003: All Ollama inference requests are directed to `http://ollama:11434/api/chat` (internal Docker network); zero requests are made to any external IP address
  - AC-004: A colloquial patient message such as "my knee has been killing me for weeks" results in an extracted chief complaint of "knee pain" (not a literal quote) and an AI follow-up question about duration or severity
  - AC-005: After each `POST /intake/ai/message` response, an `IntakeRecord` with `status = "Draft"` is upserted in the database containing the PHI-encrypted partial intake data collected so far
- **Edge Cases:**
  - Ollama model not loaded (503 from `http://ollama:11434/api/chat`): the API must return HTTP 503 with `{"error": "AI intake is temporarily unavailable. You can use the manual form instead."}` — the patient's session state must be preserved so they can resume if the model becomes available
  - Inference timeout (30 seconds): if the Ollama HTTP call does not complete within 30 seconds, the `CancellationToken` fires; the API must return an error response with the retry message; the session state in `IDistributedCache` must remain intact for the next attempt

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
| **AI Impact** | Yes |
| **AIR Requirements** | AIR-001 (multi-turn conversational intake using local LLM), AIR-002 (structured field extraction from colloquial patient responses) |
| **AI Pattern** | Multi-turn conversational dialogue with structured extraction — system prompt instructs the Llama 3.1 8B model to collect 5 field groups and return a JSON extraction after each turn |
| **Prompt Template Path** | .propel/context/ai/prompts/intake-dialogue-system-prompt.md |
| **Guardrails Config** | PHI extracted from patient responses must not be emitted to Seq/stdout logs — only structural session IDs are logged; raw patient messages are stored encrypted in `IntakeSessionState` |
| **Model Provider** | Ollama + Llama 3.1 8B (latest stable) — local inference only; `http://ollama:11434` internal Docker network endpoint |

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
| Backend | ASP.NET Core Web API (C#) | .NET 8.0 | TR-002 (`IntakeAiController` with two endpoints; `[Authorize(Roles = "Patient")]`) |
| Backend | EF Core + Npgsql.EntityFrameworkCore.PostgreSQL | 8.x | TR-002 (upsert `IntakeRecord` entity on each turn; AC-005) |
| Backend | Microsoft.Extensions.Caching.Distributed | .NET 8.0 built-in | TR-002 (`IDistributedCache` for `IntakeSessionState`; pattern from us_010 and us_013) |
| AI | Ollama + Llama 3.1 8B | latest stable | AIR-001, AIR-002 (local LLM for multi-turn dialogue and structured extraction; `IHttpClientFactory` typed client to internal endpoint) |

---

## Task Overview

Implement the two AI intake endpoints (`POST /intake/ai/start` and `POST /intake/ai/message`) backed by a typed `OllamaIntakeClient` that calls the Llama 3.1 8B model via the internal Docker network. `IntakeSessionService` maintains conversation history and extracted field state in `IDistributedCache`. After each patient turn, `IntakeRecordService` upserts an encrypted `IntakeRecord` with `status = "Draft"`. The 3-second response window for `/start`, the 30-second Ollama timeout, and the 503 model-not-ready path are all handled with typed exceptions caught at the controller level.

---

## Dependent Tasks
- task_001 (us_005) — `intake_records` table must exist in the schema; `IPhiEncryptionService` must be available for encrypting the JSONB draft data (AC-005)
- task_002 (us_009) — JWT Bearer middleware and `[Authorize(Roles = "Patient")]` must be in place

---

## Impacted Components
- `src/api/Controllers/IntakeAiController.cs` — new controller: `POST /intake/ai/start`, `POST /intake/ai/message`
- `src/api/AI/OllamaIntakeClient.cs` — new typed HTTP client for Ollama `/api/chat` endpoint
- `src/api/AI/IntakeSessionService.cs` — new service: manages `IntakeSessionState` in `IDistributedCache`
- `src/api/AI/IntakeSessionState.cs` — new class: conversation history + 5-field extraction state
- `src/api/AI/Exceptions/IntakeAiUnavailableException.cs` — new typed exception for 503/timeout paths
- `src/api/Services/IntakeRecordService.cs` — new service: upserts `IntakeRecord` with encrypted JSONB
- `src/api/Program.cs` — modified: register `OllamaIntakeClient` via `AddHttpClient`, configure 30s timeout and Ollama base URL from env var; register `IntakeSessionService` and `IntakeRecordService`

---

## Implementation Plan
1. Create `IntakeSessionState` class: `Guid SessionId`, `Guid PatientId`, `List<OllamaChatMessage> History`, `IntakeFieldState Fields` (with nullable properties for Demographics, MedicalHistory, Medications, Allergies, ChiefComplaint); `bool AllFieldsCollected` computed property that returns true when all 5 field properties are non-null (AC-001, AC-002)
2. Implement `OllamaIntakeClient` as a typed `HttpClient` wrapper: `ChatAsync(List<OllamaChatMessage> history, CancellationToken ct)` posts to `/api/chat` with model `llama3.1:8b` and the system prompt loaded from `intake-dialogue-system-prompt.md`; on `HttpRequestException` with status 503 → throw `IntakeAiUnavailableException`; the `CancellationToken` passed from the controller enforces the 30-second timeout (AC-003, AC-004; Edge: 503, timeout)
3. Implement `IntakeSessionService` with `CreateSessionAsync(Guid patientId)` and `GetSessionAsync(Guid sessionId)` / `UpdateSessionAsync(IntakeSessionState state)`; uses `IDistributedCache` with key `intake_session:{sessionId}` and TTL of 2 hours serialised as JSON; validates `state.PatientId == requestingPatientId` in `GetSessionAsync` to prevent cross-patient session access (AC-001, AC-002; OWASP A01)
4. Implement `POST /intake/ai/start` in `IntakeAiController`: create session via `IntakeSessionService`, call `OllamaIntakeClient.ChatAsync` with the opening system prompt, store Ollama reply in session history, return `{sessionId, message}` — wrap the entire Ollama call in a `CancellationTokenSource(TimeSpan.FromSeconds(29))` to stay within the 3-second SLA budget after JWT/DB overhead (AC-001)
5. Implement `POST /intake/ai/message` in `IntakeAiController`: validate sessionId ownership, append patient message to session history, call `OllamaIntakeClient.ChatAsync`, parse the model's structured JSON extraction embedded in its reply to update `IntakeSessionState.Fields`, call `IntakeSessionService.UpdateSessionAsync`, then call `IntakeRecordService.UpsertDraftAsync`, return response (AC-002, AC-004, AC-005)
6. Implement `IntakeRecordService.UpsertDraftAsync(Guid patientId, IntakeFieldState fields)`: serialise `fields` to JSON, encrypt via `IPhiEncryptionService`, upsert `IntakeRecord {PatientId, Status="Draft", Data=encryptedJson, UpdatedAt=UtcNow}` using EF Core `AddOrUpdate` pattern (AC-005; OWASP A09)
7. Catch `IntakeAiUnavailableException` in `IntakeAiController` action methods → return `ObjectResult(new { error = "AI intake is temporarily unavailable. You can use the manual form instead." }, 503)`; catch `OperationCanceledException` (timeout) → return the retry message in a 504 response; session state is preserved in both paths (Edge: model not loaded, Edge: timeout)

---

## Current Project State
```
src/
└── api/
    ├── Controllers/
    │   └── IntakeAiController.cs                    (CREATE)
    ├── AI/
    │   ├── OllamaIntakeClient.cs                    (CREATE)
    │   ├── IntakeSessionService.cs                  (CREATE)
    │   ├── IntakeSessionState.cs                    (CREATE)
    │   └── Exceptions/
    │       └── IntakeAiUnavailableException.cs      (CREATE)
    ├── Services/
    │   └── IntakeRecordService.cs                   (CREATE)
    └── Program.cs                                   (MODIFY — register Ollama HttpClient + services)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | src/api/Controllers/IntakeAiController.cs | POST /intake/ai/start + POST /intake/ai/message; Authorize(Patient); exception handling |
| CREATE | src/api/AI/OllamaIntakeClient.cs | Typed HttpClient → http://ollama:11434/api/chat; 503 + timeout exception mapping |
| CREATE | src/api/AI/IntakeSessionService.cs | IDistributedCache session CRUD with patientId ownership check |
| CREATE | src/api/AI/IntakeSessionState.cs | Conversation history + 5-field extraction state + AllFieldsCollected computed property |
| CREATE | src/api/AI/Exceptions/IntakeAiUnavailableException.cs | Typed exception for 503 and timeout paths |
| CREATE | src/api/Services/IntakeRecordService.cs | Upsert encrypted IntakeRecord Draft |
| MODIFY | src/api/Program.cs | Register OllamaIntakeClient (AddHttpClient + 30s timeout + base URL); register services |

---

## External References
- https://ollama.com/docs/api (Ollama API — `/api/chat` request/response format for multi-turn dialogue)
- https://learn.microsoft.com/en-us/dotnet/core/extensions/httpclient-factory (IHttpClientFactory typed clients — timeout and base address configuration)
- https://learn.microsoft.com/en-us/aspnet/core/performance/caching/distributed?view=aspnetcore-8.0 (IDistributedCache — TTL and serialisation patterns)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] Authenticate as Patient; call `POST /intake/ai/start`; verify HTTP 200 with `sessionId` and a non-empty `message` field returned within 3 seconds; verify session exists in distributed cache (AC-001)
- [ ] Continue the session across 5 successive `POST /intake/ai/message` calls with responses covering all 5 field groups; verify the final response includes `"allFieldsCollected": true` and the `summary` object has non-null values for all groups (AC-002)
- [ ] Monitor Docker network traffic during an intake session; verify all Ollama HTTP calls go to `http://ollama:11434` and no request leaves the Docker bridge to an external IP (AC-003)
- [ ] Send `POST /intake/ai/message` with body `"my knee has been killing me for weeks"`; inspect the session state's `ChiefComplaint` field; verify it contains an extracted concept such as "knee pain" and the AI reply contains a follow-up question (AC-004)
- [ ] After the 2nd `POST /intake/ai/message`, query the `intake_records` table for the patient; verify a row exists with `status = "Draft"` and an encrypted JSONB `data` column (AC-005)
- [ ] Stop the Ollama container; call `POST /intake/ai/start`; verify HTTP 503 with the correct error message (Edge: model not loaded)
- [ ] Set a short timeout (e.g., 2s) in `OllamaIntakeClient` for testing; send a message; verify HTTP 504 with the retry message and the session remains in cache (Edge: inference timeout)

---

## Implementation Checklist
- [ ] `OllamaIntakeClient` base URL is loaded from `Environment.GetEnvironmentVariable("OLLAMA_BASE_URL")` defaulting to `http://ollama:11434` — never hardcoded; external URLs are blocked at the network level but must also not be configurable via source code (AC-003; OWASP A02)
- [ ] `IntakeSessionService.GetSessionAsync` validates `state.PatientId == requestingPatientId` before returning the session — prevents Patient A from continuing or reading Patient B's session even with a valid sessionId (AC-001; OWASP A01)
- [ ] PHI extracted in `IntakeSessionState.Fields` is never emitted to `ILogger` or Serilog — only the structural `sessionId` and `actionType` are logged; raw patient messages in `History` are also excluded from logs (AIR guardrails; OWASP A09; HIPAA minimum-necessary)
- [ ] `IntakeRecordService.UpsertDraftAsync` calls `IPhiEncryptionService.Encrypt` on the serialised fields JSON before persisting — the `IntakeRecord.Data` column contains only ciphertext at rest (AC-005; OWASP A02; HIPAA §164.312(a)(2)(iv))
- [ ] `IntakeAiUnavailableException` and `OperationCanceledException` are caught exclusively in `IntakeAiController` action methods — they must not reach the global exception handler which would return a generic 500 (Edge handling; OWASP A05)
- [ ] `/intake/ai/start` wraps the Ollama call in a `CancellationTokenSource(TimeSpan.FromSeconds(29))` — the 3-second SLA (AC-001) must account for JWT validation and DB overhead, so the raw Ollama call budget is capped below 3 seconds (AC-001; latency SLA)
- [ ] Session TTL in `IDistributedCache` is set to 2 hours with `AbsoluteExpirationRelativeToNow` — expired sessions return 404 from `GetSessionAsync`; the controller maps this to a user-friendly "Session expired, please start a new intake" 404 response (AC-001; session lifecycle)
