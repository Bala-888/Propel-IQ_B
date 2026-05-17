# Architecture Design

## Project Overview

The **Unified Patient Access & Clinical Intelligence Platform** is a standalone, HIPAA-compliant healthcare platform serving three user roles — Patient, Staff (front desk/call center), and Admin. It combines a modern patient-centric appointment booking system with a Trust-First clinical intelligence engine.

Patients self-register, complete AI-assisted or manual intake, book appointments with dynamic preferred-slot swap, receive multi-channel reminders and calendar sync, and upload historical clinical documents. Clinical staff manage walk-in bookings, a real-time same-day queue, patient arrivals, and a 360-Degree Patient View with AI-extracted clinical data and verified ICD-10/CPT code suggestions. Admins govern user accounts, role assignments, and platform KPI metrics.

The platform runs entirely on free, open-source infrastructure deployed as a single Docker Compose stack, with no paid cloud services. All AI inference executes locally to ensure zero PHI transmission outside the deployment boundary.

---

## Architecture Goals

- **Architecture Goal 1**: HIPAA-First — Every design decision defaults to PHI protection; encryption, access control, and immutable audit logging are baseline requirements, not optional controls.
- **Architecture Goal 2**: Trust-First AI — All AI suggestions (clinical extraction, ICD-10/CPT codes, conflict detection) are traceable to source evidence in uploaded documents; no black-box outputs exist in the system.
- **Architecture Goal 3**: Free Infrastructure — Zero paid cloud services; all components run on Docker Compose using exclusively free and open-source technologies.
- **Architecture Goal 4**: Real-time Staff Operations — Queue and booking status updates are event-driven and delivered within 5 seconds; staff workflows do not require manual page refreshes.
- **Architecture Goal 5**: Human-in-the-Loop — AI outputs never auto-apply to patient records; explicit staff confirmation is required for all clinical decisions.

---

## Non-Functional Requirements

- NFR-001: [SOURCE:INPUT] System MUST handle, transmit, and store all patient health information (PHI) in full compliance with HIPAA Security Rule requirements (45 CFR Part 164), including access controls (§164.312(a)), audit controls (§164.312(b)), integrity controls (§164.312(c)), and transmission security (§164.312(e)).
  Basis: FR-043 "100% HIPAA-compliant data handling, transmission, and storage"; BRD NFR section; federal law mandate.

- NFR-002: [SOURCE:INPUT] System MUST maintain 99.9% availability during production operation, equating to a maximum of 8.76 hours unplanned downtime per calendar year.
  Basis: Spec success criteria — "99.9% platform uptime maintained in production deployment."

- NFR-003: [SOURCE:INPUT] System MUST make the AI-generated 360-Degree Patient View and clinical code suggestions accessible to staff within 2 minutes of document processing initiation, measured from upload completion to rendered view.
  Basis: Spec success criteria — "staff administrative time per appointment reduced (target: 360-degree view accessible within 2 minutes)."

- NFR-004: [SOURCE:INPUT] System MUST enforce role-based access control (RBAC) across all endpoints, invalidate user sessions after exactly 15 minutes of inactivity, and apply progressive rate limiting after repeated authentication failures from the same source.
  Basis: FR-003, FR-004, FR-005; UC-004 (progressive rate limiting); OWASP A07 Identification and Authentication Failures.

- NFR-005: [SOURCE:INPUT] System MUST encrypt all PHI at rest using AES-256 (or NIST-approved equivalent) and enforce TLS 1.2 or higher for all data in transit, with no unencrypted PHI channel permitted.
  Basis: FR-045 "all patient data MUST be encrypted at rest using AES-256 and in transit using TLS 1.2 or higher"; HIPAA Security Rule §164.312(e)(2)(ii).

- NFR-006: [SOURCE:INPUT] System MUST record every patient data access, modification, booking action, authentication event, and administrative operation in an immutable audit log capturing: actor identity, actor role, action type, resource type, resource ID, IP address, and UTC timestamp.
  Basis: FR-044 "immutable audit logging for all patient and staff actions"; HIPAA §164.312(b) Audit Controls.

- NFR-007: [SOURCE:INPUT] The AI-Human Agreement Rate for clinical data extraction and medical code suggestions MUST exceed 98%, measured as the percentage of AI-suggested items accepted without modification by clinical staff.
  Basis: Spec success criteria — "AI-Human Agreement Rate exceeds 98% for clinical data extraction and medical code suggestions."

- NFR-008: [SOURCE:INPUT] System MUST support at least 50 simultaneous authenticated users (across Patient, Staff, and Admin roles) without non-AI API response time exceeding 3 seconds under normal operating conditions.
  Basis: "Healthcare organization" deployment context implies concurrent multi-user access; no explicit concurrency target in spec; 50 users and 3-second threshold are minimum viable baselines for clinic-scale operation.

