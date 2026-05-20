# Development Task Plan — Unified Patient Access & Clinical Intelligence Platform

## Task Index

| Task ID | Title | Epic | Story | Type | Priority | Estimate | Status |
|---------|-------|------|-------|------|----------|----------|--------|
| TASK-001 | Docker Compose stack scaffold (all 8 services) | EP-TECH | — | Infra | P0 | 3d | Not Started |
| TASK-002 | Nginx TLS 1.2+ config & HTTP→HTTPS redirect | EP-TECH | — | Infra | P0 | 0.5d | Not Started |
| TASK-003 | React 18 TypeScript SPA project scaffold | EP-TECH | — | Frontend | P0 | 1d | Not Started |
| TASK-004 | .NET 8.0 ASP.NET Core Web API scaffold | EP-TECH | — | Backend | P0 | 1d | Not Started |
| TASK-005 | Prometheus + Grafana observability wiring | EP-TECH | — | Infra | P1 | 1d | Not Started |
| TASK-006 | Seq + Serilog structured logging setup | EP-TECH | — | Infra | P1 | 0.5d | Not Started |
| TASK-007 | GitHub Actions CI/CD skeleton | EP-TECH | — | Infra | P1 | 1d | Not Started |
| TASK-008 | PostgreSQL + pgcrypto + pgvector provisioning | EP-DATA | — | DB | P0 | 1d | Not Started |
| TASK-009 | EF Core migrations — all 12 domain entities | EP-DATA | — | DB | P0 | 2d | Not Started |
| TASK-010 | pgcrypto AES-256 column-level PHI encryption | EP-DATA | — | DB | P0 | 1.5d | Not Started |
| TASK-011 | pgvector ivfflat cosine index on ChunkEmbedding | EP-DATA | — | DB | P0 | 0.5d | Not Started |
| TASK-012 | AuditLog INSERT-only DB grant + append-only constraint | EP-DATA | — | DB | P0 | 0.5d | Not Started |
| TASK-013 | Database seed script (admin user + InsuranceRecord data) | EP-DATA | — | DB | P1 | 0.5d | Not Started |
| TASK-014 | Patient self-registration API endpoint | EP-001 | US-001 | Backend | P0 | 1d | Not Started |
| TASK-015 | JWT auth: login, refresh, token expiry (15 min) | EP-001 | US-003 | Backend | P0 | 1.5d | Not Started |
| TASK-016 | RBAC middleware — role enforcement on all endpoints | EP-001 | US-003 | Backend | P0 | 1d | Not Started |
| TASK-017 | Progressive rate limiting on /auth endpoints (5/15min) | EP-001 | US-004 | Backend | P0 | 0.5d | Not Started |
| TASK-018 | Session timeout logic (client-side timer + server JWT) | EP-001 | US-005 | Full-stack | P0 | 1d | Not Started |
| TASK-019 | Admin user CRUD API endpoints | EP-001 | US-006 | Backend | P1 | 1d | Not Started |
| TASK-020 | React login page with role-based redirect | EP-001 | US-003 | Frontend | P0 | 1d | Not Started |
| TASK-021 | React patient registration page + form validation | EP-001 | US-001 | Frontend | P0 | 1d | Not Started |
| TASK-022 | React admin console — user management UI | EP-001 | US-006 | Frontend | P1 | 1.5d | Not Started |
| TASK-023 | Walk-in account creation API (staff-only) | EP-001 | US-002 | Backend | P1 | 0.5d | Not Started |
| TASK-024 | IAuditLogger interface + PostgresAuditLogger implementation | EP-002 | US-007 | Backend | P0 | 1.5d | Not Started |
| TASK-025 | Audit middleware wired as cross-cutting pipeline concern | EP-002 | US-007 | Backend | P0 | 0.5d | Not Started |
| TASK-026 | Serilog structured audit sink to Seq | EP-002 | US-007 | Backend | P0 | 0.5d | Not Started |
| TASK-027 | GET /admin/audit-logs paginated endpoint (Admin only) | EP-002 | US-007 | Backend | P1 | 0.5d | Not Started |
| TASK-028 | RBAC 403/401 audit logging + admin alert threshold | EP-002 | US-008 | Backend | P0 | 0.5d | Not Started |
| TASK-029 | Ollama intake system prompt design + multi-turn API | EP-003 | US-009 | Backend/AI | P1 | 2d | Not Started |
| TASK-030 | AI intake endpoints: /start, /message, /confirm | EP-003 | US-009 | Backend | P1 | 1.5d | Not Started |
| TASK-031 | Manual intake form API endpoint | EP-003 | US-010 | Backend | P1 | 1d | Not Started |
| TASK-032 | Intake mode-switch endpoint with field mapping | EP-003 | US-011 | Backend | P1 | 1d | Not Started |
| TASK-033 | Intake draft auto-save endpoint | EP-003 | US-012 | Backend | P2 | 0.5d | Not Started |
| TASK-034 | React AI intake chat widget | EP-003 | US-009 | Frontend | P1 | 2d | Not Started |
| TASK-035 | React manual intake multi-section form | EP-003 | US-010 | Frontend | P1 | 1.5d | Not Started |
| TASK-036 | React intake mode-switch control + data preservation | EP-003 | US-011 | Frontend | P1 | 1d | Not Started |
| TASK-037 | GET /slots?available=true API endpoint | EP-004 | US-013 | Backend | P0 | 0.5d | Not Started |
| TASK-038 | POST /bookings ACID transaction (SELECT FOR UPDATE) | EP-004 | US-013 | Backend | P0 | 2d | Not Started |
| TASK-039 | No-show risk scoring algorithm + persistence | EP-004 | US-016 | Backend | P1 | 1.5d | Not Started |
| TASK-040 | Insurance pre-check endpoint (soft validation) | EP-004 | US-017, US-018 | Backend | P1 | 0.5d | Not Started |
| TASK-041 | QuestPDF appointment confirmation template | EP-004 | US-015 | Backend | P1 | 1d | Not Started |
| TASK-042 | Async SMTP email dispatch + 3-retry back-off | EP-004 | US-015 | Backend | P1 | 1d | Not Started |
| TASK-043 | React booking calendar/slot list view | EP-004 | US-013 | Frontend | P0 | 2d | Not Started |
| TASK-044 | React booking conflict UI (inline alternatives) | EP-004 | US-014 | Frontend | P1 | 1d | Not Started |
| TASK-045 | React insurance pre-check inline feedback | EP-004 | US-017, US-018 | Frontend | P1 | 0.5d | Not Started |
| TASK-046 | Preferred slot designation: POST /bookings preferredSlotId | EP-005 | US-019 | Backend | P1 | 1d | Not Started |
| TASK-047 | Slot monitor background job (poll + ACID swap) | EP-005 | US-020 | Backend | P1 | 2d | Not Started |
| TASK-048 | Swap notification: updated PDF + email + SMS | EP-005 | US-020 | Backend | P1 | 1d | Not Started |
| TASK-049 | Reminder scheduler background job | EP-005 | US-021 | Backend | P1 | 1.5d | Not Started |
| TASK-050 | Google Calendar OAuth 2.0 sync | EP-005 | US-022 | Backend | P2 | 1.5d | Not Started |
| TASK-051 | Outlook Calendar OAuth 2.0 sync (Microsoft Graph) | EP-005 | US-023 | Backend | P2 | 1.5d | Not Started |
| TASK-052 | React preferred slot selector in booking flow | EP-005 | US-019 | Frontend | P1 | 1d | Not Started |
| TASK-053 | React calendar sync buttons + opt-out settings | EP-005 | US-022 | Frontend | P2 | 1d | Not Started |
| TASK-054 | POST /walkins staff-only endpoint | EP-006 | US-025 | Backend | P0 | 1d | Not Started |
| TASK-055 | PATCH /bookings/{id}/status — Mark Arrived | EP-006 | US-026 | Backend | P0 | 0.5d | Not Started |
| TASK-056 | ASP.NET Core SignalR hub (/hubs/queue) | EP-006 | US-025 | Backend | P0 | 1.5d | Not Started |
| TASK-057 | SignalR broadcast on walk-in + arrival events | EP-006 | US-025 | Backend | P0 | 0.5d | Not Started |
| TASK-058 | GET /dashboard/staff daily operations endpoint | EP-006 | US-027 | Backend | P0 | 1d | Not Started |
| TASK-059 | GET /admin/metrics KPI aggregation endpoint | EP-006 | US-028 | Backend | P1 | 1d | Not Started |
| TASK-060 | React staff queue dashboard with SignalR client | EP-006 | US-027 | Frontend | P0 | 2.5d | Not Started |
| TASK-061 | React walk-in creation form (staff-only) | EP-006 | US-025 | Frontend | P0 | 1d | Not Started |
| TASK-062 | React admin KPI metrics dashboard | EP-006 | US-028 | Frontend | P1 | 1.5d | Not Started |
| TASK-063 | POST /documents — upload, MIME/size/SHA-256 validation | EP-007-I | US-030, US-031 | Backend | P1 | 1.5d | Not Started |
| TASK-064 | PdfPig text extraction service | EP-007-I | US-032 | Backend/AI | P1 | 1d | Not Started |
| TASK-065 | Text chunker (500-token sliding window, 50-token overlap) | EP-007-I | US-032 | Backend/AI | P1 | 1d | Not Started |
| TASK-066 | Ollama embedding call + pgvector ChunkEmbedding INSERT | EP-007-I | US-032 | Backend/AI | P1 | 1.5d | Not Started |
| TASK-067 | Ollama entity extraction prompt + JSON schema validator | EP-007-I | US-032 | Backend/AI | P1 | 2d | Not Started |
| TASK-068 | ExtractedRecord de-duplication logic | EP-007-I | US-032 | Backend | P1 | 1d | Not Started |
| TASK-069 | Extraction pipeline orchestrator + 120s timeout | EP-007-I | US-032 | Backend | P1 | 1d | Not Started |
| TASK-070 | Extraction failure detection + staff alert | EP-007-I | US-033 | Backend | P2 | 1d | Not Started |
| TASK-071 | PATCH /documents/{id}/manual-extraction endpoint | EP-007-I | US-033 | Backend | P2 | 0.5d | Not Started |
| TASK-072 | React document upload widget (drag-drop, progress, status) | EP-007-I | US-030 | Frontend | P1 | 1.5d | Not Started |
| TASK-073 | Conflict detection job (rule-based + Ollama analysis) | EP-007-II | US-034 | Backend/AI | P1 | 2d | Not Started |
| TASK-074 | PATCH /conflicts/{id}/resolve staff endpoint | EP-007-II | US-034 | Backend | P1 | 0.5d | Not Started |
| TASK-075 | GET /patients/{id}/view — 360° aggregated endpoint | EP-007-II | US-035 | Backend | P1 | 2d | Not Started |
| TASK-076 | RAG code suggestion pipeline (ICD-10 + CPT) | EP-007-II | US-036, US-037 | Backend/AI | P1 | 2.5d | Not Started |
| TASK-077 | ICD-10 + CPT format validators | EP-007-II | US-036 | Backend | P1 | 0.5d | Not Started |
| TASK-078 | PATCH /suggestions/{id} accept/reject/correct endpoint | EP-007-II | US-038 | Backend | P1 | 1d | Not Started |
| TASK-079 | AI-Human Agreement Rate KPI calculation | EP-007-II | US-039 | Backend | P1 | 0.5d | Not Started |
| TASK-080 | React 360° patient view — clinical data panel | EP-007-II | US-035 | Frontend | P1 | 2.5d | Not Started |
| TASK-081 | React conflict flags banner + resolve UI | EP-007-II | US-034 | Frontend | P1 | 1d | Not Started |
| TASK-082 | React code review tab (ICD-10 + CPT, approve/reject) | EP-007-II | US-038 | Frontend | P1 | 2d | Not Started |
| TASK-083 | Calendar sync failure — client toast + server-side error logging | EP-005 | US-024 | Full-stack | P2 | 0.5d | Not Started |
| TASK-084 | Patient self-checkin RBAC block — tests + UI gate | EP-006 | US-029 | Backend | P0 | 0.5d | Not Started |

