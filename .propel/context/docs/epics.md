# Epic - Unified Patient Access & Clinical Intelligence Platform

## Epic Summary Table

| Epic ID | Epic Title | Mapped Requirement IDs |
|---------|------------|------------------------|
| EP-TECH | Infrastructure & Platform Bootstrap | TR-001, TR-002, TR-008, TR-014, TR-015, TR-016, NFR-002, NFR-010 |
| EP-DATA | Data Layer, Schema & PHI Encryption | TR-003, TR-007, DR-001, DR-002, DR-003, DR-004, DR-005, DR-006, DR-007, DR-008 |
| EP-001 | Authentication, RBAC & User Management | FR-001, FR-002, FR-003, FR-004, FR-005, FR-006, NFR-004, TR-005, TR-017 |
| EP-002 | HIPAA Compliance, Security & Audit | FR-043, FR-044, FR-045, NFR-001, NFR-005, NFR-006 |
| EP-003 | Patient Intake (AI Conversational & Manual) | FR-007, FR-008, FR-009, FR-010, AIR-002 |
| EP-004 | Appointment Booking, Slots & Insurance Pre-check | FR-011, FR-012, FR-013, FR-014, FR-015, FR-039, NFR-003, NFR-009, TR-009 |
| EP-005 | Preferred Slot Swap, Notifications & Calendar Sync | FR-016, FR-017, FR-018, FR-019, FR-020, FR-021, FR-022, FR-023, TR-011, TR-012, TR-013 |
| EP-006 | Staff Queue, Walk-in Management & Dashboards | FR-024, FR-025, FR-026, FR-027, FR-028, FR-040, FR-041, FR-042, NFR-008, NFR-011, TR-006 |
| EP-007-I | Clinical AI — Document Upload & Extraction | FR-029, FR-030, FR-031, FR-032, FR-033, AIR-001, AIR-003, AIR-004, AIR-008, TR-004, TR-010, TR-018 |
| EP-007-II | Clinical AI — 360° View, Conflicts & Medical Coding | FR-034, FR-035, FR-036, FR-037, FR-038, AIR-005, AIR-006, AIR-007, NFR-007 |

---

## Epic Description

### EP-TECH: Infrastructure & Platform Bootstrap

**Business Value**: Establishes the entire deployable platform foundation — Docker Compose stack, Nginx TLS termination, React SPA scaffolding, .NET API scaffold, Prometheus/Grafana observability, and Seq structured logging. Without this epic, no feature epic can be developed, tested, or deployed. Directly satisfies the free-infrastructure constraint (NFR-010) and underpins the 99.9% availability target (NFR-002).

**Description**: Green-field infrastructure bootstrap. Sets up the full Docker Compose 2.x service topology (Nginx, API, DB, Ollama, Prometheus, Grafana, Seq), configures TLS 1.2+ termination at Nginx, initialises the React 18 TypeScript SPA project with routing skeletons, bootstraps the ASP.NET Core .NET 8.0 Web API project with middleware pipeline, and wires Prometheus `/metrics` endpoint and Grafana dashboards for operational monitoring. Seq Serilog sink is configured in the API. All services launch as a single `docker compose up` command. CI/CD skeleton (lint, build, test) is included.

**UI Impact**: No

**Screen References**: N/A

**Key Deliverables**:
- `docker-compose.yml` with all 8 services: nginx, api, db, ollama, prometheus, grafana, seq
- Nginx configuration: TLS 1.2+ termination, HTTP→HTTPS redirect, SPA static file serving, `/api/*` reverse proxy
- React 18 TypeScript SPA project scaffold: routing shell, auth context placeholder, role-based redirect skeleton
- ASP.NET Core .NET 8.0 Web API project scaffold: middleware pipeline, health check endpoint, `/metrics` Prometheus endpoint
- Prometheus `prometheus.yml` scrape config targeting `api:8080/metrics`
- Grafana datasource provisioning + base dashboard (request latency, error rate, uptime)
- Seq Serilog sink wired from .NET API with structured event schema
- Docker named volumes: `db_data`, `ollama_models`, `seq_data`, `prometheus_data`, `grafana_data`
- Health checks and `restart: always` policies on all stateful services
- CI/CD skeleton: `.github/workflows/ci.yml` (lint, build, docker-compose up --wait, smoke test)

**Dependent EPICs**:
- None

---

### EP-DATA: Data Layer, Schema & PHI Encryption