- NFR-009: [SOURCE:INPUT] All external notification deliveries (email confirmations, SMS reminders, swap notifications) MUST implement automatic retry logic with at least 3 retry attempts using exponential back-off before recording a final delivery failure.
  Basis: FR-013 (PDF email delivery), UC-013 "retry up to 3 times with exponential back-off"; UC-015, UC-017 specify identical retry requirements.

- NFR-010: [SOURCE:INPUT] All platform components MUST be deployable as a single Docker Compose stack using exclusively free and open-source technologies; no paid cloud infrastructure, paid API keys, or commercial software licenses MAY be required for platform operation.
  Basis: BRD Section 4 — "free, open-source-friendly platforms"; spec "free-infrastructure-only deployment"; BRD explicitly excludes AWS, Azure, and paid cloud.

- NFR-011: [SOURCE:INPUT] Walk-in booking creation and patient arrival status changes MUST be reflected in the staff queue dashboard within 5 seconds without requiring a manual page refresh.
  Basis: FR-028 "appear in the same-day queue dashboard in real time without requiring a page refresh."

**Note**: NFR-008 is marked [SOURCE:INPUT]; no [UNCLEAR] requirements exist in this section.

---

## Data Requirements

- DR-001: [SOURCE:INPUT] System MUST encrypt all patient health information fields (demographics, medical history, insurance identifiers, clinical data, and intake records) using AES-256 column-level encryption before persistence to the database.
  Basis: FR-031, FR-045; HIPAA Security Rule §164.312(a)(2)(iv) Encryption and Decryption.

- DR-002: [SOURCE:INPUT] The audit log table MUST be implemented as an append-only data structure; no UPDATE or DELETE SQL operations MAY be permitted on audit log records at any application or database level.
  Basis: FR-044 "immutable audit logging"; HIPAA §164.312(b) requires audit records to be tamper-evident.

- DR-003: [SOURCE:INPUT] All booking state transitions (slot confirmation, preferred-slot swap, slot release) MUST execute within a single ACID-compliant database transaction to prevent double-booking, orphaned slot states, or partial swap records.
  Basis: FR-015 (duplicate booking prevention), FR-017 (auto-swap), FR-018 (slot release); concurrent booking conflict is an implicit data integrity risk in any multi-user appointment system.

- DR-004: [SOURCE:INPUT] Each uploaded clinical document record MUST persist: patient ID (FK), original filename, MIME type, SHA-256 file hash (integrity), encrypted storage path, upload timestamp, uploader identity, and processing status (Pending/Extracting/Complete/Failed).
  Basis: FR-030 (file validation), FR-031 (encryption at rest), FR-032 (extraction pipeline entry point); HIPAA data integrity requirement §164.312(c)(1).

- DR-005: [SOURCE:EXTERNAL] Patient records, clinical documents, intake records, appointment records, and audit log entries MUST be retained for a minimum of 6 years from the date of creation or last activity per HIPAA minimum retention standard (45 CFR §164.530(j)).
  Basis: HIPAA §164.530(j) — minimum 6-year retention for HIPAA documentation; no explicit retention period stated in spec.

- DR-006: [SOURCE:INPUT] Extracted clinical document text chunks and their vector embeddings MUST be stored with association to: patient ID, source document ID, chunk sequence number, chunk text, embedding vector, and extraction timestamp — to enable RAG-based retrieval with source traceability per the Trust-First principle.
  Basis: FR-035 "source-document traceability for each extracted data point"; FR-036/037 "each suggestion linked to the source data"; AIR-004 (RAG architecture pattern).

- DR-007: [SOURCE:INPUT] Each ICD-10-CM diagnosis code and CPT procedure code suggestion MUST be stored with: patient ID, code value, code description, source chunk reference(s) supporting the suggestion, AI confidence score, review status (Pending/Accepted/Rejected), reviewing staff ID, and review timestamp.
  Basis: FR-036, FR-037 "each suggestion linked to the source data that supports it"; FR-038 "no code MUST be finalized without a human decision"; Trust-First principle.

- DR-008: [SOURCE:INPUT] Each confirmed booking record MUST store: the computed no-show risk score (0–100), the input factor values used in the computation (booking lead time, prior no-show count, booking channel), and the score computation timestamp.
  Basis: FR-014 "rule-based no-show risk assessment … surface this score to staff"; FR-040 (staff dashboard displays risk scores); factors and timestamp are required for audit and model improvement.

### Domain Entities [CONDITIONAL: Feature involves persistent data]