---

## Dependency Graph

```
TASK-001 (Docker Compose)
  ├── TASK-002 (Nginx TLS)
  ├── TASK-003 (React scaffold)
  ├── TASK-004 (.NET scaffold)
  ├── TASK-005 (Prometheus/Grafana)
  └── TASK-006 (Seq/Serilog)
        └── TASK-007 (CI/CD)

TASK-008 (PostgreSQL setup)
  └── TASK-009 (EF Core migrations)
        ├── TASK-010 (pgcrypto PHI encryption)
        ├── TASK-011 (pgvector index)
        ├── TASK-012 (AuditLog INSERT-only)
        └── TASK-013 (Seed data)

[Auth Chain]
TASK-009 → TASK-014 → TASK-015 → TASK-016
  └── TASK-017 (rate limiting)
  └── TASK-018 (session timeout)
  └── TASK-019 (admin CRUD)
  └── TASK-020 (React login)
  └── TASK-021 (React register)
  └── TASK-022 (React admin console)

[Audit Chain]
TASK-016 → TASK-024 → TASK-025 → TASK-026
  └── TASK-027 (audit log API)
  └── TASK-028 (403/401 audit)

[Intake Chain]
TASK-015 → TASK-029 → TASK-030
           TASK-031
           TASK-032 → TASK-033
  └── TASK-034 (React AI chat)
  └── TASK-035 (React manual form)
  └── TASK-036 (React mode switch)

[Booking Chain]
TASK-009 → TASK-037 → TASK-038
                       TASK-039 (risk score)
                       TASK-040 (insurance)
                       TASK-041 → TASK-042 (PDF email)
  └── TASK-043 (React calendar)
  └── TASK-044 (React conflict UI)
  └── TASK-045 (React insurance UI)

[Swap/Notification Chain]
TASK-038 → TASK-046 → TASK-047 → TASK-048
           TASK-049 (reminders)
           TASK-050 (Google Cal) → TASK-083 (sync failure handling)
           TASK-051 (Outlook Cal) → TASK-083

[Queue Chain]
TASK-004 → TASK-054 → TASK-056 → TASK-057
           TASK-055                └── TASK-060 (React queue)
           TASK-058                    TASK-061 (React walk-in form)
           TASK-059                    TASK-062 (React admin KPIs)
TASK-016 → TASK-084 (Patient self-checkin RBAC block tests)

[AI Pipeline Chain]
TASK-008 → TASK-063 → TASK-064 → TASK-065 → TASK-066 → TASK-067 → TASK-068
                                                                      └── TASK-069
           TASK-070 → TASK-071
  └── TASK-072 (React upload widget)

[Clinical AI Chain]
TASK-069 → TASK-073 → TASK-074
           TASK-075
           TASK-076 → TASK-077 → TASK-078 → TASK-079
  └── TASK-080 (React 360° view)
  └── TASK-081 (React conflicts)
  └── TASK-082 (React code review)
```