**Business Value**: Provides the entire database schema, encryption infrastructure, and data integrity guarantees that every feature epic depends on. Implements AES-256 column-level PHI encryption (pgcrypto), vector embedding storage for AI (pgvector), ACID booking transactions, and 6-year HIPAA data retention policy. Failure to deliver this epic before feature epics will result in unencrypted PHI, broken AI pipelines, and booking race conditions.

**Description**: Provisions PostgreSQL 15.3+ with pgcrypto and pgvector 0.5+ extensions. Designs and migrates all 12 domain entities (User, Patient, IntakeRecord, AppointmentSlot, Booking, ClinicalDocument, ExtractedRecord, ChunkEmbedding, MedicalCodeSuggestion, AuditLog, ReminderSchedule, InsuranceRecord) with correct column types, FK constraints, and nullable semantics. Configures pgcrypto `pgp_sym_encrypt` on all PHI columns. Implements AuditLog as INSERT-only (no UPDATE/DELETE privilege at DB level). Sets up pgvector ivfflat cosine index on ChunkEmbedding.embedding. Applies EF Core migrations. Encryption key injected via Docker environment variable — never committed to source control.

**UI Impact**: No

**Screen References**: N/A

**Key Deliverables**:
- EF Core migration files for all 12 domain entities
- pgcrypto extension provisioning and AES-256 column-level encryption wrappers for all PHI fields (Patient demographics, IntakeRecord.intakeData, ExtractedRecord.entityValue, ChunkEmbedding.chunkText, ClinicalDocument.storagePath)
- pgvector 0.5+ extension provisioning and `ivfflat` cosine index on `chunk_embeddings.embedding` (dim=1536, lists=100)
- AuditLog table with INSERT-only PostgreSQL role (REVOKE UPDATE, DELETE on audit_log FROM app_user)
- ACID transaction enforcement via EF Core explicit transactions on all Booking state transitions (DR-003)
- ReminderSchedule table with delivery status and attempt count columns
- InsuranceRecord table with provider name and ID pattern columns
- Docker Compose environment variable injection for `PHI_ENCRYPTION_KEY`
- Database seed script: admin user, InsuranceRecord dummy dataset for pre-check (FR-039)
- 6-year retention policy documented in database README (DR-005)

**Dependent EPICs**:
- EP-TECH - Foundational - Requires PostgreSQL container and Docker Compose infrastructure

---

### EP-001: Authentication, RBAC & User Management

**Business Value**: The gateway to the entire platform. Every user — Patient, Staff, or Admin — must authenticate before accessing any protected resource. This epic delivers multi-role login, JWT session management, progressive rate limiting, and admin CRUD for user accounts. Without it, no feature epic has a verified identity context.

**Description**: Implements ASP.NET Core Identity with JWT Bearer token issuance (15-minute expiry, refresh token rotation). Builds patient self-registration and staff-assisted walk-in account creation flows. Enforces role-based access control (RBAC) on every API endpoint via `[Authorize(Roles = ...)]` attributes. Progressive rate limiting after 5 consecutive failed login attempts from the same IP (TR-017). Admin user management UI (CRUD on user accounts and role assignments). Role-appropriate post-login redirect in the React SPA (Patient → intake/booking, Staff → queue dashboard, Admin → admin console).

**UI Impact**: Yes

**Screen References**: N/A (figma_spec not available)

**Key Deliverables**:
- ASP.NET Core Identity schema (AspNetUsers, AspNetRoles, AspNetUserRoles) included in EP-DATA migrations
- JWT Bearer token issuance: `POST /auth/register`, `POST /auth/login`, `POST /auth/refresh`
- Bcrypt password hashing on registration
- RBAC middleware enforced on all protected endpoints (Patient/Staff/Admin role claims)
- Session invalidation at 15-minute inactivity: client-side timer + server-side JWT expiry
- Progressive rate limiting: `POST /auth/login`, `POST /auth/register` — 5 attempts / 15 min window → 429
- React SPA: `/login`, `/register` pages with form validation
- React SPA: Role-based post-login redirect (Patient → `/intake`, Staff → `/queue`, Admin → `/admin`)
- Admin Console: user list table, create user modal, edit user modal, deactivate user action, role assignment dropdown
- Walk-in account creation flow: staff-facing form with optional "Create Account" toggle

**Dependent EPICs**:
- EP-TECH - Foundational - Requires .NET API scaffold and React SPA project

---