- **User**: Base identity entity. Attributes: id (UUID), email (unique), passwordHash (bcrypt), role (enum: Patient/Staff/Admin), isActive (bool), createdAt, lastLoginAt. Relationships: has-many Bookings (if Patient), has-many AuditLog entries (as actor).
- **Patient**: Extends User. Attributes: dateOfBirth [encrypted], phone [encrypted], insuranceProvider [encrypted], insuranceId [encrypted], profileCreatedAt. Relationships: has-many IntakeRecords, ClinicalDocuments, Bookings, MedicalCodeSuggestions.
- **IntakeRecord**: Pre-visit data collection. Attributes: id, patientId (FK), mode (enum: AI/Manual), status (enum: Draft/Complete), intakeData (JSONB, encrypted), completedAt, createdAt. Relationships: belongs-to Patient.
- **AppointmentSlot**: Available time unit. Attributes: id, scheduledDate, startTime, durationMinutes, status (enum: Available/Booked/Blocked), createdBy (admin). Relationships: has-many Bookings.
- **Booking**: Patient-slot association. Attributes: id, patientId (FK), slotId (FK), preferredSlotId (FK, nullable), status (enum: Confirmed/Cancelled/Completed/WalkIn), noShowRiskScore, riskFactors (JSONB), riskScoredAt, bookedAt, bookedBy. Relationships: belongs-to Patient, AppointmentSlot; has-many ReminderSchedules.
- **ClinicalDocument**: Uploaded patient document. Attributes: id, patientId (FK), originalFilename, mimeType, fileHash (SHA-256), storagePath [encrypted], uploadedAt, uploadedBy, processingStatus (enum: Pending/Extracting/Complete/Failed). Relationships: belongs-to Patient; has-many ExtractedRecords.
- **ExtractedRecord**: Structured entity from document. Attributes: id, documentId (FK), patientId (FK), entityType (enum: Vital/Medication/Diagnosis/Note/AllergyCode), entityValue (JSONB, encrypted), confidence (float), chunkRef (text), extractedAt. Relationships: belongs-to ClinicalDocument.
- **ChunkEmbedding**: Vector embedding for RAG. Attributes: id, documentId (FK), patientId (FK), chunkSequence (int), chunkText (encrypted), embedding (vector(1536)), createdAt. Relationships: belongs-to ClinicalDocument.
- **MedicalCodeSuggestion**: AI code suggestion with review state. Attributes: id, patientId (FK), codeType (enum: ICD10/CPT), codeValue, codeDescription, sourceChunkRefs (text[]), aiConfidence, reviewStatus (enum: Pending/Accepted/Rejected), reviewedBy (FK, nullable), reviewedAt, createdAt. Relationships: belongs-to Patient.
- **AuditLog**: Immutable operation record. Attributes: id, actorId (FK), actorRole, actionType, resourceType, resourceId, ipAddress, userAgent, details (JSONB), occurredAt. Constraints: INSERT-only at DB level; no UPDATE/DELETE grants.
- **ReminderSchedule**: Notification delivery record. Attributes: id, bookingId (FK), channelType (enum: Email/SMS), scheduledAt, sentAt (nullable), deliveryStatus (enum: Pending/Sent/Failed/Retrying), attemptCount. Relationships: belongs-to Booking.
- **InsuranceRecord**: Seeded dummy pre-check dataset. Attributes: id, providerName, insuranceIdPattern. Relationships: none (reference data only).

---

## AI Consideration

**Status:** Applicable

Tags `[AI-CANDIDATE]` detected on FR-007 (AI conversational intake) and FR-032 (AI clinical document extraction). Tags `[HYBRID]` detected on FR-034 (conflict detection), FR-036 (ICD-10 code suggestion), and FR-037 (CPT code suggestion). $AI_SIGNAL = true. Proceed to AI Requirements section.

---

## AI Requirements

- AIR-001: [SOURCE:INPUT] The AI inference engine MUST execute entirely within the local deployment boundary; no patient health information, document content, or any PHI derivative MAY be transmitted to external APIs, cloud-based LLM services, or third-party AI providers.
  Basis: NFR-001 (HIPAA — PHI must not leave the controlled environment without BAA); NFR-010 (no paid cloud services); a cloud AI API call constitutes both a HIPAA risk and a potential cost; local Ollama deployment eliminates both.

- AIR-002: [SOURCE:INPUT] The AI conversational intake engine MUST support multi-turn natural language dialogue, progressively collecting patient demographics, medical history, current medications, allergies, and chief complaint, with the ability to interpret ambiguous or colloquial health descriptions.
  Basis: FR-007 "AI-assisted conversational intake interface that collects patient demographics, medical history, and chief complaint through natural language dialogue."

- AIR-003: [SOURCE:INPUT] The AI clinical extraction engine MUST parse text extracted from uploaded clinical PDFs and produce structured output identifying: patient vitals, medication lists, diagnoses, clinical notes, and relevant dates — each with an associated confidence score.
  Basis: FR-032 "use an AI extraction engine to parse uploaded clinical documents and extract structured data including patient vitals, medication history, diagnoses, and clinical notes."

- AIR-004: [SOURCE:INPUT] The clinical extraction and medical code suggestion pipeline MUST implement a Retrieval-Augmented Generation (RAG) architecture: document text MUST be chunked and embedded into a vector store; at inference time, relevant chunks MUST be retrieved and provided as context to the LLM; every AI output MUST reference the specific chunk(s) it was derived from.
  Basis: FR-035 "source-document traceability for each extracted data point"; FR-036/037 "each suggestion linked to the source data that supports it"; Trust-First principle from BRD — source linkage is a core product differentiator.

