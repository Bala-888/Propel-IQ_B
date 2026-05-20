# User Stories — Unified Patient Access & Clinical Intelligence Platform

## Story Summary Table

| Story ID | Title | Epic | Role | FRs | UCs |
|----------|-------|------|------|-----|-----|
| US-001 | Patient Self-Registration | EP-001 | Patient | FR-001, FR-003, FR-004 | UC-001 |
| US-002 | Staff Walk-in Account Creation | EP-001 | Staff | FR-002, FR-004 | UC-002 |
| US-003 | Multi-Role Login & Dashboard Redirect | EP-001 | Patient / Staff / Admin | FR-003, FR-004 | UC-003 |
| US-004 | Login Failure & Rate Limiting | EP-001 | Any | FR-003 | UC-004 |
| US-005 | Session Timeout with Warning | EP-001 | Any | FR-005 | UC-005 |
| US-006 | Admin Creates, Edits & Deactivates Users | EP-001 | Admin | FR-006 | UC-006 |
| US-007 | Immutable Audit Log on Every Sensitive Action | EP-002 | System | FR-044, FR-043 | UC-037 |
| US-008 | Unauthorized Access Blocked & Logged | EP-002 | Any | FR-004, FR-043, FR-044 | UC-038, UC-023 |
| US-009 | AI Conversational Intake | EP-003 | Patient | FR-007, FR-009, FR-010 | UC-007 |
| US-010 | Manual Intake Form Submission | EP-003 | Patient | FR-008, FR-009, FR-010 | UC-008 |
| US-011 | Mid-Session Intake Mode Switch | EP-003 | Patient | FR-009 | UC-009 |
| US-012 | Intake Validation Failure & Draft Save | EP-003 | Patient | FR-007, FR-008 | UC-010 |
| US-013 | Patient Books an Available Slot | EP-004 | Patient | FR-011, FR-012, FR-015 | UC-011 |
| US-014 | Booking Conflict & Alternative Slot Offer | EP-004 | Patient | FR-011, FR-015 | UC-012 |
| US-015 | PDF Appointment Confirmation Email | EP-004 | System | FR-013 | UC-013 |
| US-016 | No-Show Risk Score Computed & Shown to Staff | EP-004 | Staff | FR-014 | UC-011, UC-035 |
| US-017 | Insurance Pre-Check Passes | EP-004 | Patient | FR-039 | UC-033 |
| US-018 | Insurance Pre-Check Fails (Non-Blocking Warning) | EP-004 | Patient | FR-039 | UC-034 |
| US-019 | Patient Designates a Preferred Slot | EP-005 | Patient | FR-016 | UC-014 |
| US-020 | System Auto-Executes Preferred Slot Swap | EP-005 | System | FR-017, FR-018, FR-019 | UC-015, UC-016 |
| US-021 | Automated Appointment Reminders (SMS + Email) | EP-005 | System | FR-020, FR-023 | UC-017 |
| US-022 | Google Calendar Sync on Booking | EP-005 | Patient | FR-021, FR-023 | UC-018 |
| US-023 | Outlook Calendar Sync on Booking | EP-005 | Patient | FR-022, FR-023 | UC-019 |
| US-024 | Calendar Sync Failure Handled Gracefully | EP-005 | System | FR-021, FR-022 | UC-020 |
| US-025 | Staff Creates Walk-in Booking & Queue Updates | EP-006 | Staff | FR-024, FR-025, FR-028 | UC-021 |
| US-026 | Staff Marks Patient as Arrived | EP-006 | Staff | FR-027 | UC-022 |
| US-027 | Real-Time Queue Dashboard for Staff | EP-006 | Staff | FR-026, FR-028 | UC-021, UC-035 |
| US-028 | Admin Views KPI Metrics Dashboard | EP-006 | Admin | FR-042 | UC-036 |
| US-029 | Patient Cannot Self-Check-In | EP-006 | Patient | FR-024 | UC-023 |
| US-030 | Patient Uploads Clinical Document | EP-007-I | Patient | FR-029, FR-031 | UC-024 |
| US-031 | Document Upload Validation Failure | EP-007-I | Patient | FR-029, FR-030 | UC-025 |
| US-032 | AI Extracts & De-duplicates Clinical Data | EP-007-I | System | FR-032, FR-033 | UC-026 |
| US-033 | Extraction Failure Flagged for Manual Review | EP-007-I | Staff | FR-032 | UC-029 |
| US-034 | Data Conflict Detected and Surfaced to Staff | EP-007-II | Staff | FR-034 | UC-027 |
| US-035 | Staff Views 360-Degree Patient View | EP-007-II | Staff | FR-035 | UC-028 |
| US-036 | AI Suggests ICD-10 Diagnosis Codes | EP-007-II | Staff | FR-036, FR-038 | UC-030 |
| US-037 | AI Suggests CPT Procedure Codes | EP-007-II | Staff | FR-037, FR-038 | UC-030 |
| US-038 | Staff Approves AI Code Suggestion | EP-007-II | Staff | FR-038 | UC-031 |
| US-039 | Staff Rejects or Corrects AI Code Suggestion | EP-007-II | Staff | FR-038 | UC-032 |
| US-040 | All PHI Encrypted at Rest and in Transit | EP-002 | System | FR-045, NFR-005 | UC-037 |

---

## EP-001 — Authentication, RBAC & User Management

---

### US-001: Patient Self-Registration

**Epic**: EP-001
**Role**: Patient
**Mapped FRs**: FR-001, FR-003, FR-004
**Mapped UCs**: UC-001

**Story**:
As a **new patient**, I want to self-register on the platform by providing my personal and insurance details, so that I can access appointment booking and clinical features without needing staff assistance.

**Acceptance Criteria**:
- [ ] Registration form collects: full name, date of birth, email address, phone number, insurance provider, and insurance ID.
- [ ] All mandatory fields (name, DOB, email, phone) are validated; submission blocked if any are missing with field-level error messages.
- [ ] System checks for duplicate email; returns "An account with this email already exists" if found — does not reveal the existing account's role.
- [ ] On successful registration, account is created with role = `Patient` and `isActive = true`.
- [ ] All PHI fields (DOB, phone, insurance provider, insurance ID) are stored AES-256 encrypted via pgcrypto.
- [ ] Patient is redirected to the intake screen after successful registration.
- [ ] An audit log entry is written: `{ action: "PATIENT_REGISTERED", resourceType: "User", actorId: newUserId }`.

**Definition of Done**:
- [ ] `POST /auth/register` returns `201 Created` with JWT on success; `409 Conflict` on duplicate email.
- [ ] Unit tests for duplicate detection, field validation, and PHI encryption.
- [ ] Audit log entry verified in integration test.

---

### US-002: Staff Walk-in Account Creation

**Epic**: EP-001
**Role**: Staff
**Mapped FRs**: FR-002, FR-004
**Mapped UCs**: UC-002

**Story**:
As a **staff member**, I want to optionally create a patient account when processing a walk-in booking, so that the patient can access their records online after their visit.

**Acceptance Criteria**:
- [ ] Walk-in booking flow includes an optional "Create Account for Patient" toggle — off by default.
- [ ] When toggled on, staff provides: patient name, email, and phone. DOB is optional.
- [ ] System generates a temporary password and sends it to the patient's email.
- [ ] Account is created with role = `Patient`; patient receives credentials via email.
- [ ] When toggled off, walk-in booking is created without an associated patient account; no email is sent.
- [ ] Staff cannot create accounts with `Staff` or `Admin` roles via this form — role is locked to `Patient`.
- [ ] Audit log entry written for account creation: `{ action: "WALKIN_ACCOUNT_CREATED", actorId: staffId }`.