### EP-002: HIPAA Compliance, Security & Audit

**Business Value**: Non-negotiable regulatory requirement under 45 CFR Part 164. Non-compliance exposes the organisation to civil and criminal penalties. This epic delivers the full HIPAA Security Rule implementation: PHI access controls, immutable audit trail, AES-256 encryption, TLS 1.2+ enforcement, and role-based access auditing. Directly supports all FR-043/044/045 and NFR-001/005/006 contractual obligations.

**Description**: Implements the immutable audit log (INSERT-only AuditLog table, INSERT privilege only on app_user role) capturing: actor identity, actor role, action type, resource type, resource ID, IP address, user agent, UTC timestamp. Serilog structured events emitted to Seq for every sensitive action (login success/failure, patient data access, booking actions, document upload, code review, admin operations). Enforces TLS 1.2+ at Nginx (from EP-TECH). PHI field encryption via pgcrypto (from EP-DATA). AuditLog middleware wired as a cross-cutting concern in the .NET API pipeline — fires on every sensitive endpoint without per-controller annotation.

**UI Impact**: No

**Screen References**: N/A

**Key Deliverables**:
- AuditLog middleware service: `IAuditLogger` interface + `PostgresAuditLogger` implementation (INSERT-only)
- Audit events for: login success, login failure, patient registration, patient data access (360° view), booking create/modify, document upload, code suggestion accept/reject, admin user CRUD, role change, unauthorized access attempt
- Serilog structured sink to Seq (all audit events + operational logs)
- Nginx TLS 1.2+ cipher suite configuration (`ssl_protocols TLSv1.2 TLSv1.3; ssl_ciphers HIGH:!aNULL:!MD5`)
- PHI encryption key rotation guide (documented procedure, not automated)
- HIPAA compliance checklist document: access controls (§164.312(a)), audit controls (§164.312(b)), integrity controls (§164.312(c)), transmission security (§164.312(e))
- Rate-limiting audit events integrated with progressive lockout from EP-001
- `/admin/audit-logs` API endpoint (Admin role only, paginated, read-only)

**Dependent EPICs**:
- EP-DATA - Foundational - AuditLog table provisioned in data layer (DR-002: append-only)

---

### EP-003: Patient Intake (AI Conversational & Manual)

**Business Value**: The first clinical touchpoint for every patient. AI-assisted conversational intake reduces front-desk workload and improves data quality by guiding patients through structured health history collection. Manual fallback ensures 100% completion rate regardless of AI availability. Together, they capture the primary source of clinical data for the platform.

**Description**: Implements a dual-mode intake system: (1) AI Conversational mode — multi-turn Ollama Llama 3.1 8B dialogue collecting patient demographics, medical history, medications, allergies, and chief complaint, producing a structured JSON summary reviewed by the patient before save; (2) Manual Form mode — multi-section form as a complete standalone alternative. Seamless mid-session mode switching preserves all entered data. Intake data stored as AES-256-encrypted JSONB in IntakeRecord. AI inference executes entirely locally via Ollama (AIR-001 — no PHI to external APIs).

**UI Impact**: Yes

**Screen References**: N/A (figma_spec not available)

**Key Deliverables**:
- `POST /intake/ai/start` — initiates Ollama chat session with intake system prompt
- `POST /intake/ai/message` — processes multi-turn dialogue turn
- `POST /intake/ai/confirm` — saves AI-collected summary as IntakeRecord [Complete, encrypted JSONB]
- `POST /intake/manual` — saves manually entered form as IntakeRecord [Manual, Complete, encrypted JSONB]
- `POST /intake/mode-switch` — maps partial data from AI to Manual fields (and reverse) without data loss
- `PATCH /intake/ai/field` — in-place field correction on AI summary before confirmation
- `POST /intake/draft` — auto-saves draft on navigation-away (status=Draft, partial encrypted data)
- React SPA intake wizard: AI chat interface, manual form with sections (demographics, history, meds, allergies, complaint), mode-switch control, summary review screen
- Intake system prompt (Ollama): structured to elicit all required fields in conversational sequence
- IntakeRecord encryption: all JSONB data encrypted via pgcrypto before persistence

**Dependent EPICs**:
- EP-DATA - Foundational - Requires IntakeRecord entity and pgcrypto JSONB encryption

---

### EP-004: Appointment Booking, Slots & Insurance Pre-check