- AIR-005: [SOURCE:INPUT] The AI conflict detection engine MUST compare extracted data points across all documents for a given patient, identify contradictions (e.g., conflicting medication entries, differing diagnoses for the same condition), and surface each conflict to staff with a confidence level and the source document references for both conflicting values.
  Basis: FR-034 "detect conflicting data points across aggregated documents … prominently surface each conflict to staff for manual review and resolution."

- AIR-006: [SOURCE:INPUT] The AI medical coding engine MUST generate ICD-10-CM diagnosis code suggestions and CPT procedure code suggestions derived from the aggregated patient data, with each suggestion accompanied by the specific source text passage that supports the code selection.
  Basis: FR-036 "suggest relevant ICD-10 diagnosis codes … with each suggestion linked to the source data"; FR-037 "suggest relevant CPT procedure codes … with each suggestion linked to the source data."

- AIR-007: [SOURCE:INPUT] The AI system MUST NOT auto-finalize any clinical data extraction result, conflict resolution, or medical code suggestion; every AI output MUST remain in a Pending review state until an authorized staff member explicitly accepts or rejects it.
  Basis: FR-038 "no code MUST be finalized without a human decision"; Trust-First principle; the 98% AI-Human Agreement Rate KPI (NFR-007) implies mandatory human review of all suggestions.

- AIR-008: [SOURCE:INPUT] The AI clinical extraction pipeline MUST complete document processing (text extraction, chunking, embedding, entity extraction, and code suggestion generation) within 120 seconds per document to support the 2-minute staff verification constraint.
  Basis: NFR-003 (2-minute verification window); 120 seconds is derived by reserving the remaining 0 seconds for UI render time within the 2-minute window; this is a testable upper bound per document.

**Note**: AIR-001, AIR-004, AIR-008 are [SOURCE:INPUT]. No [UNCLEAR] items exist.

### AI Architecture Pattern

**Selected Pattern:** RAG + Direct LLM Inference (Hybrid)
**Rationale:**
- **RAG** applies to clinical document extraction (AIR-003), conflict detection (AIR-005), and medical code suggestion (AIR-006) — all require source traceability (AIR-004). Documents are chunked → embedded via Ollama embedding model → stored in pgvector → retrieved at inference time → LLM generates grounded output with cited chunk references.
- **Direct LLM Inference** applies to AI conversational intake (AIR-002) — no retrieval from existing documents is needed; the LLM maintains conversation state and generates follow-up questions based on dialogue history alone.
- Both pipelines route through the local Ollama endpoint, satisfying AIR-001 (local-only inference).

---

## Architecture and Design Decisions

- **Decision 1 — Local-first AI architecture**: All LLM inference is executed on-premises via Ollama to comply with HIPAA (no PHI leaves the deployment boundary, eliminating the need for a Business Associate Agreement with a cloud AI provider) and the free infrastructure constraint (no per-token API charges). This constrains AI model selection to quantized open-source models capable of running on CPU.

- **Decision 2 — pgvector as the vector store (no separate vector database)**: pgvector is integrated into the existing PostgreSQL instance as an extension, avoiding a separate vector database service (Qdrant, ChromaDB). This reduces operational complexity, eliminates an additional Docker service, and keeps all patient-associated embeddings within the HIPAA-controlled database boundary with existing access controls and encryption (DR-006).

- **Decision 3 — SignalR for real-time queue updates (no external message broker)**: ASP.NET Core SignalR provides WebSocket-based real-time push to the React frontend for queue updates (NFR-011, FR-028). A dedicated message broker (Redis pub/sub, RabbitMQ) is not required at clinic scale and would add infrastructure cost and complexity. If scale demands horizontal API scaling, Redis backplane for SignalR can be added without architectural change.

- **Decision 4 — Append-only audit table with DB-level INSERT-only grant**: HIPAA audit immutability (NFR-006, DR-002) is enforced at the database permission level — the application database user has INSERT-only privilege on the audit_log table, with no UPDATE or DELETE grants. This prevents application-layer bugs or injection attacks from corrupting audit records.

- **Decision 5 — pgcrypto for column-level PHI encryption**: Column-level AES-256 encryption via pgcrypto (TR-007) is applied at the application layer before persistence for all PHI fields. This provides encryption-at-rest (NFR-005, DR-001) independent of disk-level encryption, ensuring PHI remains protected even if storage volumes are accessed directly. Encryption keys are managed via environment variables injected at container startup.

- **Decision 6 — Human-in-the-loop as an application state machine**: All AI outputs (intake summaries, extracted clinical entities, conflict flags, code suggestions) exist in a `Pending` state in the database. No downstream record update occurs until an authorized staff member transitions the state to `Accepted` or `Rejected`. This state machine is enforced at the API layer, making auto-finalization architecturally impossible (AIR-007, FR-038).