**Definition of Done**:
- [ ] `POST /walkins` with `createAccount: true` creates User + Booking atomically; credential email queued.
- [ ] `POST /walkins` with `createAccount: false` creates Booking only.
- [ ] Integration test validates email queued, audit log written, credentials email contains temporary password.

---

### US-003: Multi-Role Login & Dashboard Redirect

**Epic**: EP-001
**Role**: Patient / Staff / Admin
**Mapped FRs**: FR-003, FR-004
**Mapped UCs**: UC-003

**Story**:
As a **registered user** (Patient, Staff, or Admin), I want to log in with my email and password and be routed to the correct dashboard for my role, so that I see only the features relevant to my access level.

**Acceptance Criteria**:
- [ ] Login form accepts email and password.
- [ ] On valid credentials: JWT Bearer token issued (15-minute expiry) + refresh token stored.
- [ ] Role claim is embedded in JWT (`role: "Patient" | "Staff" | "Admin"`).
- [ ] Patient is redirected to `/intake` (or `/dashboard` if intake complete).
- [ ] Staff is redirected to `/queue`.
- [ ] Admin is redirected to `/admin`.
- [ ] Session inactivity timer starts on login; 15-minute expiry enforced client-side and server-side.
- [ ] Audit log entry: `{ action: "LOGIN_SUCCESS", actorId, actorRole, ipAddress }`.

**Definition of Done**:
- [ ] `POST /auth/login` returns `200` with `{ accessToken, refreshToken, role }`.
- [ ] React SPA reads role from JWT and navigates to correct route on login.
- [ ] Unit tests for token generation, role claim embedding, redirect logic.

---

### US-004: Login Failure & Rate Limiting

**Epic**: EP-001
**Role**: Any
**Mapped FRs**: FR-003
**Mapped UCs**: UC-004

**Story**:
As a **security-conscious system**, I want failed login attempts to return a generic error and trigger progressive rate limiting, so that brute-force attacks and account enumeration are prevented.

**Acceptance Criteria**:
- [ ] Invalid email or password returns HTTP `401` with message: `"Invalid email or password."` — does not indicate which field is wrong.
- [ ] After 5 consecutive failed attempts from the same IP within 15 minutes, all further attempts from that IP return HTTP `429 Too Many Requests`.
- [ ] Lockout is lifted automatically after 15 minutes with no further attempts.
- [ ] Every failed attempt is recorded in the audit log: `{ action: "LOGIN_FAILED", ipAddress, userAgent, timestamp }`.
- [ ] Admin dashboard shows an alert if 3+ unauthorized access attempts occur from the same IP within 10 minutes.

**Definition of Done**:
- [ ] Rate limiting middleware applied to `POST /auth/login` using ASP.NET Core built-in rate limiter.
- [ ] Unit tests for generic error response and lockout threshold.
- [ ] Integration test verifies 6th request returns `429`.

---

### US-005: Session Timeout with Warning

**Epic**: EP-001
**Role**: Any
**Mapped FRs**: FR-005
**Mapped UCs**: UC-005

**Story**:
As a **logged-in user**, I want to receive a warning before my session expires due to inactivity and be given the option to extend it, so that I don't lose unsaved work unexpectedly.

**Acceptance Criteria**:
- [ ] After 14 minutes of inactivity, a modal is shown: "Your session will expire in 60 seconds. Extend session?"
- [ ] If the user clicks "Extend Session", the session timer resets to 15 minutes.
- [ ] If the user does not respond within 60 seconds, the JWT is invalidated and user is redirected to `/login` with message: `"Your session has expired. Please log in again."`.
- [ ] Server-side: JWT refresh token is invalidated on expiry — subsequent API calls return `401`.
- [ ] Audit log entry: `{ action: "SESSION_EXPIRED", actorId, timestamp }`.

**Definition of Done**:
- [ ] Client-side inactivity timer (reset on user interactions: mouse, keyboard, scroll).
- [ ] Warning modal shown at 14-minute mark; dismiss extends, ignore triggers logout.
- [ ] E2E test simulates inactivity and verifies redirect.

---

### US-006: Admin Creates, Edits & Deactivates Users

**Epic**: EP-001
**Role**: Admin
**Mapped FRs**: FR-006, FR-041
**Mapped UCs**: UC-006

**Story**:
As an **admin**, I want to create, update, and deactivate user accounts and change role assignments, so that I can manage who has access to the platform and at what level.

**Acceptance Criteria**:
- [ ] Admin console shows a user list table with columns: name, email, role, status (Active/Inactive), last login.
- [ ] "Create User" modal: name, email, role (dropdown: Patient / Staff / Admin), generates temporary password and emails it.
- [ ] "Edit User" modal: update name, email, role. Email uniqueness validated.
- [ ] "Deactivate User": sets `isActive = false`; deactivated user receives `401` on next login attempt.
- [ ] Admin cannot deactivate their own account — action blocked with error: `"You cannot deactivate your own account."`.
- [ ] All user management actions recorded in audit log with actor ID and timestamp.
- [ ] Affected users receive an email notification on account creation and role change.

**Definition of Done**:
- [ ] `POST /admin/users`, `PATCH /admin/users/{id}`, `DELETE /admin/users/{id}` endpoints, all requiring `Admin` role JWT.
- [ ] Unit tests for self-deactivation block, duplicate email check, role validation.
- [ ] Integration test: create user → deactivate → verify 401 on next login.

---

## EP-002 — HIPAA Compliance, Security & Audit

---

### US-007: Immutable Audit Log on Every Sensitive Action

**Epic**: EP-002
**Role**: System (cross-cutting)
**Mapped FRs**: FR-044, FR-043
**Mapped UCs**: UC-037

**Story**:
As a **HIPAA compliance officer**, I want every patient data access, modification, booking action, and administrative operation to be recorded in an immutable, append-only audit log, so that we can demonstrate full accountability and detect unauthorized access.

**Acceptance Criteria**:
- [ ] Audit log captures: `actorId`, `actorRole`, `actionType`, `resourceType`, `resourceId`, `ipAddress`, `userAgent`, `details (JSONB)`, `occurredAt (UTC)`.
- [ ] AuditLog table: PostgreSQL role has INSERT-only privilege — no UPDATE or DELETE grants at DB level.
- [ ] Audit log write is transactionally linked to the triggering action: if the audit log write fails, the action is blocked and rolled back.
- [ ] The following action types are captured: `LOGIN_SUCCESS`, `LOGIN_FAILED`, `PATIENT_REGISTERED`, `PATIENT_DATA_ACCESSED`, `BOOKING_CREATED`, `BOOKING_STATUS_CHANGED`, `DOCUMENT_UPLOADED`, `CODE_SUGGESTION_REVIEWED`, `USER_CREATED`, `USER_DEACTIVATED`, `ROLE_CHANGED`, `UNAUTHORIZED_ACCESS_ATTEMPT`, `SESSION_EXPIRED`.
- [ ] Audit events are emitted to Seq via Serilog structured sink alongside DB write.
- [ ] `GET /admin/audit-logs` endpoint (Admin role only) returns paginated, read-only audit entries with filter by date range, actor, and action type.