---

## Sprint Plan (5 Sprints × 2 Weeks)

### Sprint 1 — Foundation (Weeks 1–2)
**Sprint Goal**: Full Docker Compose stack running with TLS; database schema complete with PHI encryption; authentication working end-to-end with RBAC.

| Task | Title | Type | Est |
|------|-------|------|-----|
| TASK-001 | Docker Compose stack scaffold | Infra | 3d |
| TASK-002 | Nginx TLS 1.2+ config | Infra | 0.5d |
| TASK-003 | React 18 SPA project scaffold | Frontend | 1d |
| TASK-004 | .NET 8.0 Web API scaffold | Backend | 1d |
| TASK-005 | Prometheus + Grafana | Infra | 1d |
| TASK-006 | Seq + Serilog setup | Infra | 0.5d |
| TASK-007 | GitHub Actions CI/CD | Infra | 1d |
| TASK-008 | PostgreSQL + pgcrypto + pgvector | DB | 1d |
| TASK-009 | EF Core migrations (12 entities) | DB | 2d |
| TASK-010 | pgcrypto PHI column encryption | DB | 1.5d |
| TASK-011 | pgvector ivfflat index | DB | 0.5d |
| TASK-012 | AuditLog INSERT-only grant | DB | 0.5d |
| TASK-013 | Seed data | DB | 0.5d |
| **Total** | | | **~14d** |

**Sprint 1 Exit Criteria**:
- [ ] `docker compose up` starts all 8 services with no errors.
- [ ] HTTPS accessible on port 443; HTTP redirects to HTTPS.
- [ ] All 12 EF Core migrations applied cleanly on fresh DB.
- [ ] PHI column ciphertext verified directly via psql (bypassing application).
- [ ] AuditLog UPDATE/DELETE blocked at DB level (permission test).

---

### Sprint 2 — Auth, HIPAA & Intake (Weeks 3–4)
**Sprint Goal**: All three roles can register and log in; HIPAA audit logging active on all sensitive actions; patients can complete AI and manual intake.

| Task | Title | Type | Est |
|------|-------|------|-----|
| TASK-014 | Patient self-registration API | Backend | 1d |
| TASK-015 | JWT auth: login, refresh | Backend | 1.5d |
| TASK-016 | RBAC middleware | Backend | 1d |
| TASK-017 | Rate limiting on /auth | Backend | 0.5d |
| TASK-018 | Session timeout (client + server) | Full-stack | 1d |
| TASK-019 | Admin user CRUD API | Backend | 1d |
| TASK-020 | React login page | Frontend | 1d |
| TASK-021 | React registration page | Frontend | 1d |
| TASK-022 | React admin user management UI | Frontend | 1.5d |
| TASK-023 | Walk-in account creation API | Backend | 0.5d |
| TASK-024 | IAuditLogger + PostgresAuditLogger | Backend | 1.5d |
| TASK-025 | Audit middleware in pipeline | Backend | 0.5d |
| TASK-026 | Serilog sink to Seq | Backend | 0.5d |
| TASK-027 | GET /admin/audit-logs endpoint | Backend | 0.5d |
| TASK-028 | 403/401 audit + admin alert | Backend | 0.5d |
| TASK-029 | Ollama intake system prompt | Backend/AI | 2d |
| TASK-030 | AI intake endpoints | Backend | 1.5d |
| TASK-031 | Manual intake form API | Backend | 1d |
| TASK-032 | Intake mode-switch endpoint | Backend | 1d |
| TASK-033 | Intake draft auto-save | Backend | 0.5d |
| TASK-034 | React AI intake chat widget | Frontend | 2d |
| TASK-035 | React manual intake form | Frontend | 1.5d |
| TASK-036 | React intake mode-switch | Frontend | 1d |
| **Total** | | | **~24d** |