**Business Value**: Core business transaction — the appointment booking. Delivering this epic directly enables patient access to care, generates the primary operational data for staff, and drives PDF confirmation revenue (QuestPDF). The no-show risk score gives staff actionable intelligence to intervene proactively, reducing empty slots. Insurance pre-check (soft validation) reduces admin rework.

**Description**: Implements the full appointment booking lifecycle: display available slots → patient selects slot → insurance soft-validation → ACID booking transaction (SELECT FOR UPDATE → INSERT Booking → UPDATE Slot status → compute risk score → COMMIT) → trigger PDF confirmation email. Concurrent booking conflict handled via SELECT FOR UPDATE with ROLLBACK + 409 response. No-show risk score computed from booking lead time, prior no-show history, and booking channel. PDF appointment confirmation generated server-side by QuestPDF and dispatched via SMTP (async, 3-retry exponential back-off per NFR-009).

**UI Impact**: Yes

**Screen References**: N/A (figma_spec not available)

**Key Deliverables**:
- `GET /slots?available=true` — paginated available slot listing
- `POST /bookings` — ACID booking transaction with SELECT FOR UPDATE, duplicate check (FR-015), and no-show risk score computation (FR-014, DR-008)
- `POST /insurance/validate` — soft insurance pre-check against InsuranceRecord seed data (FR-039, non-blocking)
- QuestPDF appointment confirmation PDF: patient name, appointment date/time, location, booking reference
- `POST /notifications/confirmation` — async SMTP email with PDF attachment (3-retry exponential back-off, TR-009, TR-011)
- ReminderSchedule INSERT on booking confirmation (seeds reminder jobs)
- No-show risk scoring algorithm: rule-based scoring (lead time, prior no-show count, booking channel) stored in Booking.noShowRiskScore + riskFactors JSONB (DR-008)
- React SPA booking flow: slot calendar view, insurance input fields with soft-validation feedback, booking confirmation screen
- 409 conflict response with alternative slot suggestions when booked concurrently

**Dependent EPICs**:
- EP-DATA - Foundational - Requires AppointmentSlot, Booking, InsuranceRecord, ReminderSchedule entities and ACID transaction infrastructure

---

### EP-005: Preferred Slot Swap, Notifications & Calendar Sync

**Business Value**: Maximises slot utilisation by automatically filling cancellation gaps with patients who designated a preference — a key differentiator vs. basic booking systems. Automated reminders reduce no-show rates (directly improving the NFR-007 no-show KPI). Calendar sync increases patient engagement and reduces missed appointments. All notifications implement retry logic per NFR-009 to ensure delivery reliability.

**Description**: Implements the preferred-slot monitor (background job polling for freed slots → atomic swap transaction → new PDF + email/SMS notification). Automated reminder scheduler (SMS + email on configurable schedule before appointment, opt-out honoured). Google Calendar OAuth 2.0 sync and Outlook Calendar (Microsoft Graph) OAuth 2.0 sync on booking confirmation — failures are non-blocking (booking not rolled back). Notification delivery via configurable SMTP (Mailpit/production) and Twilio SMS free-tier, with 3-retry exponential back-off.

**UI Impact**: Yes

**Screen References**: N/A (figma_spec not available)

**Key Deliverables**:
- `POST /bookings` extended: `preferredSlotId` field accepted, `SlotMonitor` record inserted
- Slot Monitor background job: polls for active monitors against newly freed slots; executes ACID swap transaction (UPDATE Booking slotId, UPDATE old Slot → Available, UPDATE new Slot → Booked, UPDATE SlotMonitor active=false); triggers swap notifications
- QuestPDF swap notification PDF: updated appointment details
- Swap email + SMS notification with 3-retry exponential back-off (TR-011, TR-012, NFR-009)
- Reminder scheduler background job: processes due ReminderSchedule entries; sends SMS (Twilio) + email (SMTP); honours per-patient opt-out flags; 3-retry back-off on delivery failure
- `POST /calendar/google/sync` — OAuth 2.0 Google Calendar event create (non-blocking on failure)
- `POST /calendar/outlook/sync` — OAuth 2.0 Microsoft Graph event create (non-blocking on failure)
- `POST /auth/google/callback`, `POST /auth/microsoft/callback` — OAuth token exchange and encrypted token storage
- `POST /bookings/{id}/preferred-slot` (DELETE) — cancel preferred slot designation
- React SPA: preferred slot selector in booking flow, calendar sync buttons post-booking, reminder opt-out settings in patient profile