**Definition of Done**:
- [ ] `IAuditLogger` interface with `PostgresAuditLogger` implementation injected as scoped service.
- [ ] DB migration: `REVOKE UPDATE, DELETE ON audit_log FROM app_user;` applied.
- [ ] Integration test: trigger each audit event type → verify entry in DB and Seq.

---

### US-008: Unauthorized Access Blocked & Logged

**Epic**: EP-002
**Role**: Any
**Mapped FRs**: FR-004, FR-043, FR-044
**Mapped UCs**: UC-038, UC-023

**Story**:
As a **security system**, I want any attempt to access a resource or perform an action that exceeds the caller's role to be blocked and logged, so that unauthorized data access is prevented and traceable.

**Acceptance Criteria**:
- [ ] Every API endpoint enforces `[Authorize(Roles = "...")]` — unauthenticated requests return `401`, wrong role returns `403`.
- [ ] A Patient attempting to access walk-in creation or queue management endpoints receives `403 Forbidden`.
- [ ] A Staff member attempting to access admin user management endpoints receives `403 Forbidden`.
- [ ] Every `401`/`403` response generates an audit log entry: `{ action: "UNAUTHORIZED_ACCESS_ATTEMPT", resourcePath, ipAddress, actorId (if authenticated) }`.
- [ ] If 3 or more `403` events from the same source IP occur within 10 minutes, an alert is pushed to the Admin dashboard notification panel.
- [ ] TLS 1.2+ enforced at Nginx — HTTP requests redirected to HTTPS; no unencrypted API connections accepted.

**Definition of Done**:
- [ ] RBAC enforcement verified via integration tests for all 3 roles on all protected endpoints.
- [ ] Alert threshold unit tested with mock audit log.
- [ ] Nginx TLS configuration validated (SSL Labs Grade A equivalent in local test).

---

## EP-003 — Patient Intake (AI Conversational & Manual)

---

### US-009: AI Conversational Intake

**Epic**: EP-003
**Role**: Patient
**Mapped FRs**: FR-007, FR-009, FR-010
**Mapped UCs**: UC-007

**Story**:
As a **patient**, I want to complete my pre-visit health intake through a natural language conversation with an AI assistant, so that I can provide my health history comfortably without filling out a complex form.

**Acceptance Criteria**:
- [ ] Patient selects "Start AI Intake" — a chat interface opens with an AI greeting.
- [ ] AI progressively collects: demographics (name confirmation, DOB), medical history (past conditions, surgeries), current medications (name, dose, frequency), allergies (medication and environmental), and chief complaint.
- [ ] AI handles colloquial descriptions (e.g., "water pill" → diuretic) and asks clarifying follow-up questions.
- [ ] When all required fields are collected, AI presents a structured summary for patient review.
- [ ] Patient can edit any individual field on the summary screen before confirming.
- [ ] On confirmation, `IntakeRecord` is saved with `mode: AI`, `status: Complete`, and encrypted JSONB data.
- [ ] All AI inference runs via local Ollama — no PHI is transmitted externally.
- [ ] Audit log entry: `{ action: "INTAKE_COMPLETED", patientId, mode: "AI" }`.

**Definition of Done**:
- [ ] `POST /intake/ai/start`, `POST /intake/ai/message`, `POST /intake/ai/confirm` endpoints.
- [ ] Ollama system prompt tested with ≥10 representative patient scenarios covering edge cases.
- [ ] Integration test: complete multi-turn dialogue → verify IntakeRecord created with correct encrypted fields.

---

### US-010: Manual Intake Form Submission

**Epic**: EP-003
**Role**: Patient
**Mapped FRs**: FR-008, FR-009, FR-010
**Mapped UCs**: UC-008

**Story**:
As a **patient**, I want to complete my pre-visit intake using a structured form without AI assistance, so that I can provide my health information at my own pace in a familiar format.

**Acceptance Criteria**:
- [ ] Patient selects "Manual Intake Form" — a multi-section form is displayed (Demographics, Medical History, Current Medications, Allergies, Chief Complaint).
- [ ] Each section is presented on a single scrollable page with clear headings and field labels.
- [ ] All mandatory fields validated on submit; missing fields highlighted with inline error messages.
- [ ] Patient can save as draft at any point — partial data is preserved.
- [ ] On submit, `IntakeRecord` saved with `mode: Manual`, `status: Complete`, encrypted JSONB.
- [ ] Patient can return later and edit any field without staff intervention.
- [ ] Audit log entry: `{ action: "INTAKE_COMPLETED", patientId, mode: "Manual" }`.

**Definition of Done**:
- [ ] `POST /intake/manual` and `POST /intake/draft` endpoints.
- [ ] Unit tests for field validation (required fields, date format, medication format).
- [ ] Integration test: submit form → verify IntakeRecord created.

---

### US-011: Mid-Session Intake Mode Switch

**Epic**: EP-003
**Role**: Patient
**Mapped FRs**: FR-009
**Mapped UCs**: UC-009

**Story**:
As a **patient**, I want to switch between AI chat and manual form at any point during intake without losing my previously entered data, so that I can use whichever input method I prefer at any moment.

**Acceptance Criteria**:
- [ ] A "Switch to Manual Form" button is visible during AI intake; a "Switch to AI Intake" button is visible during manual form.
- [ ] On switch, all data collected in the source mode is mapped to the equivalent fields in the target mode.
- [ ] Pre-populated fields in the target mode are editable.
- [ ] Data collected in the source mode that has no direct mapping is displayed as a "Review" item and not silently discarded.
- [ ] Switching does not create a new session or clear the current `sessionId`.
- [ ] Audit log entry: `{ action: "INTAKE_MODE_SWITCHED", patientId, fromMode, toMode }`.

**Definition of Done**:
- [ ] `POST /intake/mode-switch { sessionId, targetMode }` endpoint with field mapping logic.
- [ ] Unit test: AI session with 3 collected fields → switch → verify 3 fields pre-populated in manual form.
- [ ] Unit test: Manual form with 3 fields → switch → verify AI summary pre-populated.

---

### US-012: Intake Validation Failure & Draft Save

**Epic**: EP-003
**Role**: Patient
**Mapped FRs**: FR-007, FR-008
**Mapped UCs**: UC-010

**Story**:
As a **patient**, I want to see specific error messages when my intake submission fails validation and have my work preserved as a draft, so that I can fix issues without re-entering everything.

**Acceptance Criteria**:
- [ ] Submitting intake with missing mandatory fields returns field-level error messages (not a generic error).
- [ ] Invalid date formats (e.g., DOB in wrong format) are highlighted with a correction hint.
- [ ] Entered data is never cleared on a failed submission — all values remain in the form.
- [ ] If the patient navigates away mid-intake, their partial data is auto-saved as a draft (`status: Draft`).
- [ ] On returning to intake, the patient is prompted to resume their draft.
- [ ] Draft is stored encrypted in `IntakeRecord` with `status: Draft`.

**Definition of Done**:
- [ ] Client-side validation mirrors server-side rules for fast feedback.
- [ ] `POST /intake/draft` called on navigation-away event.
- [ ] Unit test: submit incomplete form → verify error fields identified + no IntakeRecord status=Complete created.

---

## EP-004 — Appointment Booking, Slots & Insurance Pre-check

---

### US-013: Patient Books an Available Slot

**Epic**: EP-004
**Role**: Patient
**Mapped FRs**: FR-011, FR-012, FR-015
**Mapped UCs**: UC-011