> Sprint 2 is intentionally heavier — parallelise frontend and backend streams.

**Sprint 2 Exit Criteria**:
- [ ] Patient, Staff, and Admin can log in and reach role-correct dashboards.
- [ ] 5 failed logins from same IP → 429 response.
- [ ] Session expires at 15 min inactivity; warning modal shown at 14 min.
- [ ] Audit log entry created for every action listed in US-007.
- [ ] Patient can complete AI intake end-to-end (multi-turn Ollama) → IntakeRecord saved.
- [ ] Patient can complete manual intake → IntakeRecord saved.
- [ ] Mode switch preserves all entered data.

---

### Sprint 3 — Booking, Notifications & Queue (Weeks 5–6)
**Sprint Goal**: Patients can book slots end-to-end with PDF confirmation; staff can manage walk-ins and the live queue; preferred slot swap operational.

| Task | Title | Type | Est |
|------|-------|------|-----|
| TASK-037 | GET /slots?available=true | Backend | 0.5d |
| TASK-038 | POST /bookings ACID transaction | Backend | 2d |
| TASK-039 | No-show risk scoring | Backend | 1.5d |
| TASK-040 | Insurance pre-check endpoint | Backend | 0.5d |
| TASK-041 | QuestPDF confirmation template | Backend | 1d |
| TASK-042 | Async SMTP 3-retry dispatch | Backend | 1d |
| TASK-043 | React booking calendar view | Frontend | 2d |
| TASK-044 | React booking conflict UI | Frontend | 1d |
| TASK-045 | React insurance inline feedback | Frontend | 0.5d |
| TASK-046 | Preferred slot designation API | Backend | 1d |
| TASK-047 | Slot monitor background job | Backend | 2d |
| TASK-048 | Swap notification: PDF + email + SMS | Backend | 1d |
| TASK-049 | Reminder scheduler background job | Backend | 1.5d |
| TASK-050 | Google Calendar OAuth 2.0 | Backend | 1.5d |
| TASK-051 | Outlook Calendar OAuth 2.0 | Backend | 1.5d |
| TASK-052 | React preferred slot selector | Frontend | 1d |
| TASK-053 | React calendar sync + opt-out UI | Frontend | 1d |
| TASK-054 | POST /walkins staff-only | Backend | 1d |
| TASK-055 | PATCH /bookings/{id}/status Arrived | Backend | 0.5d |
| TASK-056 | SignalR hub (/hubs/queue) | Backend | 1.5d |
| TASK-057 | SignalR broadcast events | Backend | 0.5d |
| TASK-058 | GET /dashboard/staff endpoint | Backend | 1d |
| TASK-059 | GET /admin/metrics endpoint | Backend | 1d |
| TASK-060 | React staff queue + SignalR client | Frontend | 2.5d |
| TASK-061 | React walk-in creation form | Frontend | 1d |
| TASK-062 | React admin KPI dashboard | Frontend | 1.5d |
| TASK-083 | Calendar sync failure — client toast + server-side error logging | Full-stack | 0.5d |
| TASK-084 | Patient self-checkin RBAC block — tests + UI gate | Backend | 0.5d |
| **Total** | | | **~33d** |

> Sprint 3 is the largest — split booking/notification stream from queue/dashboard stream across two parallel developers.

**Sprint 3 Exit Criteria**:
- [ ] Patient books a slot → ACID transaction confirmed → PDF email received in Mailpit.
- [ ] Concurrent booking test: one slot booked by two users simultaneously → one gets 409 with alternatives.
- [ ] No-show risk score computed and visible in staff dashboard.
- [ ] Preferred slot swap: free a slot → monitor fires → patient appointment updated + notified within 60s.
- [ ] Staff walk-in → queue updates on second browser tab within 5 seconds (SignalR).
- [ ] Mark Arrived → queue row updates in real time.
- [ ] Admin metrics endpoint returns correct KPI values.
- [ ] Calendar sync failure → booking remains Confirmed, toast shown to patient, error logged.
- [ ] Patient JWT on `POST /walkins` → 403; Patient JWT on `PATCH /bookings/{id}/status` with Arrived → 403; both audit-logged.

---

### Sprint 4 — Clinical Document Upload & AI Extraction (Weeks 7–8)
**Sprint Goal**: Patients can upload clinical documents; AI pipeline extracts and de-duplicates structured data; staff alerted on extraction failures.

| Task | Title | Type | Est |
|------|-------|------|-----|
| TASK-063 | POST /documents upload + validation | Backend | 1.5d |
| TASK-064 | PdfPig text extraction service | Backend/AI | 1d |
| TASK-065 | Text chunker (sliding window) | Backend/AI | 1d |
| TASK-066 | Ollama embedding + pgvector INSERT | Backend/AI | 1.5d |
| TASK-067 | Ollama entity extraction + JSON schema validator | Backend/AI | 2d |
| TASK-068 | ExtractedRecord de-duplication | Backend | 1d |
| TASK-069 | Extraction pipeline orchestrator + timeout | Backend | 1d |
| TASK-070 | Extraction failure detection + staff alert | Backend | 1d |
| TASK-071 | PATCH /documents/{id}/manual-extraction | Backend | 0.5d |
| TASK-072 | React document upload widget | Frontend | 1.5d |
| **Total** | | | **~13d** |