**Dependent EPICs**:
- EP-TECH - Foundational - Requires Docker Compose infrastructure, SMTP + SMS + Calendar API integrations wired in environment variables

---

### EP-006: Staff Queue, Walk-in Management & Dashboards

**Business Value**: Delivers the real-time operational command centre for staff. Walk-in registration directly addresses same-day access to care. The live queue dashboard with SignalR push eliminates the need for manual refresh, supporting the 5-second update requirement (NFR-011). The admin KPI dashboard tracks platform health metrics (no-show rate, AI agreement rate, critical conflicts) enabling data-driven management decisions. The no-show risk score visible to staff enables proactive interventions.

**Description**: Implements the staff-only walk-in booking flow (with optional patient account creation), the real-time queue dashboard powered by ASP.NET Core SignalR (NFR-011 — 5s push), and the daily operations dashboard (scheduled appointments + walk-ins + arrival status + no-show risk scores). Admin console provides user management CRUD (linked to EP-001 user endpoints) and KPI metrics aggregation dashboard (FR-042: total patients, total bookings, no-show rate, AI-Human Agreement Rate, critical conflicts). Walk-in creation and patient-arrived events broadcast to all connected staff clients via SignalR hub.

**UI Impact**: Yes

**Screen References**: N/A (figma_spec not available)

**Key Deliverables**:
- `POST /walkins` — staff-only walk-in booking creation (JWT Staff role enforced), optional patient account creation
- `PATCH /bookings/{id}/status` — staff-only "Mark Arrived" action (JWT Staff/Admin enforced; Patient role → 403)
- `GET /dashboard/staff` — daily appointments + walk-ins + queue ordered by arrival sequence + no-show risk scores
- `GET /admin/metrics` — KPI aggregation: COUNT(Patient), COUNT(Booking), AVG(noShowRate), aiHumanAgreementRate, COUNT(criticalConflicts)
- `GET /admin/users` (linked to EP-001 admin endpoints)
- ASP.NET Core SignalR Hub `/hubs/queue`: `BroadcastQueueUpdate` method broadcasting walk-in creation and arrival status changes to all connected Staff clients
- SignalR broadcast latency target: < 5 seconds from triggering DB write (NFR-011)
- React SPA Staff Dashboard: live queue table (auto-updates via SignalR), "Mark Arrived" action button, no-show risk score badge, "New Walk-in" form
- React SPA Admin Console: KPI metrics cards, user management table (CRUD via EP-001 endpoints)
- RBAC enforcement: `[Authorize(Roles = "Staff,Admin")]` on all walk-in and queue endpoints

**Dependent EPICs**:
- EP-TECH - Foundational - Requires SignalR hub infrastructure in .NET API scaffold and React SPA `@microsoft/signalr` client

---

### EP-007-I: Clinical AI — Document Upload & Extraction

**Business Value**: Unlocks the core AI differentiator of the platform: automated extraction of structured clinical data from uploaded patient documents. This eliminates hours of manual data entry per patient, reduces transcription errors, and builds the vector knowledge base that powers 360° views and medical coding. Delivering this epic first establishes the entire RAG infrastructure (pgvector, Ollama, PdfPig pipeline) that EP-007-II depends on.

**Description**: Implements the full clinical document upload and AI extraction pipeline: server-side file validation (MIME type, file size ≤20 MB, SHA-256 hash, TR-018), encrypted document storage (storagePath encrypted via pgcrypto), background extraction job (PdfPig text extraction → 500-token sliding window chunking with 50-token overlap → Ollama embedding → pgvector INSERT → Ollama entity extraction prompt → JSON schema validation → ExtractedRecord INSERT [Pending, encrypted]), de-duplication of repeated entities across multiple documents. All AI inference runs locally via Ollama (AIR-001 — no PHI leaves the deployment). Processing must complete within 120 seconds per document (AIR-008).

**UI Impact**: Yes

**Screen References**: N/A (figma_spec not available)