**Story**:
As a **patient**, I want to view available appointment slots and book one, so that I can schedule my visit with the clinic.

**Acceptance Criteria**:
- [ ] Available slots are displayed in a calendar/list view with date, time, and duration.
- [ ] Only `status: Available` slots are shown to patients.
- [ ] Patient selects a slot and sees a booking confirmation summary (date, time, insurance status) before confirming.
- [ ] Booking is created atomically via `SELECT FOR UPDATE` on the slot row — race condition prevented.
- [ ] If the selected slot is taken by a concurrent booking, a `409 Conflict` is returned with the nearest available alternatives.
- [ ] Patient cannot hold two active bookings for the same time window; duplicate attempt returns an error.
- [ ] Booking confirmed: `AppointmentSlot.status` → `Booked`, `Booking.status` → `Confirmed`, all in a single ACID transaction.
- [ ] Audit log entry: `{ action: "BOOKING_CREATED", patientId, slotId, bookingId }`.

**Definition of Done**:
- [ ] `GET /slots?available=true` and `POST /bookings` endpoints.
- [ ] Concurrent booking test: two requests for same slot simultaneously → one succeeds, one gets 409.
- [ ] Integration test: book slot → verify slot status=Booked, booking status=Confirmed, audit log entry.

---

### US-014: Booking Conflict & Alternative Slot Offer

**Epic**: EP-004
**Role**: Patient
**Mapped FRs**: FR-011, FR-015
**Mapped UCs**: UC-012

**Story**:
As a **patient**, I want to be shown alternative slots when my chosen slot is taken or I already have an active booking, so that I can quickly select another option without starting over.

**Acceptance Criteria**:
- [ ] If the selected slot is no longer available: error message `"This slot is no longer available."` shown with up to 3 nearest alternative available slots.
- [ ] If the patient already has an active booking at the same time: error `"You already have an active booking for this time."` shown.
- [ ] If no alternative slots are available: message `"No slots currently available."` with option to join a waitlist.
- [ ] Alternative slots are presented inline in the booking UI — patient does not need to restart the flow.

**Definition of Done**:
- [ ] `POST /bookings` returns `409` with `{ message, alternatives: Slot[] }` on conflict.
- [ ] Unit test: validate alternative slot query returns nearest available after target slot.
- [ ] UI test: 409 response renders alternatives inline.

---

### US-015: PDF Appointment Confirmation Email

**Epic**: EP-004
**Role**: System
**Mapped FRs**: FR-013
**Mapped UCs**: UC-013

**Story**:
As a **patient**, I want to receive a PDF appointment confirmation by email immediately after booking, so that I have a formal record of my appointment details.

**Acceptance Criteria**:
- [ ] PDF is generated server-side (QuestPDF) immediately after booking is confirmed.
- [ ] PDF content: patient name, appointment date, appointment time, clinic location, booking reference number, insurance pre-check status.
- [ ] PDF is attached to an SMTP email sent to the patient's registered email address within 30 seconds of booking.
- [ ] If SMTP delivery fails, the system retries up to 3 times with exponential back-off (2s, 4s, 8s).
- [ ] If all 3 retries fail, the failure is logged in `ReminderSchedule.deliveryStatus = Failed` and an alert raised.
- [ ] Email is sent asynchronously — booking confirmation is not blocked by email delivery.

**Definition of Done**:
- [ ] QuestPDF template renders all required fields.
- [ ] Async email dispatch tested with Mailpit in dev environment.
- [ ] Retry logic unit tested with mocked SMTP failures.

---

### US-016: No-Show Risk Score Computed & Shown to Staff

**Epic**: EP-004
**Role**: Staff
**Mapped FRs**: FR-014
**Mapped UCs**: UC-011, UC-035

**Story**:
As a **staff member**, I want to see a no-show risk score for each appointment, so that I can proactively reach out to high-risk patients and reduce empty appointment slots.

**Acceptance Criteria**:
- [ ] No-show risk score (0–100) is computed at booking time using rule-based algorithm: booking lead time (days until appointment), patient's prior no-show history (count), and booking channel (online/walk-in/phone).
- [ ] Score and input factor values are persisted in `Booking.noShowRiskScore` and `Booking.riskFactors (JSONB)`.
- [ ] Staff queue dashboard displays a colour-coded risk badge per patient: Green (0–33), Amber (34–66), Red (67–100).
- [ ] Staff can click a patient row to see the risk score breakdown (lead time, prior no-shows, channel).
- [ ] Score is read-only for staff — no manual override in Phase 1.

**Definition of Done**:
- [ ] Risk scoring service: unit tested with boundary values for each factor.
- [ ] `GET /dashboard/staff` returns `noShowRiskScore` and `riskFactors` per booking.
- [ ] UI test: risk badge renders correct colour for each score range.

---

### US-017: Insurance Pre-Check Passes

**Epic**: EP-004
**Role**: Patient
**Mapped FRs**: FR-039
**Mapped UCs**: UC-033

**Story**:
As a **patient**, I want to see a confirmation that my insurance details have been verified during booking, so that I know there are no foreseeable billing issues before my appointment.

**Acceptance Criteria**:
- [ ] Insurance pre-check runs against the internal `InsuranceRecord` dummy dataset during the booking flow.
- [ ] If a match is found, a green `"Insurance Verified ✓"` indicator is shown on the booking form.
- [ ] Pre-check result (`status: Verified`) is stored on the `Booking` record.
- [ ] Verification is non-blocking — booking proceeds regardless of outcome.
- [ ] Pre-check runs silently in the background when the patient enters their insurance provider name and ID.

**Definition of Done**:
- [ ] `POST /insurance/validate` endpoint with case-insensitive pattern matching against `InsuranceRecord` seed data.
- [ ] Unit test: known provider + valid pattern → returns `{ status: "Verified" }`.

---

### US-018: Insurance Pre-Check Fails (Non-Blocking Warning)

**Epic**: EP-004
**Role**: Patient
**Mapped FRs**: FR-039
**Mapped UCs**: UC-034

**Story**:
As a **patient**, I want to see a warning (not a blocker) when my insurance details cannot be verified, so that I'm aware of a potential issue while still being able to complete my booking.

**Acceptance Criteria**:
- [ ] If no match is found in `InsuranceRecord`, a yellow warning banner is shown: `"Insurance details could not be verified. Please confirm with the clinic before your visit."`
- [ ] Booking is **not** blocked — patient can proceed.
- [ ] Appointment record is flagged with `insuranceStatus: "Not Verified"` for staff visibility in the dashboard.
- [ ] If the pre-check service is unavailable, booking proceeds with `insuranceStatus: "Check Skipped"` and error logged.
- [ ] Patient can update their insurance details and re-trigger validation without refreshing the page.

**Definition of Done**:
- [ ] `POST /insurance/validate` returns `{ status: "NotVerified" }` for unmatched entries.
- [ ] Unit test: unknown provider → returns `NotVerified`, booking succeeds.
- [ ] Staff dashboard test: `Not Verified` flag visible in appointment details.

---

## EP-005 — Preferred Slot Swap, Notifications & Calendar Sync

---

### US-019: Patient Designates a Preferred Slot

**Epic**: EP-005
**Role**: Patient
**Mapped FRs**: FR-016
**Mapped UCs**: UC-014

**Story**:
As a **patient**, I want to designate an unavailable slot as my preferred appointment time when booking, so that the system can automatically move me to that slot if it opens up.