**Sprint 4 Exit Criteria**:
- [ ] Patient uploads a PDF → ClinicalDocument created with status=Pending → pipeline completes within 120s → status=Complete.
- [ ] ExtractedRecord rows created with entity types: Vital, Medication, Diagnosis, Note, Allergy.
- [ ] Upload of `.exe` file → 422 with `"File type not supported"` message.
- [ ] Upload of 25MB PDF → 422 with `"File exceeds 20 MB"` message.
- [ ] De-duplication test: same medication in two documents → one ExtractedRecord with `isDuplicate=true` on the second.
- [ ] Extraction failure: mock Ollama timeout → document flagged Failed → staff alert visible in dashboard.
- [ ] PHI test: all extracted entity values are ciphertext in database direct query.

---

### Sprint 5 — 360° View, Conflict Detection & Medical Coding (Weeks 9–10)
**Sprint Goal**: Staff can view the full 360° patient view with source traceability; conflict detection surfaces contradictory data; ICD-10/CPT code suggestions ready for staff review and finalization.

| Task | Title | Type | Est |
|------|-------|------|-----|
| TASK-073 | Conflict detection job (rule-based + Ollama) | Backend/AI | 2d |
| TASK-074 | PATCH /conflicts/{id}/resolve endpoint | Backend | 0.5d |
| TASK-075 | GET /patients/{id}/view 360° endpoint | Backend | 2d |
| TASK-076 | RAG code suggestion pipeline (ICD-10 + CPT) | Backend/AI | 2.5d |
| TASK-077 | ICD-10 + CPT format validators | Backend | 0.5d |
| TASK-078 | PATCH /suggestions/{id} accept/reject/correct | Backend | 1d |
| TASK-079 | AI-Human Agreement Rate KPI calculation | Backend | 0.5d |
| TASK-080 | React 360° clinical data panel | Frontend | 2.5d |
| TASK-081 | React conflict flags banner + resolve UI | Frontend | 1d |
| TASK-082 | React code review tab (approve/reject/correct) | Frontend | 2d |
| **Total** | | | **~15d** |

**Sprint 5 Exit Criteria**:
- [ ] Staff opens 360° view → all extracted entities displayed grouped by type with source document links.
- [ ] Two conflicting medication records → ConflictFlag surfaced at top of 360° view with both values and sources.
- [ ] Staff resolves conflict → ConflictFlag status=Resolved; audit log entry written.
- [ ] RAG pipeline produces ≥1 ICD-10 and ≥1 CPT suggestion for a test patient with clinical documents.
- [ ] All suggestions stored with `reviewStatus: Pending` — no auto-accepted records in DB.
- [ ] Staff approves suggestion → `reviewStatus: Accepted`, `reviewedBy` set; KPI incremented.
- [ ] Staff rejects + enters replacement → original `Rejected`, replacement saved as `Accepted`.
- [ ] AI-Human Agreement Rate in admin KPI dashboard reflects actual review outcomes.
- [ ] 360° view response time < 3 seconds for patient with 10 documents.

---

## Detailed Task Specifications

### TASK-001: Docker Compose Stack Scaffold

**Epic**: EP-TECH
**Type**: Infrastructure
**Priority**: P0
**Estimate**: 3 days
**Depends On**: —

**Description**:
Create the complete `docker-compose.yml` defining all 8 platform services with correct network topology, named volumes, health checks, and restart policies.

**Services to define**:
| Service | Image | Network | Volume |
|---------|-------|---------|--------|
| `nginx` | nginx:1.25-alpine | frontend_net | — |
| `api` | custom .NET build | backend_net | — |
| `db` | postgres:15.3 | backend_net | db_data |
| `ollama` | ollama/ollama:0.1.29 | backend_net | ollama_models |
| `seq` | datalust/seq:2023.4 | backend_net | seq_data |
| `prometheus` | prom/prometheus:v2.45.0 | monitoring_net | prometheus_data |
| `grafana` | grafana/grafana:10.0.0 | monitoring_net | grafana_data |
| `mailpit` | axllent/mailpit:latest | backend_net | — |

**Acceptance**:
- [ ] `docker compose up --wait` exits 0 with all services healthy.
- [ ] `db` service exposes PostgreSQL only on Docker internal network (no external port binding).
- [ ] `ollama` service exposes port 11434 only on `backend_net`.
- [ ] `restart: always` on `db`, `api`, `ollama`, `seq`.
- [ ] All named volumes declared in `volumes:` section.

---

### TASK-009: EF Core Migrations — All 12 Domain Entities

**Epic**: EP-DATA
**Type**: Database
**Priority**: P0
**Estimate**: 2 days
**Depends On**: TASK-008

**Description**:
Create EF Core `DbContext` and all 12 domain entity configurations. Apply as a single initial migration or as logically grouped sequential migrations.

**Entities**: `User`, `Patient`, `IntakeRecord`, `AppointmentSlot`, `Booking`, `ClinicalDocument`, `ExtractedRecord`, `ChunkEmbedding`, `MedicalCodeSuggestion`, `AuditLog`, `ReminderSchedule`, `InsuranceRecord`.

**Key constraints to encode**:
- `User.Email` — unique index.
- `Booking.SlotId` + `Booking.PatientId` — composite unique index (prevent duplicate active bookings).
- `ChunkEmbedding.Embedding` — `vector(1536)` column type (pgvector).
- `AuditLog` — no FK cascade deletes; soft reference only.
- All FK relationships with `DeleteBehavior.Restrict` (no cascade on PHI records).

**Acceptance**:
- [ ] `dotnet ef migrations add InitialSchema` applies without error on fresh PostgreSQL 15.3+ instance.
- [ ] All 12 tables created with correct column types, nullability, and indices.
- [ ] `EXPLAIN` on `ChunkEmbedding` confirms `ivfflat` index present.

---

### TASK-024: IAuditLogger + PostgresAuditLogger

**Epic**: EP-002
**Type**: Backend
**Priority**: P0
**Estimate**: 1.5 days
**Depends On**: TASK-012, TASK-016