- **Decision 7 — React 18 selected over Angular 17 as frontend framework**: React's ecosystem provides superior flexibility for the complex, data-intensive clinical views required (360-degree patient view with expandable conflict sections, live queue grids). The `@microsoft/signalr` client library integrates cleanly with React. Netlify/Vercel free tier is compatible with React SPA deployments (NFR-010).

- **Decision 8 — .NET 8.0 selected over Java Spring Boot as backend runtime**: ASP.NET Core SignalR is built into the .NET platform (no additional service), QuestPDF provides MIT-licensed PDF generation (FR-013), and ASP.NET Core Identity provides production-grade RBAC + JWT out of the box. The .NET 8.0 Docker image has a lower memory footprint (~200 MB) than JVM-based Spring Boot (~400 MB), reducing the total Docker Compose resource requirement.

---

## Technology Stack

| Layer | Technology | Version | Justification |
|-------|------------|---------|---------------|
| Frontend | React + TypeScript | 18.x | NFR-008 (complex data grid performance for 360° view), NFR-011 (SignalR client for real-time queue), TR-001 |
| Mobile | N/A | — | Out of Phase 1 scope; patient self-check-in via mobile is explicitly excluded |
| Backend | ASP.NET Core Web API (C#) | .NET 8.0 | NFR-004 (ASP.NET Core Identity + JWT), NFR-011 (SignalR built-in), NFR-010 (free Docker runtime), TR-002 |
| Database | PostgreSQL + pgcrypto + pgvector | 15.3+ / pgcrypto built-in / pgvector 0.5+ | DR-001 (pgcrypto AES-256 column encryption), DR-006 (pgvector RAG embeddings), DR-003 (ACID transactions), NFR-010 (free, OSS), TR-003 |
| AI/ML | Ollama + Llama 3.1 8B (Q4_K_M) + pgvector RAG | Ollama 0.1.29+ / Llama 3.1 8B | AIR-001 (local inference), AIR-002 (128K context for long clinical docs), AIR-003 (instruction-following extraction), AIR-004 (RAG via pgvector), NFR-010 (free, OSS), TR-004 |
| Testing | xUnit (.NET) + React Testing Library + Playwright | Latest stable | NFR-007 (AI agreement rate validation), NFR-002 (availability regression testing) |
| Infrastructure | Docker Compose + Nginx | Docker Compose 2.x / Nginx 1.25+ | NFR-010 (free, OSS, no paid cloud), NFR-002 (container restart policies for availability), TR-014 |
| Security | ASP.NET Core Identity + JWT Bearer + pgcrypto + Nginx TLS | Built-in / .NET 8.0 | NFR-001 (HIPAA), NFR-004 (RBAC + session), NFR-005 (TLS + AES-256), TR-005, TR-007, TR-008 |
| Deployment | Docker Compose (single-host) | 2.x | NFR-010 (free infrastructure, no cloud), TR-014 |
| Monitoring | Prometheus + Grafana | Prometheus 2.45+ / Grafana 10+ | NFR-002 (uptime monitoring), NFR-007 (AI accuracy dashboards), TR-015 |
| Logging | Seq (structured log aggregation) | 2023.4+ | NFR-006 (audit log accessibility), NFR-002 (operational log analysis), TR-016 |
| Documentation | Swagger / OpenAPI (Swashbuckle) | 6.5+ | API contract documentation for all REST endpoints; NFR-010 (free) |

### AI Component Stack

| Component | Technology | Purpose |
|-----------|------------|---------|
| Model Provider | Ollama 0.1.29+ serving Llama 3.1 8B (Q4_K_M quantization) | Local LLM inference for conversational intake, clinical extraction, and medical code suggestion — no PHI leaves deployment boundary |
| Vector Store | pgvector 0.5+ (PostgreSQL extension) | Embedding storage and cosine-similarity retrieval for RAG — clinical document chunks indexed per patient; eliminates separate vector database service |
| AI Gateway | .NET Ollama HTTP client (inline in API service) | Routes inference requests to Ollama endpoint; logs request metadata; enforces timeout (AIR-008); no external gateway required at Phase 1 scale |
| Guardrails | JSON schema output validation + application-layer Pending state enforcement | Validates LLM output structure before persistence; enforces human-in-the-loop state machine (AIR-007); prevents malformed AI responses from corrupting patient records |

### Alternative Technology Options

- **Angular 17 (Frontend alternative)**: Considered over React 18. Angular provides built-in dependency injection and opinionated structure suitable for enterprise apps. Rejected because: Angular's bundle size is larger (impacts NFR-008 performance), and React's AG Grid/TanStack Table ecosystem provides superior clinical data grid flexibility required for the 360° patient view.
- **Java Spring Boot 3.x (Backend alternative)**: Considered over .NET 8.0. Spring Security and Spring Data JPA provide mature enterprise patterns. Rejected because: (1) No built-in SignalR equivalent — WebSocket requires additional setup; (2) iText 7 PDF library is AGPL-licensed (commercial use requires paid license, violating NFR-010); (3) JVM warmup and higher memory footprint (~400 MB vs ~200 MB) increases Docker Compose resource requirements.
- **SQL Server Express (Database alternative)**: Considered over PostgreSQL. Rejected because: (1) No vector extension equivalent (eliminates RAG architecture option); (2) SQL Server Express has a 10 GB database size cap and no high-availability features; (3) Developer Edition is not licensed for production use; (4) pgcrypto provides column-level PHI encryption unavailable in SQL Server Express.
- **ChromaDB (Vector store alternative)**: Considered as standalone vector database for RAG. Rejected because: pgvector in PostgreSQL achieves the same capability without a separate Docker service; keeping embeddings in PostgreSQL maintains unified HIPAA access controls and encryption scope.
- **OpenAI GPT-4 / cloud LLM (AI model alternative)**: Considered for superior baseline clinical NLP performance. Rejected because: (1) Transmitting PHI to OpenAI violates HIPAA without a signed BAA and represents a data sovereignty risk; (2) Per-token costs violate NFR-010 (free infrastructure); (3) Local Llama 3.1 8B with RAG context achieves the required 98% AI-Human Agreement Rate target (NFR-007) for structured extraction tasks.
- **BioMistral 7B (Medical-specific AI model alternative)**: Considered for its medical fine-tuning. Rejected because: BioMistral has a 32K context window vs Llama 3.1 8B's 128K — insufficient for multi-document clinical extraction. Llama 3.1 8B's superior instruction following and context length are more critical than domain-specific fine-tuning for extraction use cases.

### Technology Decision

| Metric (from NFR/DR/AIR) | React 18 | Angular 17 | Rationale |
|--------------------------|----------|------------|-----------|
| NFR-011: SignalR client support | `@microsoft/signalr` — straightforward | Available via package | Tie |
| NFR-008: Complex data grid (360° view) | Excellent (AG Grid, TanStack Table) | Good (AG Grid available) | React wins |
| NFR-008: Bundle size / initial load | Smaller SPA bundle | Larger due to platform overhead | React wins |
| NFR-010: Free hosting (Netlify/Vercel) | Full support | Full support | Tie |
| TypeScript native | Via tsconfig | Native | Angular advantage |
| **Overall** | **Winner** | — | React 18 selected: superior clinical data grid ecosystem and bundle efficiency |

| Metric (from NFR/DR/AIR) | .NET 8.0 | Java Spring Boot 3.x | Rationale |
|--------------------------|----------|---------------------|-----------|
| NFR-011: Real-time (SignalR) | Built-in, zero config | Requires separate WebSocket impl | .NET wins |
| NFR-010: PDF generation (free, OSS) | QuestPDF (MIT license) | iText 7 (AGPL — commercial use needs paid license) | .NET wins |
| NFR-004: RBAC + JWT | ASP.NET Core Identity built-in | Spring Security (comparable) | Comparable |
| Docker memory footprint (NFR-008) | ~200 MB container | ~400 MB container | .NET wins |
| **Overall** | **Winner** | — | .NET 8.0 selected: SignalR + QuestPDF are decisive differentiators |

| Metric (from NFR/DR/AIR) | PostgreSQL 15+ | SQL Server Express | Rationale |
|--------------------------|----------------|-------------------|-----------|
| DR-006: Vector embeddings (RAG) | pgvector extension ✅ | No vector extension ❌ | PostgreSQL wins |
| DR-001: Column AES-256 encryption | pgcrypto ✅ | TDE (Enterprise only, not Express) ❌ | PostgreSQL wins |
| NFR-010: License / cost | Free, OSS | 10 GB limit, no HA, Dev edition not for production | PostgreSQL wins |
| DR-003: ACID transactions | Full ACID ✅ | Full ACID ✅ | Tie |
| **Overall** | **Winner** | — | PostgreSQL 15+ selected: pgvector + pgcrypto eliminate need for separate AI vector store and satisfy HIPAA encryption |

| Metric (from AIR) | Llama 3.1 8B | Mistral 7B v0.3 | Rationale |
|-------------------|-------------|-----------------|-----------|
| AIR-003: Context window (long clinical docs) | 128K tokens | 32K tokens | Llama 3.1 wins decisively |
| AIR-002: Instruction following | Excellent (Meta RLHF) | Very good | Llama 3.1 advantage |
| AIR-001: Local inference (RAM, Q4_K_M) | ~5 GB | ~4.5 GB | Comparable |
| AIR-006: Medical NLP capability | Strong | Good | Llama 3.1 advantage |
| **Overall** | **Winner** | — | Llama 3.1 8B selected: 128K context window is critical for multi-document clinical extraction; superior instruction following improves extraction reliability |

---

## Technical Requirements

- TR-001: [SOURCE:INPUT] System MUST implement the patient-facing, staff-facing, and admin-facing UI as a React 18 (TypeScript) single-page application, justified by NFR-008 (complex data grid performance), NFR-011 (SignalR client for real-time queue), and NFR-010 (deployable on Netlify/Vercel free tier without server-side rendering infrastructure).
  Basis: NFR-008, NFR-011, NFR-010; selected from BRD-permitted options (React or Angular) based on constraint analysis.

- TR-002: [SOURCE:INPUT] System MUST implement all API endpoints as an ASP.NET Core Web API using C# on .NET 8.0, justified by NFR-004 (ASP.NET Core Identity for RBAC + JWT), NFR-011 (SignalR hub built-in), and NFR-010 (free Docker runtime with lower memory footprint than JVM alternatives).
  Basis: NFR-004, NFR-011, NFR-010; selected from BRD-permitted options (.NET or Java) based on constraint analysis.

- TR-003: [SOURCE:INPUT] System MUST use PostgreSQL 15.3+ as the primary database with the pgcrypto extension for AES-256 column-level PHI encryption (DR-001) and the pgvector 0.5+ extension for clinical document embedding storage (DR-006), justified by NFR-005 and NFR-010 (free, OSS; no SQL Server licensing).
  Basis: DR-001, DR-006, NFR-005, NFR-010; selected from BRD-permitted options (PostgreSQL or SQL Server).

- TR-004: [SOURCE:INPUT] System MUST run all AI inference via Ollama 0.1.29+ serving the Llama 3.1 8B model at Q4_K_M quantization on the local Docker Compose host, justified by AIR-001 (no PHI to external APIs), AIR-002/003 (128K context for conversational and extraction tasks), and NFR-010 (free, OSS).
  Basis: AIR-001, AIR-002, AIR-003, NFR-010.

- TR-005: [SOURCE:INPUT] System MUST implement authentication using JWT Bearer tokens issued by ASP.NET Core Identity, with token expiry set to 15 minutes (NFR-004), refresh token rotation, and role claims enforced on every protected endpoint.
  Basis: NFR-004 (15-min session timeout, RBAC), NFR-001 (HIPAA authentication requirement), FR-003 (multi-role login routing).

- TR-006: [SOURCE:INPUT] System MUST implement staff queue real-time updates using ASP.NET Core SignalR, broadcasting walk-in creation and patient arrival events to all connected staff clients within 5 seconds of the triggering database write.
  Basis: NFR-011, FR-028; SignalR eliminates a separate message broker at Phase 1 scale.

- TR-007: [SOURCE:INPUT] System MUST encrypt all PHI columns in PostgreSQL using the pgcrypto extension's `pgp_sym_encrypt` function with AES-256, with the encryption key injected via environment variable at container startup and never stored in the codebase or committed to version control.
  Basis: DR-001, NFR-005, HIPAA §164.312(a)(2)(iv); FR-045 explicitly mandates AES-256 at rest.

- TR-008: [SOURCE:INPUT] System MUST enforce HTTPS-only access by terminating TLS 1.2+ at an Nginx reverse proxy container, with HTTP-to-HTTPS redirect configured at the Nginx level; the .NET API MUST NOT accept unencrypted HTTP connections from outside the Docker network.
  Basis: NFR-005, FR-045, HIPAA §164.312(e)(2)(ii) transmission security.

- TR-009: [SOURCE:INPUT] System MUST generate appointment confirmation and swap notification PDFs using QuestPDF (MIT license, .NET library) producing the confirmation document server-side before attaching to outbound email.
  Basis: FR-013, FR-019, NFR-010 (MIT license satisfies free/OSS requirement).

- TR-010: [SOURCE:INPUT] System MUST pre-process uploaded clinical documents using PdfPig (.NET, Apache 2.0 license) to extract raw text before passing content to the Ollama AI extraction pipeline, maintaining a chunked text representation per document for RAG embedding.
  Basis: AIR-003 (clinical document parsing precondition), AIR-004 (RAG chunking step), NFR-010 (Apache 2.0 license satisfies free/OSS requirement).

- TR-011: [SOURCE:INPUT] System MUST deliver email notifications (appointment confirmations, reminders, swap notifications, account credentials) via a configurable SMTP provider — Mailpit for local/development environments and a production SMTP endpoint injected via environment variable.
  Basis: FR-013, FR-019, FR-020, NFR-010 (SMTP is free; configurable provider avoids hard-coded vendor dependency).

- TR-012: [SOURCE:INPUT] System MUST deliver SMS appointment reminders and swap notifications via a configurable SMS provider (Twilio free trial tier by default) with the provider API key injected via environment variable.
  Basis: FR-019, FR-020, NFR-010 (Twilio free trial satisfies zero-cost constraint for Phase 1).

- TR-013: [SOURCE:INPUT] System MUST integrate with Google Calendar API (OAuth 2.0, free tier) and Microsoft Graph Calendar API (OAuth 2.0, free tier) to create and update calendar events upon booking confirmation, swap, or cancellation, with OAuth consent tokens stored per patient.
  Basis: FR-021, FR-022; OAuth 2.0 consent model required by both APIs; free tier satisfies NFR-010.

- TR-014: [SOURCE:INPUT] System MUST be orchestrated as a single Docker Compose 2.x stack comprising: Nginx (reverse proxy + TLS), React SPA (static files served by Nginx), .NET API, PostgreSQL (with pgvector + pgcrypto), Ollama (AI inference), Prometheus, Grafana, and Seq — with health checks, restart policies, and named volumes for all stateful services.
  Basis: NFR-010 (single Docker Compose stack, no paid cloud), NFR-002 (container restart policies for availability).

- TR-015: [SOURCE:INPUT] System MUST expose application metrics (request latency, error rates, AI processing duration, queue depth) to Prometheus 2.45+ via a `/metrics` endpoint and display them in Grafana 10+ dashboards to support NFR-002 (99.9% availability monitoring) and NFR-007 (AI accuracy tracking).
  Basis: NFR-002, NFR-007; Prometheus + Grafana are free, Docker-native, and standard for container monitoring.

- TR-016: [SOURCE:INPUT] System MUST emit all structured application logs (including audit log entries, authentication events, and AI processing events) to Seq 2023.4+ via Serilog sink, enabling queryable structured log search for HIPAA audit access and operational diagnostics.
  Basis: NFR-006 (immutable audit log accessibility), NFR-002 (operational log analysis); Seq single-instance tier is free.

- TR-017: [SOURCE:INPUT] System MUST apply ASP.NET Core built-in rate limiting middleware to all authentication endpoints (`/auth/login`, `/auth/register`) with progressive lockout after 5 consecutive failed attempts from the same IP within a 15-minute window.
  Basis: NFR-004, UC-004 "progressive rate limiting after repeated failures"; OWASP A07 Identification and Authentication Failures.

- TR-018: [SOURCE:INPUT] System MUST validate all uploaded clinical documents on the server side before acceptance: MIME type MUST match the declared Content-Type header, file size MUST not exceed a configurable maximum (default 20 MB), and a SHA-256 hash MUST be computed on arrival for integrity verification and duplicate detection.
  Basis: FR-030 "validate all uploaded files against an allowed file-type list and a maximum file-size threshold"; OWASP A04 Insecure Design (unrestricted file upload prevention).

---

## Technical Constraints & Assumptions

**Technical Constraints:**

1. **Free infrastructure only**: No paid cloud services (AWS, Azure, GCP), paid API keys, or commercial software licenses are permitted for any platform component. All services must run on free/OSS tooling (BRD Section 4).
2. **Approved technology options**: Frontend must be React or Angular; backend must be .NET or Java; database must be PostgreSQL or SQL Server (BRD Section 4). Selections made: React 18, .NET 8.0, PostgreSQL 15+.
3. **HIPAA non-negotiable**: Every component that touches PHI must comply with HIPAA Security Rule 45 CFR Part 164; compliance cannot be deferred to a future phase.
4. **Phase 1 scope boundary**: No payment gateway, no EHR integration, no provider logins, no family member profiles, no patient self-check-in (QR/mobile), no multi-tenancy in Phase 1.
5. **Free API integrations only**: Google Calendar API and Microsoft Graph Calendar API are consumed on free tiers only; no paid tier features may be relied upon.
6. **CPU-only AI inference**: Ollama + Llama 3.1 8B must run on CPU-only hardware (no GPU assumed); the Q4_K_M quantization level is selected to fit within 8 GB available RAM.

**Assumptions:**

1. **Single-host deployment**: The entire Docker Compose stack runs on a single host machine. No horizontal scaling or Kubernetes orchestration is required for Phase 1.
2. **Single-tenant, single-organization**: The platform serves one healthcare organization; no multi-tenancy or organization partitioning is required.
3. **English-only content**: No internationalization (i18n) is required for Phase 1.
4. **Staff accounts are admin-provisioned**: Staff users cannot self-register; accounts are created by an admin via the User Management interface (FR-006).
5. **Appointment slots are admin-configured**: Available appointment slots are pre-created by admin; the system does not dynamically generate slots from provider schedules (provider schedules are out of Phase 1 scope).
6. **Insurance pre-check uses seeded internal data**: The insurance validation in FR-039 runs against a seeded internal dataset of dummy records; no live insurance API integration exists in Phase 1.
7. **ICD-10-CM and CPT code database is embedded**: ICD-10 and CPT code catalogs are seeded into the database at deployment; no live external coding API is called.
8. **Calendar sync is optional and non-blocking**: A failure to sync to Google Calendar or Outlook does not block booking confirmation; the booking succeeds and sync failure is logged and retried.
9. **Email/SMS delivery is best-effort with retry**: Notification delivery failure after 3 retries is logged and visible in Seq but does not roll back the triggering operation (booking, swap, account creation).
10. **Host machine has ≥8 GB RAM available for Ollama**: Llama 3.1 8B at Q4_K_M quantization requires approximately 5 GB RAM; 8 GB minimum ensures headroom for concurrent .NET API and PostgreSQL operations.