**Acceptance Criteria**:
- [ ] After selecting an available slot, patient sees an option: `"I'd prefer a different time"`.
- [ ] Patient can browse unavailable (Booked/Blocked) slots and select one as their preference.
- [ ] Selected preferred slot is stored as `Booking.preferredSlotId`.
- [ ] A `SlotMonitor` record is created and activated for the preferred slot.
- [ ] Patient sees a confirmation: `"We'll automatically move your appointment to [date/time] if it becomes available."`.
- [ ] Patient can cancel the preferred slot designation from their profile at any time.
- [ ] Cancelling the preference deactivates the `SlotMonitor` without cancelling the original booking.

**Definition of Done**:
- [ ] `POST /bookings` accepts optional `preferredSlotId` field.
- [ ] `DELETE /bookings/{id}/preferred-slot` deactivates the monitor.
- [ ] Unit test: preferred slot stored, SlotMonitor record created with active=true.

---

### US-020: System Auto-Executes Preferred Slot Swap

**Epic**: EP-005
**Role**: System
**Mapped FRs**: FR-017, FR-018, FR-019
**Mapped UCs**: UC-015, UC-016

**Story**:
As a **patient**, I want my booking to be automatically moved to my preferred slot when it becomes available, so that I get my preferred appointment time without any manual intervention.

**Acceptance Criteria**:
- [ ] When any slot is freed (cancellation or rebooking), the system checks for active `SlotMonitor` records targeting that slot.
- [ ] If a matching monitor exists, an ACID swap transaction executes:
  - Old slot → `Available`
  - New (preferred) slot → `Booked`
  - `Booking.slotId` updated to preferred slot
  - `SlotMonitor.active` → `false`
- [ ] FIFO tie-breaking: if multiple patients prefer the same slot, the first monitor registered wins.
- [ ] Patient receives an email + SMS notification: `"Your appointment has been moved to [new date/time]."` with an updated PDF confirmation.
- [ ] Notifications sent within 60 seconds of the swap execution.
- [ ] If the appointment date passes without the preferred slot opening, the `SlotMonitor` is deactivated automatically and the original booking is preserved.
- [ ] Swap event recorded in audit log.

**Definition of Done**:
- [ ] Background job polls for freed slots and executes swap transactions.
- [ ] ACID swap transaction unit tested (old slot freed, new slot booked, no orphan states).
- [ ] Race condition test: two monitors for same slot → only one swap executes; second patient notified no swap occurred.
- [ ] Notification delivery tested with Mailpit + Twilio mock.

---

### US-021: Automated Appointment Reminders (SMS + Email)

**Epic**: EP-005
**Role**: System
**Mapped FRs**: FR-020, FR-023
**Mapped UCs**: UC-017

**Story**:
As a **patient**, I want to receive automatic SMS and email reminders before my appointment, so that I don't forget and miss my slot.

**Acceptance Criteria**:
- [ ] Reminders are sent at two configurable intervals: 48 hours before and 2 hours before appointment.
- [ ] Each reminder contains: patient name, appointment date, time, clinic location, and booking reference.
- [ ] Reminders are sent to the patient's registered email (SMTP) and phone number (Twilio SMS).
- [ ] If a patient has opted out of a channel (email or SMS), that channel is skipped — the other channel still delivers.
- [ ] Failed deliveries are retried up to 3 times with exponential back-off; `ReminderSchedule.deliveryStatus` updated accordingly.
- [ ] Reminders are not sent for cancelled appointments.

**Definition of Done**:
- [ ] Background scheduler: processes `ReminderSchedule` rows with `scheduledAt ≤ now` and `deliveryStatus: Pending`.
- [ ] Opt-out flag checked before dispatch.
- [ ] Unit test: opt-out respected — email skipped, SMS sent.
- [ ] Integration test: reminder row processed → verify email + SMS dispatched via Mailpit/Twilio mock.

---

### US-022: Google Calendar Sync on Booking

**Epic**: EP-005
**Role**: Patient
**Mapped FRs**: FR-021, FR-023
**Mapped UCs**: UC-018

**Story**:
As a **patient**, I want my confirmed appointment to be added to my Google Calendar, so that it appears alongside my other commitments.

**Acceptance Criteria**:
- [ ] After booking confirmation, patient is offered: `"Add to Google Calendar"` button.
- [ ] Clicking triggers Google OAuth 2.0 consent flow — patient grants calendar write permission.
- [ ] On consent, system creates a Google Calendar event with: appointment title, date/time, duration, and clinic address.
- [ ] OAuth token is stored encrypted per patient for future syncs.
- [ ] Calendar sync is **non-blocking** — if it fails, booking confirmation is unaffected.
- [ ] Patient can opt out — preference saved; button not shown on future bookings if opted out.
- [ ] On preferred slot swap, the existing Google Calendar event is updated (not duplicated).

**Definition of Done**:
- [ ] `POST /calendar/google/sync` and `POST /auth/google/callback` endpoints.
- [ ] OAuth token stored encrypted in `Patient` record.
- [ ] Integration test: mock Google API → verify calendar event created; verify failure does not roll back booking.

---

### US-023: Outlook Calendar Sync on Booking

**Epic**: EP-005
**Role**: Patient
**Mapped FRs**: FR-022, FR-023
**Mapped UCs**: UC-019

**Story**:
As a **patient**, I want my confirmed appointment to be added to my Outlook Calendar, so that I can track it alongside my work and personal events.

**Acceptance Criteria**:
- [ ] After booking confirmation, patient is offered: `"Add to Outlook Calendar"` button.
- [ ] Clicking triggers Microsoft OAuth 2.0 consent flow via Microsoft Graph API.
- [ ] On consent, system creates an Outlook Calendar event with appointment details.
- [ ] OAuth token stored encrypted per patient.
- [ ] Calendar sync is non-blocking — booking unaffected by sync failure.
- [ ] Patient can opt out; preference saved.

**Definition of Done**:
- [ ] `POST /calendar/outlook/sync` and `POST /auth/microsoft/callback` endpoints.
- [ ] Integration test: mock Microsoft Graph API → verify event created; verify failure does not affect booking.

---

### US-024: Calendar Sync Failure Handled Gracefully

**Epic**: EP-005
**Role**: System
**Mapped FRs**: FR-021, FR-022
**Mapped UCs**: UC-020

**Story**:
As a **patient**, I want to be informed clearly if my calendar sync failed without my booking being affected, so that I know to add the event manually if needed.

**Acceptance Criteria**:
- [ ] If Google/Outlook Calendar API returns an error (timeout, rate limit, auth failure), the error is logged.
- [ ] Booking confirmation flow is **not** rolled back — booking remains confirmed.
- [ ] Patient sees a non-blocking toast: `"Calendar sync failed. You can retry from your profile or add the event manually."`.
- [ ] A "Retry Calendar Sync" button is available on the patient's appointment details page.
- [ ] Sync failure does not block subsequent app actions.

**Definition of Done**:
- [ ] Calendar API calls wrapped in try/catch; failure does not propagate to booking transaction.
- [ ] Unit test: mock API timeout → booking remains Confirmed, error logged, toast content returned in API response.

---

## EP-006 — Staff Queue, Walk-in Management & Dashboards

---

### US-025: Staff Creates Walk-in Booking & Queue Updates in Real Time

**Epic**: EP-006
**Role**: Staff
**Mapped FRs**: FR-024, FR-025, FR-028
**Mapped UCs**: UC-021