**Description**:
Implement the audit logging infrastructure as a scoped service injected throughout the API.

**Interface**:
```csharp
public interface IAuditLogger
{
    Task LogAsync(AuditAction action, string resourceType, string resourceId,
                  string? details = null, CancellationToken ct = default);
}
```

**PostgresAuditLogger**:
- Resolves actor identity and role from `IHttpContextAccessor`.
- Resolves IP address and UserAgent from `HttpContext.Request`.
- Executes raw SQL `INSERT INTO audit_log (...) VALUES (...)` — never via EF Core tracked entities.
- Write failure must propagate and block the calling request (UC-037 — no unlogged action completes).

**Enum AuditAction** (13 values):
`LOGIN_SUCCESS`, `LOGIN_FAILED`, `PATIENT_REGISTERED`, `PATIENT_DATA_ACCESSED`, `BOOKING_CREATED`, `BOOKING_STATUS_CHANGED`, `DOCUMENT_UPLOADED`, `CODE_SUGGESTION_REVIEWED`, `USER_CREATED`, `USER_DEACTIVATED`, `ROLE_CHANGED`, `UNAUTHORIZED_ACCESS_ATTEMPT`, `SESSION_EXPIRED`, `WALKIN_CREATED`, `INTAKE_COMPLETED`, `MANUAL_EXTRACTION_ENTERED`

**Acceptance**:
- [ ] Unit test: `LogAsync` on INSERT-only table succeeds.
- [ ] Unit test: if DB unreachable, exception propagates (does not swallow).
- [ ] Integration test: trigger `LOGIN_SUCCESS` → verify row in `audit_log` with correct `actor_id`, `ip_address`, `occurred_at`.

---

### TASK-038: POST /bookings — ACID Booking Transaction

**Epic**: EP-004
**Type**: Backend
**Priority**: P0
**Estimate**: 2 days
**Depends On**: TASK-009, TASK-015

**Description**:
Implement the core booking transaction ensuring slot-level concurrency safety.

**Transaction sequence**:
```sql
BEGIN;
  SELECT * FROM appointment_slots WHERE id = @slotId FOR UPDATE;
  -- if slot.status != 'Available': ROLLBACK → 409
  -- if duplicate booking exists for patient at same time: ROLLBACK → 409
  INSERT INTO bookings (...) VALUES (...);
  UPDATE appointment_slots SET status = 'Booked' WHERE id = @slotId;
  INSERT INTO audit_log (...);        -- must succeed or full rollback
  INSERT INTO reminder_schedules (...); -- seed reminders
COMMIT;
```

**Risk score computation** (inline, before COMMIT):
- `leadTimeDays` = `slot.scheduledDate - today`
- `priorNoShows` = `COUNT(bookings WHERE patientId = ? AND status = 'NoShow')`
- `channel` = `"Online" | "WalkIn" | "Phone"`
- Score formula: `(100 - min(leadTimeDays, 30) * 2) + (priorNoShows * 15) + (channel == "WalkIn" ? 10 : 0)` clamped to [0, 100].

**Acceptance**:
- [ ] Concurrent test: 2 requests for same slot at same time → exactly one 201, one 409.
- [ ] Duplicate booking test: same patient books same time window twice → 409 on second.
- [ ] Audit log INSERT failure → entire transaction rolled back.
- [ ] `noShowRiskScore` and `riskFactors` persisted on `Booking`.

---

### TASK-047: Slot Monitor Background Job

**Epic**: EP-005
**Type**: Backend
**Priority**: P1
**Estimate**: 2 days
**Depends On**: TASK-038, TASK-046

**Description**:
Implement a `IHostedService` background job that monitors freed slots and executes atomic swap transactions.

**Job logic**:
1. Every 30 seconds: `SELECT * FROM slot_monitors WHERE active = true`.
2. For each active monitor: check if `preferredSlotId` has `status = 'Available'`.
3. If available: execute ACID swap transaction:
   ```sql
   BEGIN;
     SELECT * FROM appointment_slots WHERE id = @preferredSlotId FOR UPDATE;
     SELECT * FROM appointment_slots WHERE id = @originalSlotId FOR UPDATE;
     UPDATE bookings SET slot_id = @preferredSlotId WHERE id = @bookingId;
     UPDATE appointment_slots SET status = 'Available' WHERE id = @originalSlotId;
     UPDATE appointment_slots SET status = 'Booked' WHERE id = @preferredSlotId;
     UPDATE slot_monitors SET active = false WHERE id = @monitorId;
     INSERT INTO audit_log (...);
   COMMIT;
   ```
4. Trigger swap notification (TASK-048) after successful commit.
5. FIFO tie-breaking: if multiple monitors target same slot, process by `created_at ASC`.

**Acceptance**:
- [ ] Race condition test: two monitors for same slot → one swap completes, second monitor deactivated with no notification.
- [ ] Appointment date passed with no swap → monitor deactivated without touching booking.
- [ ] Swap audit log entry written.
- [ ] Background job restarts automatically on crash (IHostedService fault handling).

---

### TASK-056: ASP.NET Core SignalR Hub (/hubs/queue)

**Epic**: EP-006
**Type**: Backend
**Priority**: P0
**Estimate**: 1.5 days
**Depends On**: TASK-004

**Description**:
Implement the real-time queue SignalR hub broadcasting walk-in creation and patient arrival events to all connected Staff clients.

**Hub methods**:
```csharp
public class QueueHub : Hub
{
    // Called by API service layer — not directly by clients
    public async Task BroadcastWalkInCreated(QueueEntryDto entry) =>
        await Clients.Group("Staff").SendAsync("WalkInCreated", entry);

    public async Task BroadcastPatientArrived(string bookingId, string arrivedAt) =>
        await Clients.Group("Staff").SendAsync("PatientArrived", bookingId, arrivedAt);
}
```

**Auth**:
- JWT Bearer token validated on SignalR connection (`MapHub` with `[Authorize(Roles = "Staff,Admin")]`).
- On connect: client joins `"Staff"` group.