**Key Deliverables**:
- `POST /documents` — multipart upload: MIME validation (PDF, DOC, DOCX, JPG, PNG), size ≤ 20 MB, SHA-256 hash, encrypted storagePath stored, ClinicalDocument INSERT [Pending] (TR-018)
- PdfPig text extraction service: raw text per page, concatenated, passed to chunker
- Text chunker: 500-token sliding windows with 50-token overlap, sentence-boundary preservation
- Ollama embedding call: `POST /api/embeddings { text: chunk }` → float[1536] vector stored in ChunkEmbedding (pgvector)
- `ivfflat` cosine index query: retrieval of TOP-K chunks for RAG inference
- Ollama entity extraction prompt: structured prompt producing JSON `[{ entityType, value, confidence, chunkRef }]` per chunk batch (AIR-003)
- JSON schema validator: validates entity extraction output; low-confidence entities flagged; schema failures recorded with status=Failed
- ExtractedRecord INSERT: encrypted entityValue, confidence score, chunkRef, status=Pending (DR-004 — with patient ID, doc ID, processing status)
- De-duplication: group ExtractedRecords by (entityType + normalised value); soft-delete duplicates (isDuplicate=true)
- ClinicalDocument status state machine: Pending → Extracting → Complete | Failed
- 120-second processing timeout per document with status=Failed on breach (AIR-008)
- React SPA document upload widget: drag-and-drop, file type/size validation, upload progress, processing status polling

**Dependent EPICs**:
- EP-DATA - Foundational - Requires ClinicalDocument, ExtractedRecord, ChunkEmbedding entities; pgvector index; Ollama container in Docker Compose

---

### EP-007-II: Clinical AI — 360° View, Conflict Detection & Medical Coding

**Business Value**: The highest-visibility AI output: staff see a complete, de-duplicated, source-traced clinical picture of every patient in one view. Conflict detection prevents dangerous care decisions based on contradictory records. Medical code suggestions (ICD-10 + CPT) reduce coding time and improve billing accuracy. Human-in-the-loop (AIR-007) ensures no AI output is ever finalized without explicit staff review, satisfying both clinical safety requirements and the 98% AI-Human Agreement Rate KPI (NFR-007).

**Description**: Implements the RAG-based medical code suggestion pipeline (query embedding → pgvector cosine similarity retrieval → Ollama ICD-10/CPT code generation prompt → JSON schema validation → MedicalCodeSuggestion INSERT [Pending] — AIR-006, DR-007). Conflict detection: rule-based grouping of ExtractedRecords by entityType + value, followed by Ollama analysis of conflicting pairs → ConflictFlag INSERT with confidence and source references (AIR-005). 360° Patient View aggregates Patient, IntakeRecord, ExtractedRecords, MedicalCodeSuggestions, and ConflictFlags in a single authenticated staff-only GET endpoint. Human-in-loop enforced: all suggestions remain Pending until staff explicitly accepts or rejects (AIR-007, FR-038). No auto-finalization exists at any layer.

**UI Impact**: Yes

**Screen References**: N/A (figma_spec not available)

**Key Deliverables**:
- `GET /patients/{id}/view` — staff-only 360° aggregated patient view: demographics + intake + extracted entities (with source links) + conflict flags + pending code suggestions (FR-035)
- Conflict detection job: rule-based grouping → Ollama conflict analysis prompt → ConflictFlag INSERT (FR-034, AIR-005)
- `PATCH /conflicts/{id}/resolve` — staff resolves conflict by choosing authoritative value (FR-034)
- RAG code suggestion job: clinical summary embedding → pgvector TOP-20 retrieval → Ollama ICD-10/CPT prompt → JSON validation (ICD-10 format: `[A-Z][0-9]{2}\.?[0-9]{0,4}`, CPT: 5 digits, confidence > 0.0) → MedicalCodeSuggestion INSERT [Pending] (FR-036, FR-037, AIR-006, DR-007)
- `GET /patients/{id}/codes?status=Pending` — pending code suggestions with source chunk references
- `PATCH /suggestions/{id}` — staff accept / reject / correct code suggestion (FR-038, AIR-007); INSERT corrected suggestion + mark original Rejected
- AI-Human Agreement Rate tracking: compute `COUNT(Accepted without correction) / COUNT(reviewed)` — surfaced in admin KPI via EP-006
- ConflictFlag data model: links to two ExtractedRecord IDs, confidence, explanation, resolution status, resolvedBy, resolvedAt
- React SPA 360° patient view: vitals/meds/diagnoses with source document links, conflict banners with resolve action, pending code suggestions table with accept/reject/correct controls
- Low-confidence extraction indicators in 360° view (confidence threshold display for staff awareness)

**Dependent EPICs**:
- EP-007-I - Decomposed - Part II of Clinical AI epic; requires ChunkEmbedding vector store, ExtractedRecord table, and RAG infrastructure from EP-007-I