**Story**:
As a **staff member**, I want to register a walk-in patient and see the same-day queue update instantly, so that all staff at the clinic have an accurate, live view of the day's patient flow.

**Acceptance Criteria**:
- [ ] "New Walk-in" button visible only to users with `Staff` or `Admin` role — hidden for Patients.
- [ ] Walk-in form: patient name (required), phone (optional), link to existing patient account by name/DOB search (optional).
- [ ] On submission, walk-in booking created with `status: WalkIn` and added to today's queue.
- [ ] All connected staff dashboards receive the new queue entry via SignalR within **5 seconds** — no page refresh required.
- [ ] Staff can optionally toggle "Create Patient Account" to add a system account for the walk-in patient (linked to US-002).
- [ ] Audit log entry: `{ action: "WALKIN_CREATED", actorId, patientId, bookingId }`.

**Definition of Done**:
- [ ] `POST /walkins` endpoint; JWT `Staff` role enforced (Patient JWT → 403).
- [ ] SignalR hub `BroadcastQueueUpdate` tested with 2 connected clients; both receive update within 5s.
- [ ] Integration test: create walk-in → verify booking in DB, queue broadcast received.

---

### US-026: Staff Marks Patient as Arrived

**Epic**: EP-006
**Role**: Staff
**Mapped FRs**: FR-027
**Mapped UCs**: UC-022

**Story**:
As a **staff member**, I want to mark a patient as "Arrived" when they check in at the front desk, so that the clinical team knows who is physically present and ready.

**Acceptance Criteria**:
- [ ] Each patient row in the staff queue has a "Mark Arrived" button.
- [ ] Clicking records `Booking.arrivedAt` timestamp and updates status to `Arrived`.
- [ ] Queue entry updates to show an "Arrived" badge in real time on all connected staff dashboards (SignalR).
- [ ] "Mark Arrived" button is hidden once patient status is `Arrived` — prevents duplicate marking.
- [ ] Staff can search for a patient by name or DOB if they don't appear in the queue (e.g., system booking not found).
- [ ] Audit log entry: `{ action: "PATIENT_ARRIVED", actorId, patientId, bookingId, arrivedAt }`.

**Definition of Done**:
- [ ] `PATCH /bookings/{id}/status { status: "Arrived" }` endpoint; Staff/Admin roles only.
- [ ] SignalR broadcast on arrival: all connected clients receive updated queue entry.
- [ ] Unit test: double-marking same patient returns idempotent response (no duplicate timestamps).

---

### US-027: Real-Time Staff Queue Dashboard

**Epic**: EP-006
**Role**: Staff
**Mapped FRs**: FR-026, FR-028, FR-040
**Mapped UCs**: UC-021, UC-035

**Story**:
As a **staff member**, I want to see a live queue of all today's patients (scheduled and walk-ins) with their status and risk scores, so that I can manage patient flow efficiently without manually refreshing the page.

**Acceptance Criteria**:
- [ ] Queue shows all bookings for today in order: arrival time (walk-ins) or appointment time (scheduled).
- [ ] Each row shows: patient name, booking type (Scheduled / Walk-in), status (Confirmed / Arrived / Walk-in), appointment time, no-show risk badge (colour-coded), and action buttons (Mark Arrived, View Record).
- [ ] Queue updates in real time via SignalR for: new walk-in added, patient marked Arrived, new scheduled booking confirmed.
- [ ] Update latency is ≤ 5 seconds from triggering DB write (NFR-011).
- [ ] Queue is empty-state-safe: shows `"No patients scheduled for today"` if no bookings exist.
- [ ] Filtering: staff can filter queue by status (All / Confirmed / Arrived / Walk-in).

**Definition of Done**:
- [ ] `GET /dashboard/staff` returns today's queue data.
- [ ] SignalR client auto-updates queue table on received events — verified with React Testing Library.
- [ ] E2E test: create walk-in → queue row appears within 5s on second connected staff client.

---

### US-028: Admin Views KPI Metrics Dashboard

**Epic**: EP-006
**Role**: Admin
**Mapped FRs**: FR-041, FR-042
**Mapped UCs**: UC-036

**Story**:
As an **admin**, I want to see real-time platform KPI metrics on my dashboard, so that I can monitor platform health, measure clinical intelligence accuracy, and track patient engagement.

**Acceptance Criteria**:
- [ ] Admin dashboard displays KPI cards: Total Patients Registered, Total Appointments Booked, Current No-Show Rate (%), AI-Human Agreement Rate (%), Total Critical Conflicts Identified.
- [ ] No-Show Rate = `COUNT(no-shows) / COUNT(total appointments)` for the current month.
- [ ] AI-Human Agreement Rate = `COUNT(accepted without correction) / COUNT(total reviewed suggestions)`.
- [ ] Critical Conflicts Identified = total unresolved `ConflictFlag` records.
- [ ] If no data exists (new deployment), metric cards display `"No data yet"` placeholder.
- [ ] Metrics are refreshed on page load; "Refresh" button available for manual reload.

**Definition of Done**:
- [ ] `GET /admin/metrics` endpoint (Admin JWT only) returning all 5 KPI values.
- [ ] Unit test for each metric calculation.
- [ ] UI test: KPI cards render values and empty state placeholder.

---

### US-029: Patient Cannot Self-Check-In

**Epic**: EP-006
**Role**: Patient
**Mapped FRs**: FR-024
**Mapped UCs**: UC-023

**Story**:
As a **security-enforcing system**, I want to prevent patients from creating walk-in bookings or marking themselves as arrived through any patient-facing interface, so that arrival status is only under staff control.

**Acceptance Criteria**:
- [ ] "New Walk-in" button is not rendered in the patient portal at any point.
- [ ] `POST /walkins` with a Patient JWT returns `403 Forbidden`.
- [ ] `PATCH /bookings/{id}/status` with `status: "Arrived"` using a Patient JWT returns `403 Forbidden`.
- [ ] `403` attempt is recorded in audit log: `{ action: "UNAUTHORIZED_ACCESS_ATTEMPT", endpoint: "/walkins" }`.
- [ ] Patient portal shows only their own booking details — no queue management controls visible.

**Definition of Done**:
- [ ] RBAC test: Patient JWT on `/walkins` POST → 403.
- [ ] RBAC test: Patient JWT on `/bookings/{id}/status` PATCH with Arrived → 403.
- [ ] UI test: patient portal DOM contains no walk-in or queue management elements.

---

## EP-007-I — Clinical AI: Document Upload & Extraction

---

### US-030: Patient Uploads a Clinical Document

**Epic**: EP-007-I
**Role**: Patient
**Mapped FRs**: FR-029, FR-031
**Mapped UCs**: UC-024

**Story**:
As a **patient**, I want to securely upload my historical clinical documents (lab reports, discharge summaries, prescriptions), so that my care team has a complete picture of my health history.

**Acceptance Criteria**:
- [ ] Upload widget supports drag-and-drop and file browser selection.
- [ ] Accepted file types: PDF, DOCX, DOC, JPG, PNG (server-side MIME validation).
- [ ] Maximum file size: 20 MB per file (configurable).
- [ ] A SHA-256 hash is computed on upload for integrity verification and duplicate detection.
- [ ] Document is stored with AES-256 encrypted `storagePath` in the database.
- [ ] Upload progress bar shown during upload.
- [ ] Patient can upload multiple files in a single session.
- [ ] Document list shows: filename, upload date, and processing status (Pending / Extracting / Complete / Failed).
- [ ] Audit log entry: `{ action: "DOCUMENT_UPLOADED", patientId, documentId, fileHash }`.