**React client** (TASK-060):
- `@microsoft/signalr` HubConnection on `/hubs/queue`.
- `on("WalkInCreated", ...)` appends new row to queue table.
- `on("PatientArrived", ...)` updates row status badge.
- Connection auto-reconnect with exponential back-off on disconnect.

**Acceptance**:
- [ ] SignalR connection rejected for Patient JWT (role check).
- [ ] Walk-in POST → `BroadcastWalkInCreated` fires → 2 connected staff clients both receive event within 5s.
- [ ] Mark Arrived PATCH → `BroadcastPatientArrived` fires → all clients update within 5s.

---

### TASK-067: Ollama Entity Extraction Prompt + JSON Schema Validator

**Epic**: EP-007-I
**Type**: Backend/AI
**Priority**: P1
**Estimate**: 2 days
**Depends On**: TASK-065, TASK-066

**Description**:
Design the structured extraction prompt for Ollama Llama 3.1 8B and implement the JSON output validator.

**Extraction prompt template**:
```
You are a clinical data extraction assistant. Given the following document excerpt, extract all structured clinical entities.

Return ONLY valid JSON matching this schema:
{
  "entities": [
    {
      "entityType": "Vital|Medication|Diagnosis|Note|Allergy",
      "value": "<extracted value as string>",
      "confidence": <0.0–1.0>,
      "chunkRef": "<chunk sequence identifier>"
    }
  ]
}

Document excerpt:
---
{CHUNK_TEXT}
---
Extract all entities. If none found, return { "entities": [] }.
```

**JSON schema validator checks**:
- `entities` is an array.
- Each entity has `entityType` (one of 5 valid values), `value` (non-empty string), `confidence` (float 0–1), `chunkRef` (non-empty string).
- Entities with `confidence < 0.4` are logged but not persisted (`status: LowConfidence`).
- Malformed JSON or schema violations → document flagged `Failed`; audit event logged.

**Acceptance**:
- [ ] Tested with 10 representative clinical document excerpts (medication lists, discharge summaries, lab results) — entity types correctly identified in ≥8/10 cases.
- [ ] Confidence threshold test: entities below 0.4 not inserted into `ExtractedRecord`.
- [ ] Malformed JSON response from Ollama → exception caught, document flagged `Failed`, no partial insert.
- [ ] Pipeline completes for a 10-page clinical PDF within 120 seconds (AIR-008).

---

### TASK-076: RAG Code Suggestion Pipeline (ICD-10 + CPT)

**Epic**: EP-007-II
**Type**: Backend/AI
**Priority**: P1
**Estimate**: 2.5 days
**Depends On**: TASK-069, TASK-077

**Description**:
Implement the full Retrieval-Augmented Generation pipeline for medical code suggestion.

**Pipeline steps**:
1. Build a clinical query from the patient's aggregated `ExtractedRecord` diagnoses and notes.
2. Embed the query via `POST /api/embeddings` to Ollama.
3. pgvector cosine similarity search: `SELECT chunk_text FROM chunk_embeddings WHERE patient_id = ? ORDER BY embedding <=> @queryVector LIMIT 20`.
4. Build inference prompt:
```
You are a medical coding assistant. Given the clinical context below, suggest the most appropriate ICD-10-CM diagnosis codes and CPT procedure codes.

For each suggestion return:
{
  "codes": [
    {
      "codeType": "ICD10|CPT",
      "codeValue": "<code>",
      "codeDescription": "<description>",
      "sourceChunkRefs": ["<chunkRef1>", ...],
      "confidence": <0.0–1.0>
    }
  ]
}

Clinical context:
---
{TOP_20_CHUNKS}
---
```
5. Parse and validate output:
   - ICD-10: `^[A-Z][0-9]{2}\.?[0-9]{0,4}$`
   - CPT: `^[0-9]{5}$`
6. Insert valid codes as `MedicalCodeSuggestion` with `reviewStatus: Pending`.

**Acceptance**:
- [ ] Test patient with 3 clinical documents produces ≥1 ICD-10 and ≥1 CPT suggestion.
- [ ] Malformed code values rejected by format validator — not persisted.
- [ ] All persisted suggestions have `reviewStatus: Pending` (no auto-finalization).
- [ ] Each suggestion has non-empty `sourceChunkRefs` referencing actual chunk records.
- [ ] `GET /patients/{id}/codes?status=Pending` returns suggestions within 3 seconds.

---

### TASK-075: GET /patients/{id}/view — 360° Aggregated Endpoint

**Epic**: EP-007-II
**Type**: Backend
**Priority**: P1
**Estimate**: 2 days
**Depends On**: TASK-069, TASK-073

**Description**:
Implement the single aggregated patient view endpoint used by staff for clinical preparation.

**Response schema**:
```json
{
  "patient": { "demographics": {}, "insuranceStatus": "" },
  "intake": { "mode": "", "completedAt": "", "data": {} },
  "clinicalData": {
    "vitals": [{ "value": "", "sourceDocument": "", "uploadDate": "", "confidence": 0.0 }],
    "medications": [...],
    "diagnoses": [...],
    "notes": [...],
    "allergies": [...]
  },
  "conflictFlags": [
    {
      "id": "", "entityType": "", "valueA": "", "sourceA": "",
      "valueB": "", "sourceB": "", "confidence": 0.0, "status": "Unresolved"
    }
  ],
  "pendingCodeSuggestions": {
    "icd10": [{ "codeValue": "", "description": "", "confidence": 0.0, "sourceChunkRefs": [] }],
    "cpt": [...]
  },
  "documents": [{ "id": "", "filename": "", "uploadedAt": "", "processingStatus": "" }]
}
```

**Performance requirements**:
- Response time < 3 seconds for patients with up to 10 documents (NFR-008).
- Pagination for `clinicalData` arrays > 50 items.
- Staff/Admin JWT required; Patient JWT → 403.