**Definition of Done**:
- [ ] `POST /documents` multipart endpoint with MIME + size + SHA-256 validation.
- [ ] Integration test: upload PDF → verify ClinicalDocument record with encrypted path, status=Pending, hash stored.
- [ ] Duplicate detection test: same file uploaded twice → second upload detected via hash match.

---

### US-031: Document Upload Validation Failure

**Epic**: EP-007-I
**Role**: Patient
**Mapped FRs**: FR-029, FR-030
**Mapped UCs**: UC-025

**Story**:
As a **patient**, I want to receive a clear, specific error when I upload an unsupported or oversized file, so that I know exactly what to fix.

**Acceptance Criteria**:
- [ ] Uploading an unsupported file type (e.g., `.exe`, `.zip`) returns: `"File type not supported. Accepted types: PDF, DOCX, DOC, JPG, PNG."`.
- [ ] Uploading a file exceeding 20 MB returns: `"File exceeds the maximum size of 20 MB."`.
- [ ] Rejected files are never stored — no `ClinicalDocument` record is created.
- [ ] Error message is shown inline next to the failed file in the upload list.
- [ ] Valid files in a multi-file upload are not affected by a single file's failure.
- [ ] Client-side validation mirrors server-side checks for immediate feedback before upload begins.

**Definition of Done**:
- [ ] `POST /documents` returns `422 Unprocessable Entity` with specific error message for each violation.
- [ ] Unit tests: disallowed MIME type, oversized file, valid file unaffected in batch upload.

---

### US-032: AI Extracts & De-duplicates Clinical Data

**Epic**: EP-007-I
**Role**: System
**Mapped FRs**: FR-032, FR-033
**Mapped UCs**: UC-026

**Story**:
As a **staff member**, I want the system to automatically extract structured clinical data from uploaded patient documents, so that I don't need to manually read every document before a patient visit.

**Acceptance Criteria**:
- [ ] Extraction pipeline is triggered automatically after a document upload is validated and saved.
- [ ] Pipeline stages: PdfPig text extraction → 500-token sliding window chunking (50-token overlap) → Ollama embedding → pgvector storage → Ollama entity extraction → JSON schema validation → `ExtractedRecord` INSERT.
- [ ] Extracted entity types: Vital, Medication, Diagnosis, Clinical Note, Allergy.
- [ ] Each `ExtractedRecord` stores: entity type, encrypted value, confidence score, and source chunk reference.
- [ ] De-duplication: identical (entityType + normalised value) combinations across documents are merged; duplicates flagged `isDuplicate=true`.
- [ ] Pipeline completes within **120 seconds** per document.
- [ ] `ClinicalDocument.processingStatus` state machine: `Pending → Extracting → Complete | Failed`.
- [ ] All AI inference via local Ollama — no PHI transmitted externally.

**Definition of Done**:
- [ ] Background extraction job triggered by document upload event.
- [ ] Performance test: 10-page PDF processes within 120s.
- [ ] Unit test: de-duplication merges identical medication entries from two documents.
- [ ] Integration test: uploaded PDF → ExtractedRecord rows in DB with correct entity types and status=Pending.

---

### US-033: Extraction Failure Flagged for Manual Review

**Epic**: EP-007-I
**Role**: Staff
**Mapped FRs**: FR-032
**Mapped UCs**: UC-029

**Story**:
As a **staff member**, I want to be alerted when an AI extraction fails or produces low-confidence results, so that I can manually enter the data and ensure the patient's record is complete.

**Acceptance Criteria**:
- [ ] If AI confidence is below the configured threshold, the document is flagged `processingStatus: Failed` and a staff alert is raised.
- [ ] If the extraction engine returns an error, the pipeline retries once; if the retry fails, status → `Failed`.
- [ ] A notification badge appears in the staff dashboard for documents needing manual review.
- [ ] Staff can open the flagged document and manually enter extracted data via a structured form.
- [ ] Manually entered data is saved as `ExtractedRecord` with `sourceTag: "[MANUAL]"` and the staff member's ID.
- [ ] Audit log entry: `{ action: "MANUAL_EXTRACTION_ENTERED", actorId, documentId }`.

**Definition of Done**:
- [ ] Extraction failure detection: low confidence score (<0.5) and engine error both trigger `Failed` status.
- [ ] `PATCH /documents/{id}/manual-extraction` endpoint (Staff role only).
- [ ] Unit test: mock Ollama returning confidence=0.2 → document flagged Failed, staff alert raised.

---

## EP-007-II — Clinical AI: 360° View, Conflicts & Medical Coding

---

### US-034: Data Conflict Detected and Surfaced to Staff

**Epic**: EP-007-II
**Role**: Staff
**Mapped FRs**: FR-034
**Mapped UCs**: UC-027

**Story**:
As a **staff member**, I want to be immediately alerted to conflicting data across a patient's uploaded documents (e.g., contradictory medications), so that I can resolve the discrepancy before it causes a clinical error.

**Acceptance Criteria**:
- [ ] After extraction, the system compares extracted records of the same `entityType` for contradictions (e.g., two `Medication` records with the same drug name but different doses).
- [ ] Conflict analysis uses Ollama to evaluate conflicting pairs and assign a conflict confidence score.
- [ ] Each detected conflict creates a `ConflictFlag` record with: both conflicting `ExtractedRecord` IDs, explanation, confidence, source document references, and `status: Unresolved`.
- [ ] Active unresolved conflicts are shown prominently at the top of the 360° Patient View with a red banner.
- [ ] Each conflict displays: the two conflicting values, their source documents, and confidence level.
- [ ] Unresolved conflict count is tracked in the admin KPI (`Critical Conflicts Identified`).

**Definition of Done**:
- [ ] Conflict detection job runs after each extraction completes.
- [ ] `ConflictFlag` entity migrated to DB.
- [ ] Unit test: two Medication records with conflicting doses → ConflictFlag created.
- [ ] Integration test: staff 360° view returns conflict flags in response.

---

### US-035: Staff Views 360-Degree Patient View

**Epic**: EP-007-II
**Role**: Staff
**Mapped FRs**: FR-035
**Mapped UCs**: UC-028

**Story**:
As a **staff member**, I want a single consolidated view of all a patient's clinical data with traceability back to source documents, so that I can prepare for a visit in 2 minutes instead of 20.

**Acceptance Criteria**:
- [ ] `GET /patients/{id}/view` returns a single aggregated response containing: Patient demographics, IntakeRecord data, ExtractedRecords grouped by entity type (Vitals, Medications, Diagnoses, Notes, Allergies), ConflictFlags, MedicalCodeSuggestions (Pending), and ClinicalDocuments list.
- [ ] Each extracted data point displays a `Source:` link showing the originating document name and upload date.
- [ ] Active conflict flags appear at the top with red banners.
- [ ] Data is de-duplicated — no repeated entries for the same value from multiple documents.
- [ ] If no documents have been uploaded/processed: `"No clinical data available. Patient has not uploaded documents."` displayed.
- [ ] View is accessible to `Staff` and `Admin` roles only — Patient JWT returns `403`.
- [ ] Audit log entry: `{ action: "PATIENT_DATA_ACCESSED", actorId, patientId }`.