**Acceptance**:
- [ ] Integration test: patient with 2 uploaded + processed documents → all 5 entity types populated in response.
- [ ] Source document link present on every clinical data item.
- [ ] Conflict flags appear at top of response.
- [ ] Patient JWT → 403 + audit log entry for `UNAUTHORIZED_ACCESS_ATTEMPT`.
- [ ] `PATIENT_DATA_ACCESSED` audit log entry on every successful call.
- [ ] Response time < 3s measured in integration test.

---

## Estimation Summary

| Sprint | Focus | Total Estimate |
|--------|-------|----------------|
| Sprint 1 | Infrastructure + Data Layer | ~14 dev-days |
| Sprint 2 | Auth + HIPAA + Intake | ~24 dev-days |
| Sprint 3 | Booking + Notifications + Queue | ~33 dev-days |
| Sprint 4 | Document Upload + AI Extraction | ~13 dev-days |
| Sprint 5 | 360° View + Conflicts + Coding | ~15 dev-days |
| **Total** | | **~99 dev-days** |

> At a 2-developer team: ~49 working days ≈ 10 weeks (5 × 2-week sprints).
> At a 3-developer team: ~33 working days ≈ 7 weeks.

---

### TASK-083: Calendar Sync Failure — Client Toast + Server-Side Error Logging

**Epic**: EP-005
**Story**: US-024
**Type**: Full-stack
**Priority**: P2
**Estimate**: 0.5 days
**Depends On**: TASK-050, TASK-051

**Description**:
Ensure that Google and Outlook calendar sync failures are handled gracefully: booking is never rolled back, the client receives an actionable toast notification, a retry entry point is surfaced, and the failure is logged server-side.

**Server-side**:
- Wrap all Google Calendar API and Microsoft Graph API calls in `try/catch`.
- On exception: log structured error via Serilog (`CalendarSyncFailed`, `provider`, `patientId`, `bookingId`, `errorMessage`).
- Return `{ calendarSyncStatus: "Failed", retryAvailable: true }` in the booking response body — do **not** throw; booking transaction must already be committed.
- Expose `POST /calendar/{provider}/retry?bookingId={id}` endpoint (authenticated patient) for manual retry.

**Client-side**:
- After booking confirmation, if response contains `calendarSyncStatus: "Failed"`: display toast: `"Calendar sync failed. You can retry from your profile or add the event manually."`
- Toast includes an inline `"Retry"` button that calls the retry endpoint.
- Toast is non-blocking — user can dismiss and continue.
- On the appointment details page, a `"Retry Calendar Sync"` button is visible if `calendarSyncStatus != "Synced"`.

**Acceptance**:
- [ ] Unit test: mock Google Calendar API timeout → booking transaction already committed → `calendarSyncStatus: Failed` in response.
- [ ] Unit test: mock Microsoft Graph API 429 → same behaviour.
- [ ] Integration test: retry endpoint called after failed sync → calendar event created on second attempt.
- [ ] Booking record `calendarSyncStatus` field updated to `"Synced"` on successful retry.
- [ ] Serilog `CalendarSyncFailed` event visible in Seq on failure.

---

### TASK-084: Patient Self-Checkin RBAC Block — Tests + UI Gate

**Epic**: EP-006
**Story**: US-029
**Type**: Backend
**Priority**: P0
**Estimate**: 0.5 days
**Depends On**: TASK-016, TASK-054, TASK-055

**Description**:
Verify and harden the RBAC enforcement ensuring patients cannot create walk-in bookings or self-mark as arrived through any interface — API or UI.

**Backend enforcement** (already in TASK-016 + TASK-054 + TASK-055 — this task adds explicit test coverage and audit logging verification):
- `POST /walkins`: `[Authorize(Roles = "Staff,Admin")]` — Patient JWT → 403.
- `PATCH /bookings/{id}/status` with `{ status: "Arrived" }`: `[Authorize(Roles = "Staff,Admin")]` — Patient JWT → 403.
- Both 403 responses must trigger `IAuditLogger.LogAsync(AuditAction.UNAUTHORIZED_ACCESS_ATTEMPT, ...)`.

**Frontend gate**:
- `"New Walk-in"` button rendered only when `user.role === "Staff" || user.role === "Admin"`.
- Walk-in and Mark Arrived controls absent from the Patient portal DOM entirely (not just hidden via CSS).
- Unit test: render staff queue component with Patient role → assert no walk-in button in DOM.

**Integration tests**:
```
Given: valid Patient JWT
When: POST /walkins
Then: 403 Forbidden
  AND: audit_log contains { action: "UNAUTHORIZED_ACCESS_ATTEMPT", resourcePath: "/walkins", actorId: patientId }

Given: valid Patient JWT
When: PATCH /bookings/{id}/status { status: "Arrived" }
Then: 403 Forbidden
  AND: audit_log contains { action: "UNAUTHORIZED_ACCESS_ATTEMPT", resourcePath: "/bookings/.../status", actorId: patientId }
```

**Acceptance**:
- [ ] Both integration tests pass.
- [ ] Unit test: patient role → walk-in button not in DOM.
- [ ] Manual audit log verification: 403 entries present in Seq within 5 seconds of failed attempt.

---


| Risk ID | Risk | Impact | Mitigation |
|---------|------|--------|------------|
| RK-001 | Ollama Llama 3.1 8B extraction accuracy below 98% target | High | Benchmark against 20-doc test corpus in Sprint 4; tune prompt before Sprint 5 |
| RK-002 | pgvector cosine search latency degrades with large patient corpus | Medium | Benchmark with 1000 ChunkEmbedding rows; tune `ivfflat lists` parameter |
| RK-003 | Slot swap race condition under concurrent monitor resolution | High | ACID + `SELECT FOR UPDATE` + FIFO; concurrency test in Sprint 3 |
| RK-004 | SignalR connection instability on Nginx reverse proxy | Medium | Configure Nginx WebSocket upgrade headers; test with 10 concurrent Staff clients |
| RK-005 | Twilio free trial SMS daily limit exhausted in testing | Low | Use Mailpit mock for SMS in dev; Twilio only in staging |
| RK-006 | 120-second extraction budget exceeded on large PDFs | High | Profile in Sprint 4; reduce chunk size or increase overlap if needed; fallback to manual extraction |