**Definition of Done**:
- [ ] `GET /patients/{id}/view` endpoint; Staff/Admin roles enforced.
- [ ] Integration test: upload 2 documents → process → verify 360° view aggregates both, de-duplicates, and shows source links.
- [ ] Performance test: 360° view response < 3 seconds for patient with 10 documents.

---

### US-036: AI Suggests ICD-10 Diagnosis Codes

**Epic**: EP-007-II
**Role**: Staff
**Mapped FRs**: FR-036, FR-038
**Mapped UCs**: UC-030

**Story**:
As a **staff member**, I want the system to suggest ICD-10 diagnosis codes derived from the patient's aggregated clinical data with source evidence, so that I can verify and finalize codes in minutes rather than looking them up manually.

**Acceptance Criteria**:
- [ ] After extraction is complete, the RAG code suggestion pipeline runs: clinical summary embedding → pgvector TOP-20 retrieval → Ollama ICD-10 suggestion prompt.
- [ ] Each suggestion includes: ICD-10 code value, description, AI confidence score, and source chunk reference(s).
- [ ] ICD-10 format validated: `[A-Z][0-9]{2}\.?[0-9]{0,4}` — malformed codes are rejected before storage.
- [ ] Suggestions are stored as `MedicalCodeSuggestion` with `codeType: ICD10`, `reviewStatus: Pending`.
- [ ] If no diagnosable conditions are found, `"No ICD-10 codes suggested — manual coding required."` is shown.
- [ ] Staff can see all Pending ICD-10 suggestions in the Code Review tab of the patient record.

**Definition of Done**:
- [ ] RAG pipeline for ICD-10 tested with 5 representative clinical scenarios.
- [ ] ICD-10 format validation unit tested.
- [ ] Integration test: processed document → verify MedicalCodeSuggestion rows with codeType=ICD10, status=Pending.

---

### US-037: AI Suggests CPT Procedure Codes

**Epic**: EP-007-II
**Role**: Staff
**Mapped FRs**: FR-037, FR-038
**Mapped UCs**: UC-030

**Story**:
As a **staff member**, I want the system to suggest CPT procedure codes based on the patient's clinical notes and aggregated data, so that I can complete medical coding accurately and quickly.

**Acceptance Criteria**:
- [ ] After extraction, the RAG pipeline generates CPT code suggestions from clinical notes and procedure-relevant extracted data.
- [ ] Each suggestion includes: CPT code value (5-digit numeric), description, confidence, and source chunk reference(s).
- [ ] CPT format validated: exactly 5 numeric digits — malformed codes rejected.
- [ ] Suggestions stored as `MedicalCodeSuggestion` with `codeType: CPT`, `reviewStatus: Pending`.
- [ ] CPT suggestions displayed alongside ICD-10 suggestions in the Code Review tab, grouped by type.

**Definition of Done**:
- [ ] CPT format validation unit tested.
- [ ] RAG pipeline tested with 3 clinical scenarios containing procedure documentation.
- [ ] Integration test: verify CPT suggestions created with correct format and status=Pending.

---

### US-038: Staff Approves AI Code Suggestion

**Epic**: EP-007-II
**Role**: Staff
**Mapped FRs**: FR-038
**Mapped UCs**: UC-031

**Story**:
As a **staff member**, I want to explicitly approve each AI-suggested medical code, so that only clinically verified codes are finalized in the patient record.

**Acceptance Criteria**:
- [ ] Code Review tab shows all Pending suggestions grouped by ICD-10 and CPT, each with its supporting source evidence link.
- [ ] Each suggestion has an "Approve" button.
- [ ] Approving a suggestion sets `reviewStatus: Accepted`, records `reviewedBy` (staff ID) and `reviewedAt` timestamp.
- [ ] Approved codes are moved to a "Finalized Codes" section — no longer in the pending review list.
- [ ] "Approve All" bulk action is available to finalize all pending suggestions at once.
- [ ] AI-Human Agreement Rate KPI is incremented for each approval.
- [ ] Audit log entry: `{ action: "CODE_SUGGESTION_REVIEWED", actorId, suggestionId, action: "Accepted" }`.
- [ ] No code can reach `Accepted` status without an explicit staff `PATCH` request — no background auto-approval exists.

**Definition of Done**:
- [ ] `PATCH /suggestions/{id} { action: "accept" }` endpoint; Staff/Admin roles.
- [ ] Unit test: auto-approval background job does not exist (architecture test).
- [ ] Integration test: approve suggestion → verify reviewStatus=Accepted, reviewedBy=staffId, KPI incremented.

---

### US-039: Staff Rejects or Corrects an AI Code Suggestion

**Epic**: EP-007-II
**Role**: Staff
**Mapped FRs**: FR-038
**Mapped UCs**: UC-032

**Story**:
As a **staff member**, I want to reject an incorrect AI-suggested code and optionally enter the correct code, so that billing accuracy is maintained and AI performance is tracked.

**Acceptance Criteria**:
- [ ] Each pending suggestion has a "Reject" button.
- [ ] Rejecting sets `reviewStatus: Rejected` and removes the suggestion from the pending list.
- [ ] Staff is optionally prompted: `"Would you like to enter a replacement code?"` — this is never mandatory.
- [ ] If a replacement is entered, it is validated (ICD-10 or CPT format) and saved as a new `MedicalCodeSuggestion` with `reviewStatus: Accepted` and the staff member's ID.
- [ ] If no replacement is provided, a note `"Rejected — No replacement provided"` is attached to the rejected record.
- [ ] AI-Human Agreement Rate KPI is decremented/recalculated to reflect the rejection.
- [ ] Audit log entry: `{ action: "CODE_SUGGESTION_REVIEWED", actorId, suggestionId, action: "Rejected", replacementCode (if provided) }`.

**Definition of Done**:
- [ ] `PATCH /suggestions/{id} { action: "reject", replacementCode?: "..." }` endpoint.
- [ ] Unit test: reject without replacement → `reviewStatus=Rejected`, note set.
- [ ] Unit test: reject with replacement → original `Rejected`, new `Accepted` record with staffId.
- [ ] Unit test: AI-Human Agreement Rate recalculated correctly after rejection.

---

## Cross-Cutting Stories

---

### US-040 (Implicit — EP-002): All PHI Encrypted at Rest and in Transit

**Epic**: EP-002
**Role**: System
**Mapped FRs**: FR-045, NFR-005

**Story**:
As a **HIPAA compliance officer**, I want all patient health information to be encrypted with AES-256 at rest and transmitted only over TLS 1.2+, so that the platform meets federal data protection requirements.

**Acceptance Criteria**:
- [ ] All PHI columns (patient demographics, intake data, extracted entities, chunk text, document storage paths) are encrypted via `pgp_sym_encrypt` (pgcrypto AES-256) before persistence.
- [ ] Encryption key injected via Docker environment variable — never committed to source control or hardcoded.
- [ ] Nginx enforces TLS 1.2+ with `ssl_protocols TLSv1.2 TLSv1.3`; HTTP requests redirect to HTTPS.
- [ ] `.NET API` container is not exposed on a public port — all traffic flows through Nginx.
- [ ] `HSTS` header is set in Nginx responses: `Strict-Transport-Security: max-age=31536000`.

**Definition of Done**:
- [ ] Database test: PHI columns contain ciphertext when queried directly (bypassing application decryption).
- [ ] Nginx SSL test: verify TLS 1.2+ enforcement and HSTS header presence.
- [ ] CI check: no connection strings or encryption keys in committed source files.

---
