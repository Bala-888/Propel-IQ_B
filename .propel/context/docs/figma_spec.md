# Figma Design Specification - Unified Patient Access & Clinical Intelligence Platform

## 1. Figma Specification

**Platform**: Responsive Web — 375px (mobile) / 768px (tablet) / 1280px (laptop) / 1440px (desktop)

---

## 2. Source References

### Primary Source

| Document | Path | Purpose |
|----------|------|---------|
| Epics | .propel/context/docs/epics.md | Epics with UI impact flags, key deliverables, personas |
| Requirements Spec | .propel/context/docs/spec.md | Actors, 38 use cases, 45 functional requirements |

### Optional Sources

| Document | Path | Purpose |
|----------|------|---------|
| Wireframes | N/A — not available | Not applicable for this project phase |
| Design Assets | N/A — green-field, no existing assets | No visual references available |

### Related Documents

| Document | Path | Purpose |
|----------|------|---------|
| Design System | .propel/context/docs/designsystem.md | Tokens, branding, component specifications |

---

## 3. UX Requirements

*Generated from use cases and epics with UI impact. All requirements apply to the screen implementations defined in Section 6.*

### UXR Requirements Table

| UXR-ID | Category | Requirement | Acceptance Criteria | Screens Affected | Basis |
|--------|----------|-------------|---------------------|------------------|-------|
| UXR-101 | Usability | [SOURCE:INPUT] System MUST allow all primary patient tasks (intake, booking, document upload) to be reachable within 3 navigation steps from the Patient Home Dashboard. | Navigation depth audit: ≤3 clicks from SCR-003 to any patient primary task screen. | SCR-003, SCR-004, SCR-005, SCR-006, SCR-009 | UX discoverability principle; patient personas are non-technical and may be under stress in a healthcare context. |
| UXR-102 | Usability | [SOURCE:INPUT] System MUST route each authenticated role to a role-appropriate landing screen immediately after login, without requiring additional navigation. | Patient→SCR-003, Staff→SCR-011, Admin→SCR-016; no intermediate disambiguation screen. | SCR-001 | FR-003: "route each role to a role-appropriate UI upon successful authentication." |
| UXR-103 | Usability | [SOURCE:INPUT] System MUST preserve all field values entered in either intake mode (AI or manual) when a user switches modes mid-session, with zero data loss. | Switch AI→Manual: all AI-collected values pre-populated in corresponding form fields. Switch Manual→AI: all entered values represented in AI summary context. | SCR-004, SCR-005 | FR-009: "switch between AI conversational intake and manual form mode at any point during the intake session without losing previously entered data." |
| UXR-104 | Usability | [SOURCE:INPUT] System MUST surface the complete 360° patient view to staff in a single viewport accessible within 2 minutes from the staff queue. | Staff can navigate SCR-011→SCR-013→SCR-014 in ≤4 interactions; SCR-014 loads aggregated data within 3s (P95). | SCR-011, SCR-013, SCR-014 | FR-035 + spec success criterion: "360-degree view accessible within 2 minutes." |
| UXR-105 | Usability | [SOURCE:INPUT] Appointment slot calendar MUST distinguish Available, Booked, and Preferred-Designated slot states using shape + pattern + label, not colour alone. | Slots pass WCAG 1.4.1 (Use of Colour): state identifiable without colour (e.g., in grayscale rendering). | SCR-006 | WCAG 2.2 AA SC 1.4.1; FR-011, FR-016 require simultaneous state display. |
| UXR-106 | Usability | [SOURCE:INPUT] Staff MUST be able to execute "Mark Arrived" for any patient directly from the queue table row without navigating away from the queue dashboard. | "Mark Arrived" is a visible inline action on each SCR-011 row; no modal or separate page required. | SCR-011 | UC-022 workflow efficiency; staff personas are time-pressured with a live queue. |
| UXR-201 | Accessibility | [SOURCE:INPUT] All text content MUST meet WCAG 2.2 AA colour contrast requirements: ≥4.5:1 for normal text (<18px regular or <14px bold), ≥3:1 for large text (≥18px regular or ≥14px bold) and UI component boundaries. | Automated axe-core scan passes all contrast checks on every screen. | All screens | Elicitation decision: WCAG 2.2 AA confirmed as compliance target. |
| UXR-202 | Accessibility | [SOURCE:INPUT] All interactive forms MUST be fully keyboard-navigable with a logical tab order; no keyboard trap is permitted on any screen. | Tab sequence matches visual reading order; no focus trap exists (Escape exits modals); all actions executable without a mouse. | SCR-001, SCR-002, SCR-004, SCR-005, SCR-006, SCR-012, SCR-017 and all modals | WCAG 2.2 AA SC 2.1.1, SC 2.1.2. |
| UXR-203 | Accessibility | [SOURCE:INPUT] All interactive elements MUST display a visible focus indicator that meets WCAG 2.2 AA Focus Appearance (SC 2.4.11): minimum 2px outline with ≥3:1 contrast against adjacent colour. | Focus ring visible in all states; no outline:none override without a replacement focus style. | All screens | WCAG 2.2 AA SC 2.4.11 (new in 2.2, elicitation confirmed). |
| UXR-204 | Accessibility | [SOURCE:INPUT] All form inputs MUST have a programmatically associated visible label; placeholder text MUST NOT serve as the sole label for any input. | All `<input>` and `<textarea>` elements have associated `<label>` with matching `htmlFor`/`id`; placeholders are supplementary only. | SCR-001, SCR-002, SCR-004, SCR-005, SCR-006, SCR-012, SCR-017 | WCAG 2.2 AA SC 1.3.1, SC 3.3.2. |
| UXR-205 | Accessibility | [SOURCE:INPUT] All interactive touch targets MUST be a minimum 44×44 CSS px on the 375px mobile breakpoint. | Tap target bounding box ≥44×44px; verified via browser DevTools for all buttons, links, toggles, and table row actions. | All screens at 375px | WCAG 2.2 AA SC 2.5.8; elicitation confirmed WCAG 2.2 AA. |
| UXR-206 | Accessibility | [SOURCE:INPUT] All dynamic content changes (queue row additions, toast notifications, inline form validation errors) MUST be announced to screen readers via appropriate ARIA live regions. | `role="alert"` or `aria-live="polite"` applied to: toast container, inline error regions, queue update indicator; NVDA/VoiceOver announces changes. | SCR-005, SCR-009, SCR-011, all toast-bearing screens | WCAG 2.2 AA SC 4.1.3; SignalR queue updates require live region support. |
| UXR-207 | Accessibility | [SOURCE:INPUT] All meaningful icons, status badges, and clinical indicators (AI confidence, risk score, conflict severity) MUST provide a text alternative accessible to screen readers. | All icon-only buttons have `aria-label`; decorative icons have `aria-hidden="true"`; status badges have visually hidden text equivalents. | SCR-011, SCR-014, SCR-015, all status-bearing screens | WCAG 2.2 AA SC 1.1.1. |
| UXR-301 | Responsiveness | [SOURCE:INPUT] All screens MUST render without horizontal scroll or content overflow at 375px, 768px, 1280px, and 1440px viewport widths. | Zero horizontal scrollbar at all four breakpoints; content reflows without clipping or overflow:hidden masking. | All 17 screens | Elicitation Gap 1: fully responsive confirmed for all screens. |
| UXR-302 | Responsiveness | [SOURCE:INPUT] Navigation MUST adapt by breakpoint: persistent sidebar (≥1280px), collapsible top header nav (768px), bottom tab bar (375px). | Visual inspection at each breakpoint confirms correct navigation component rendered; no dual-nav rendering. | All authenticated screens | Elicitation Gap 1 (fully responsive); responsive navigation pattern standard for web healthcare apps. |
| UXR-303 | Responsiveness | [SOURCE:INPUT] Document upload drag-and-drop zone MUST degrade to a standard file input button on the 375px mobile breakpoint. | At 375px: drop zone shows "Select file" button only; no drag instruction visible; file input triggers native file picker. | SCR-009 | Elicitation Gap 1; drag events are unreliable on mobile touch interfaces. |
| UXR-304 | Responsiveness | [SOURCE:INPUT] Data tables (queue dashboard, admin user list, medical code review) MUST reflow to a card-list layout on the 375px mobile breakpoint. | At 375px: `<table>` is replaced by stacked card components; all table data accessible without horizontal scroll. | SCR-011, SCR-015, SCR-017 | Elicitation Gap 1; table overflow is unusable on narrow mobile viewports. |
| UXR-401 | Visual Design | [SOURCE:INPUT] All UI colour values MUST reference design system semantic tokens; hard-coded hex values are prohibited in component implementations. | Zero hard-coded hex/rgb/hsl values in component stylesheets; all colours traceable to a token name in designsystem.md. | All screens | DRY design system principle; template authority; ensures token-driven theming. |
| UXR-402 | Visual Design | [SOURCE:INPUT] All AI-generated content (intake summaries, extracted clinical entities, code suggestions) MUST carry a distinct AILabel component using the AI-accent (indigo-500) colour token + "AI" icon to identify AI origin. | Every AI-sourced data field/card displays AILabel; lab inspection confirms no AI content surfaced without attribution. | SCR-004, SCR-014, SCR-015 | FR-035 Trust-First principle; AIR-007 human-in-loop requires clear AI attribution before staff decision. |
| UXR-403 | Visual Design | [SOURCE:INPUT] All clinical risk indicators (conflict flags, no-show risk scores) MUST convey severity using colour + icon + text label — never colour alone. | ConflictBanner and RiskScoreBadge display icon + text label at all severity levels; grayscale rendering still communicates severity. | SCR-011, SCR-014 | WCAG 2.2 AA SC 1.4.1; FR-034 conflict surfacing; FR-014 risk score surfacing. |
| UXR-404 | Visual Design | [SOURCE:INPUT] Destructive and irreversible actions (deactivate user, reject code suggestion, resolve conflict with override) MUST use a destructive-variant button style and require a ConfirmDialog before execution. | Destructive buttons use `variant="destructive"` (red-600); ConfirmDialog appears before any irreversible action completes. | SCR-015, SCR-017, MOD-005 | UX safety pattern; FR-038 requires explicit human decision before code finalisation. |
| UXR-501 | Interaction | [SOURCE:INPUT] All interactive elements MUST provide visible state feedback (hover, active, loading) within 200ms of user interaction. | Lighthouse Interaction to Next Paint (INP) ≤200ms on P75; button loading state visible within one animation frame of click. | All screens | Template authority (200ms threshold); UX standard for perceived responsiveness. |
| UXR-502 | Interaction | [SOURCE:INPUT] SignalR-pushed queue row additions and status changes MUST be highlighted with a transient 2s background-color transition without interrupting any active staff interaction or scrolled position. | New/updated rows show a highlight fade animation (indigo-50 → transparent, 2s); scroll position does not jump; no row reorder during active interaction. | SCR-011 | NFR-011 5s push latency; staff must see updates without losing workflow context. |
| UXR-503 | Interaction | [SOURCE:INPUT] Document upload MUST display a real-time upload progress indicator (percentage bar) during file transmission and a polled processing status (Pending → Extracting → Complete / Failed) without requiring a page reload. | Progress bar updates at ≥10% increments during upload; processing status polling interval ≤5s; status transitions reflected without full page reload. | SCR-009, SCR-010 | FR-032 background extraction; UC-026; Ollama processing up to 120s (AIR-008) requires async status UI. |
| UXR-504 | Interaction | [SOURCE:INPUT] The AI conversational intake interface MUST display a typing indicator animation while awaiting an LLM inference response from Ollama. | Typing indicator (three-dot pulse) visible within 200ms of user message send; disappears when AI response arrives; LLM timeout ≥30s. | SCR-004 | UC-007; Ollama CPU inference latency 2–15s creates perceived dead states without feedback. |
| UXR-505 | Interaction | [SOURCE:INPUT] A session timeout warning modal MUST appear 2 minutes before the 15-minute inactivity limit expires, offering "Stay signed in" and "Sign out" options. | Modal appears at 13:00 of 15-minute inactivity timer; "Stay signed in" resets timer; "Sign out" terminates session immediately. | MOD-001, all authenticated screens | FR-005: 15-minute session timeout; UC-005: session expiry flow. |
| UXR-601 | Error Handling | [SOURCE:INPUT] All form field validation errors MUST be displayed inline immediately below the affected input with a specific, actionable message; a global summary banner MUST NOT replace per-field inline errors. | Each invalid field shows an `InlineError` component below the input; no error message is only surfaced in a banner. | SCR-001, SCR-002, SCR-005, SCR-012, SCR-017 | WCAG 2.2 AA SC 3.3.1, SC 3.3.3; clinical-professional tone requires factual, precise error communication. |
| UXR-602 | Error Handling | [SOURCE:INPUT] When a booking conflict (409) occurs, the system MUST display the 3 nearest alternative available slots inline within the booking calendar view without navigating away from SCR-006. | 409 response triggers inline alternative-slot panel in SCR-006; 3 slots shown with date/time; patient can select without re-entering the booking flow. | SCR-006 | UC-012: "alternative slot suggestions when booked concurrently"; FR-015 conflict error handling. |
| UXR-603 | Error Handling | [SOURCE:INPUT] A document upload rejection MUST display the specific rejection reason (file type not permitted / file exceeds 20 MB size limit), list permitted file types, and offer a "Try again" action. | Rejection message names the specific reason; lists PDF, DOC, DOCX, JPG, PNG and "max 20 MB"; retry action clears the rejected file from the queue. | SCR-009 | UC-025; FR-030: "returning a specific error message for rejected files." |
| UXR-604 | Error Handling | [SOURCE:INPUT] Insurance pre-check soft warnings MUST use an amber AlertBanner (not red error styling) and MUST NOT block booking completion. | Soft-warning uses `variant="warning"` (amber-600) AlertBanner; "Continue Booking" action remains enabled; no blocking modal. | SCR-006 | FR-039: "soft validation ... displaying a warning (not a blocking error) if no match is found." |
| UXR-605 | Error Handling | [SOURCE:INPUT] Calendar sync failures (Google or Outlook) MUST NOT block booking confirmation; they MUST surface as a dismissable info-level Toast notification only. | Sync failure shows `Toast variant="info"`; booking confirmation screen (SCR-007) remains fully accessible; no error modal blocks progress. | SCR-007 | FR-021/022: "non-blocking on failure (booking not rolled back)"; UC-020. |

### UXR Categories

- **Usability** (UXR-1XX): Navigation depth, role routing, data preservation, workflow efficiency
- **Accessibility** (UXR-2XX): WCAG 2.2 AA contrast, keyboard navigation, focus indicators, labels, touch targets, ARIA live regions, alt text
- **Responsiveness** (UXR-3XX): Viewport-specific layout, adaptive navigation, component degradation
- **Visual Design** (UXR-4XX): Token enforcement, AI attribution, clinical severity indicators, destructive affordances
- **Interaction** (UXR-5XX): 200ms response, SignalR row animations, async upload status, AI typing indicator, session timeout
- **Error Handling** (UXR-6XX): Inline validation, booking conflict alternatives, upload rejection, insurance soft warning, calendar sync failure

---

## 4. Personas Summary

*Derived from spec.md Actors & System Boundary section. Reference only.*

| Persona | Role | Primary Goals | Key Screens |
|---------|------|---------------|-------------|
| Patient | Authenticated healthcare patient (new or returning) | Register quickly; complete intake without staff assistance; book the right appointment; track appointment status; upload medical documents | SCR-001, SCR-002, SCR-003, SCR-004, SCR-005, SCR-006, SCR-007, SCR-008, SCR-009, SCR-010 |
| Staff / Front Desk | Healthcare front desk or call center operator | Create walk-in bookings efficiently; manage same-day queue in real time; review AI-consolidated patient data within 2 minutes; finalize ICD-10/CPT code suggestions | SCR-001, SCR-011, SCR-012, SCR-013, SCR-014, SCR-015 |
| Admin | Platform administrator | Create and manage user accounts; assign roles; monitor platform KPI metrics | SCR-001, SCR-016, SCR-017 |

---

## 5. Information Architecture

### Site Map

```text
Unified Patient Access & Clinical Intelligence Platform
+-- [Unauthenticated]
|   +-- Login (SCR-001)
|   +-- Patient Registration (SCR-002)
|
+-- [Patient Portal] (post-login: Patient role)
|   +-- Home Dashboard (SCR-003)
|   +-- Intake
|   |   +-- AI Conversational Intake (SCR-004)
|   |   +-- Manual Intake Form (SCR-005)
|   +-- Appointments
|   |   +-- Slot Calendar & Booking (SCR-006)
|   |   +-- Booking Confirmation & Calendar Sync (SCR-007)
|   +-- Documents
|   |   +-- Document Upload (SCR-009)
|   |   +-- Document Processing Status (SCR-010)
|   +-- Profile & Settings (SCR-008)
|
+-- [Staff Dashboard] (post-login: Staff role)
|   +-- Queue Dashboard (SCR-011)
|   +-- Walk-in Booking Form (SCR-012)
|   +-- Patient Search (SCR-013)
|   +-- 360 Patient View (SCR-014)
|   +-- Medical Code Review (SCR-015)
|
+-- [Admin Console] (post-login: Admin role)
    +-- KPI Dashboard (SCR-016)
    +-- User Management (SCR-017)
```

### Navigation Patterns

| Pattern | Type | Platform Behaviour |
|---------|------|--------------------|
| Primary Nav (≥1280px) | Persistent left sidebar (240px wide) | Role-scoped nav links; collapses to icon-only at 1280px if viewport narrows |
| Primary Nav (768px) | Collapsible top header nav with hamburger | Expands to full-width overlay drawer on tap |
| Primary Nav (375px) | Bottom tab bar | Maximum 5 tabs; labels visible; active tab highlighted with primary colour underline |
| Secondary Nav | Breadcrumb (≥768px) | Appears on deep screens (SCR-014, SCR-015); not on top-level dashboards |
| Utility Nav | User avatar menu (top-right header) | Role label, Settings link, Sign Out |
| In-page Nav | Tabs (SCR-014 360° View sections) | Sticky tab bar within page: Demographics / Intake / Documents / Extracted / Codes |

---

## 6. Screen Inventory

*All screens derived from use cases in spec.md.*

### Screen List

| Screen ID | Screen Name | Derived From | Personas Covered | States Required |
|-----------|-------------|--------------|------------------|-----------------|
| SCR-001 | Login | UC-003, UC-004, UC-005, UC-038 | Patient, Staff, Admin | Default, Loading, Error, Validation, Lockout |
| SCR-002 | Patient Registration | UC-001 | Patient | Default, Loading, Error, Validation |
| SCR-003 | Patient Home Dashboard | UC-003 (post-login), UC-016 | Patient | Default, Loading, Empty |
| SCR-004 | AI Conversational Intake | UC-007, UC-009 | Patient | Default, Loading (typing), Empty, Error (Ollama unavailable) |
| SCR-005 | Manual Intake Form | UC-008, UC-009, UC-010 | Patient | Default, Loading, Error, Validation |
| SCR-006 | Appointment Slot Calendar & Booking | UC-011, UC-012, UC-014, UC-033, UC-034 | Patient | Default, Loading, Empty, Error, Conflict (409) |
| SCR-007 | Booking Confirmation & Calendar Sync | UC-013, UC-015, UC-018, UC-019, UC-020 | Patient | Default, Loading, Error |
| SCR-008 | Patient Profile & Settings | UC-023, FR-023 | Patient | Default, Loading, Error, Validation |
| SCR-009 | Document Upload | UC-024, UC-025 | Patient | Default, Loading, Empty, Error, Validation |
| SCR-010 | Document Processing Status | UC-026, UC-029 | Patient | Default, Loading, Empty, Error |
| SCR-011 | Staff Queue Dashboard | UC-021, UC-022, UC-035 | Staff | Default, Loading, Empty, Error (SignalR disconnect) |
| SCR-012 | Walk-in Booking Form | UC-002, UC-021 | Staff | Default, Loading, Error, Validation |
| SCR-013 | Patient Search | UC-028 (precondition) | Staff | Default, Loading, Empty, Error |
| SCR-014 | 360° Patient View | UC-027, UC-028, UC-029 | Staff | Default, Loading, Empty, Error |
| SCR-015 | Medical Code Review | UC-030, UC-031, UC-032 | Staff | Default, Loading, Empty, Error, Validation |
| SCR-016 | Admin KPI Dashboard | UC-036 | Admin | Default, Loading, Empty, Error |
| SCR-017 | Admin User Management | UC-006, UC-036 | Admin | Default, Loading, Empty, Error, Validation |

### Screen-to-Persona Coverage Matrix

| Screen | Patient | Staff | Admin | Notes |
|--------|---------|-------|-------|-------|
| SCR-001 | Primary | Primary | Primary | Shared entry point for all roles |
| SCR-002 | Primary | — | — | Patient-exclusive registration |
| SCR-003 | Primary | — | — | Patient portal home |
| SCR-004 | Primary | — | — | AI intake — patient-exclusive |
| SCR-005 | Primary | — | — | Manual intake — patient-exclusive |
| SCR-006 | Primary | — | — | Booking — patient-exclusive |
| SCR-007 | Primary | — | — | Confirmation — patient-exclusive |
| SCR-008 | Primary | — | — | Profile settings — patient-exclusive |
| SCR-009 | Primary | — | — | Document upload — patient-exclusive |
| SCR-010 | Primary | — | — | Processing status — patient-exclusive |
| SCR-011 | — | Primary | — | Queue dashboard — staff-exclusive |
| SCR-012 | — | Primary | — | Walk-in form — staff-exclusive |
| SCR-013 | — | Primary | — | Patient search — staff-exclusive |
| SCR-014 | — | Primary | — | 360° view — staff-exclusive |
| SCR-015 | — | Primary | — | Code review — staff-exclusive |
| SCR-016 | — | — | Primary | KPI dashboard — admin-exclusive |
| SCR-017 | — | Secondary | Primary | Admin manages; staff read-only access via FR-041 |

### Modal/Overlay Inventory

| Name | Type | Trigger | Parent Screen(s) |
|------|------|---------|-----------------|
| MOD-001 Session Timeout Warning | Modal | 2 min before 15-min inactivity | All authenticated screens |
| MOD-002 Booking Confirmation | Dialog | "Confirm Booking" button in SCR-006 | SCR-006 |
| MOD-003 Preferred Slot Selection | Inline panel (accordion) | "Add preferred slot" link in SCR-006 | SCR-006 |
| MOD-004 Walk-in Account Creation | Optional section toggle | "Create account for patient" toggle in SCR-012 | SCR-012 |
| MOD-005 Resolve Conflict Drawer | Right-side Drawer | "Resolve" button on ConflictBanner in SCR-014 | SCR-014 |
| MOD-006 Create / Edit User | Modal | "New User" / "Edit" action in SCR-017 | SCR-017 |

---

## 7. Content & Tone

### Voice & Tone

- **Overall Tone**: Clinical-professional — precise, minimal, authoritative; language mirrors healthcare terminology without condescension.
- **Error Messages**: Factual and specific ("Authentication failed. Check your credentials and try again."); never vague ("Something went wrong.").
- **Empty States**: Instructional and action-oriented ("No appointments scheduled. Book your first appointment."); no decorative illustrations.
- **Success Messages**: Brief and confirmatory ("Appointment confirmed. Reference: BK-2026-00142."); immediate next-action pointer.
- **AI Outputs**: Clearly attributed ("AI-suggested"); tone is informational, not directive ("Suggestion: ICD-10 J18.9 — confirm or correct below.").

### Content Guidelines

- **Headings**: Sentence case throughout (e.g., "Appointment slot calendar", not "Appointment Slot Calendar").
- **CTAs**: Action-verb first, specific ("Book slot", "Upload document", "Mark arrived", "Accept suggestion").
- **Labels**: Concise noun phrases ("Date of birth", "Insurance provider", "Chief complaint").
- **Placeholder Text**: Meaningful examples only ("e.g., john.smith@email.com", "e.g., Aetna"); not "Enter text here".
- **Clinical Terms**: Use standard terminology (ICD-10, CPT, vitals, medications, diagnoses); no consumer euphemisms.
- **Dates/Times**: ISO-adjacent display ("17 May 2026, 09:30 AM"); consistent across all screens.

---

## 8. Data & Edge Cases

### Data Scenarios

| Scenario | Description | Handling |
|----------|-------------|----------|
| No data — Patient Home | New patient with no bookings or intake | Empty state: "Your appointments will appear here. Book your first appointment." with primary CTA |
| No data — Staff Queue | No patients scheduled or walked in today | Empty state: "No patients in queue today." neutral message, no CTA needed |
| No data — 360° View | Patient has not uploaded documents or AI extraction pending | Empty state per section tab: "No clinical data extracted yet. Upload documents to begin." |
| No data — Code Review | No pending code suggestions (all reviewed or none generated) | Empty state: "All code suggestions reviewed." with a success icon |
| First-use patient | New registrant immediately after account creation | SCR-003 empty state guides to intake → booking sequence |
| Large queue | 50+ patients in staff queue | DataTable pagination (25/page); "Load more" at bottom; SignalR updates apply to current page only |
| Long text — patient name | Names > 40 chars | Truncate at 35 chars with ellipsis; full name in tooltip on hover/focus |
| Slow AI inference | Ollama CPU inference > 15s | UXR-504 typing indicator; 120s hard timeout with error message offering manual entry |
| Slow network | API responses > 3s (P95 breach) | Skeleton screens on all data-bearing screens; no blank white flash |
| Offline / network loss | Browser loses connectivity mid-session | AlertBanner: "Connection lost. Reconnecting…" (non-blocking); SignalR hub auto-reconnect attempt every 5s |
| Session timeout | 15 min inactivity reached | MOD-001 at 13 min; if expired, redirect to SCR-001 with message "Your session expired. Sign in again." |
| Concurrent booking | Two patients book same slot simultaneously | 409 + UXR-602: inline panel with 3 alternative slots; original booking attempt rolled back |

### Edge Cases

| Case | Screen(s) Affected | Solution |
|------|-------------------|----------|
| Long patient name (>40 chars) | SCR-011, SCR-013, SCR-014 | Truncate at 35 chars + tooltip |
| Missing avatar/profile image | SCR-011, SCR-013, SCR-014 | Initials-based avatar fallback (first + last name initials) |
| Very long clinical note (>2000 chars) | SCR-014 | Collapsible section: show first 200 chars, "Show more" toggle |
| Duplicate insurance pre-check warning + field error | SCR-006 | Warning banner above form; field-level inline error below; both displayed simultaneously with distinct visual treatment |
| Document extraction partially failed | SCR-010, SCR-014 | Per-document status: "Partially extracted" with count of successful entities; "Retry extraction" CTA |
| Medical code correction input | SCR-015 | Inline text field with ICD-10/CPT format validation mask; validation error shown before save |
| Admin deactivates own account | SCR-017 | Action blocked with inline error: "You cannot deactivate your own account." |
| All 5 login failure attempts reached | SCR-001 | Lockout state: "Account temporarily locked. Try again in 15 minutes." rate-limit warning; no credential hint |

---

## 9. Branding & Visual Direction

*See designsystem.md for all design tokens (colours, typography, spacing, shadows, etc.)*

### Aesthetic Direction

- **Direction**: Utilitarian `[SOURCE:INPUT]`
- **Rationale**: The platform serves three high-stakes personas — patients navigating healthcare anxiety, staff under time pressure making clinical decisions, and admins governing regulated data. A utilitarian direction prioritises information density, unambiguous hierarchy, and functional clarity above all aesthetic expression. Every UI element earns its place through utility; colour is reserved exclusively for semantic signalling (status, severity, AI origin). This is not a brand-building exercise — it is a clinical operations tool that must communicate trust through precision rather than warmth. `[SOURCE:INPUT]`
- **Precedents**: Epic Systems EMR (information density, role-scoped dashboards), NHS Digital Design System (accessible, minimal, government-grade trust), Linear (functional data surfaces without decorative noise) `[SOURCE:INPUT]`
- **Anti-brief**: Must not resemble a wellness or telehealth consumer app (pastel palettes, rounded illustrative cards, animated gradients); must not resemble a hospital marketing website (stock photography, serif display typefaces, hero banners); must not resemble a consumer SaaS onboarding flow (emoji-heavy CTAs, purple-to-blue gradients, Intercom-style bubbles). `[SOURCE:INPUT]`
- **Basis**: Direction derived from domain (HIPAA-regulated healthcare), three time-pressured personas, elicited clinical-professional content tone, and the absence of any brand guidelines in the project input. `[SOURCE:INPUT]`

### Branding Assets

- **Logo**: Not defined in project input — placeholder: text wordmark "UPACIP" in IBM Plex Sans SemiBold, colour-primary token.
- **Icon Style**: Outlined (Phosphor Icons or Heroicons Outline set — open license); 20px/24px sizing; stroke-width 1.5px.
- **Illustration Style**: None — utilitarian direction excludes decorative illustration; empty states use icon + text only.
- **Photography Style**: Not applicable — no photography in clinical operations tool.

---

## 10. Component Specifications

*Full token definitions and component specs are in designsystem.md. Requirements per screen listed below.*

### Component Library Reference

**Source**: .propel/context/docs/designsystem.md (Component Specifications section)

### Required Components per Screen

| Screen ID | Components Required | Notes |
|-----------|---------------------|-------|
| SCR-001 | TextField (2), PasswordField (1), Button-primary (1), Button-ghost (1), Link (1), InlineError (2), AlertBanner (1) | Login form; lockout alert |
| SCR-002 | TextField (5), DatePicker (1), Select (1), Button-primary (1), InlineError (6), AlertBanner (1) | Registration; DOB picker; insurance select |
| SCR-003 | Card (N), Badge (N), Button-primary (1), Skeleton (N) | Home dashboard; upcoming appointments |
| SCR-004 | ChatBubble (N), TextField (1), IconButton (1), AILabel (N), Spinner (1), Button-secondary (1), AlertBanner (1) | AI chat; mode-switch button; typing indicator via Spinner |
| SCR-005 | TextField (8), Textarea (2), Select (3), DatePicker (1), Button-primary (1), Button-secondary (1), InlineError (N), Tabs (1) | Multi-section manual form; tabs per section |
| SCR-006 | Card (N), Button-primary (1), Button-secondary (1), StatusBadge (N), AlertBanner (1), InlineError (1), MOD-002, MOD-003 | Slot grid; booking confirm; preferred slot panel |
| SCR-007 | Card (1), Button-secondary (2), Toast (1), Link (1) | Confirmation; Google/Outlook sync buttons |
| SCR-008 | TextField (4), Select (2), Toggle (3), Button-primary (1), InlineError (N) | Notification prefs; calendar sync toggles |
| SCR-009 | FileUploadZone (1), ListItem (N), ProgressBar (1), Button-primary (1), InlineError (1), DocumentStatusIndicator (N) | Upload zone; file queue |
| SCR-010 | ListItem (N), DocumentStatusIndicator (N), Spinner (N), Button-secondary (N) | Processing status list; retry CTA |
| SCR-011 | DataTable (1), StatusBadge (N), RiskScoreBadge (N), Button-primary (1), Button-secondary (N), AlertBanner (1), Skeleton (N) | Live queue; SignalR; Mark Arrived inline |
| SCR-012 | TextField (5), Select (1), Toggle (1), Button-primary (1), Button-ghost (1), InlineError (N), MOD-004 | Walk-in form; optional account creation |
| SCR-013 | TextField (1), IconButton (1), ListItem (N), Avatar (N), Skeleton (N) | Search input; patient result list |
| SCR-014 | Tabs (5), Card (N), ConflictBanner (N), AILabel (N), Badge (N), Button-secondary (N), Skeleton (N), MOD-005 | 360° tabbed view; conflict banners; resolve drawer |
| SCR-015 | DataTable (1), CodeSuggestionCard (N), Button-primary (N), Button-secondary (N), Button-destructive (N), AILabel (N), InlineError (N), ConfirmDialog (1) | Code review table; accept/reject/correct per row |
| SCR-016 | Card (4), Badge (N), Skeleton (N) | KPI metric cards |
| SCR-017 | DataTable (1), Button-primary (1), Button-secondary (N), Button-destructive (N), Avatar (N), Badge (N), MOD-006, ConfirmDialog (1), InlineError (N) | User table; CRUD modals |

### Component Summary

| Category | Components | Variants / States |
|----------|------------|------------------|
| Actions | Button, IconButton, Link | Button: primary/secondary/ghost/destructive × S/M/L × Default/Hover/Active/Focus/Disabled/Loading |
| Inputs | TextField, PasswordField, DatePicker, Select, MultiSelect, Checkbox, Radio, Toggle, FileUploadZone, Textarea | All: Default/Focused/Filled/Error/Disabled |
| Navigation | AppHeader, Sidebar, BottomTabNav, TopNav, Breadcrumb, Tabs | Platform-responsive variants per UXR-302 |
| Content | Card, ListItem, DataTable, Avatar, Badge, StatusBadge, ProgressBar, Skeleton | Card: default/interactive/elevated; DataTable: with sort/pagination |
| Feedback | Modal, Drawer, Toast, AlertBanner, InlineError, Spinner, ConfirmDialog | Toast: info/success/warning/error; AlertBanner: info/success/warning/error |
| Clinical | ChatBubble, CodeSuggestionCard, ConflictBanner, RiskScoreBadge, DocumentStatusIndicator, AILabel | Clinical components use semantic tokens only; AILabel always indigo-500 |

### Component Constraints

- Use only components from designsystem.md; no one-off bespoke styles
- All components support: Default, Hover, Focus, Active, Disabled, Loading states
- Naming convention: `C/<Category>/<Name>` (e.g., `C/Actions/Button`, `C/Clinical/ConflictBanner`)
- No hard-coded colour values in any component (UXR-401)

---

## 11. Prototype Flows

*Flows derived from use cases in spec.md. Each flow specifies personas covered.*

### Flow: FL-001 — Patient Registration & Login Onboarding

**Flow ID**: FL-001
**Derived From**: UC-001, UC-003
**Personas Covered**: Patient
**Description**: New patient creates an account then immediately logs in and lands on the Patient Home Dashboard.

#### Flow Sequence

```text
1. Entry: SCR-001 (Login) / Default
   - Trigger: New patient visits the platform URL
   |
   v
2. Step: SCR-001 — "Register" link clicked
   - Action: Navigate to SCR-002 (Patient Registration)
   |
   v
3. Step: SCR-002 (Registration) / Default
   - Action: Patient fills name, DOB, email, phone, insurance details
   |
   v
4. Decision:
   +-- Validation pass -> POST /auth/register -> success
   +-- Validation fail -> SCR-002 / Validation state (inline field errors)
   |
   v
5. Step: SCR-001 (Login) / Default — post-registration redirect
   - Action: Patient enters new credentials
   |
   v
6. Exit: SCR-003 (Patient Home Dashboard) / Default
```

#### Required Interactions

- Registration form: real-time inline validation on blur per UXR-601
- Successful registration shows a brief Toast: "Account created. Sign in to continue."

---

### Flow: FL-002 — Multi-Role Login & Role Routing

**Flow ID**: FL-002
**Derived From**: UC-003, UC-004, UC-005
**Personas Covered**: Patient, Staff, Admin
**Description**: Any registered user logs in and is routed to their role-appropriate landing screen. Covers login failure and session lockout.

#### Flow Sequence

```text
1. Entry: SCR-001 (Login) / Default
   - Trigger: User navigates to / or session expires
   |
   v
2. Step: Credentials entered, "Sign in" clicked
   - Action: POST /auth/login
   |
   v
3. Decision:
   +-- Patient success -> SCR-003 (Patient Home) / Default
   +-- Staff success  -> SCR-011 (Queue Dashboard) / Default
   +-- Admin success  -> SCR-016 (KPI Dashboard) / Default
   +-- Invalid creds (attempt 1–4) -> SCR-001 / Error state (generic "Authentication failed" message)
   +-- Invalid creds (attempt 5)   -> SCR-001 / Lockout state (15-min lockout message per UXR-601)
```

#### Required Interactions

- Error message MUST NOT specify whether email or password is incorrect (security: no enumeration)
- Loading state on "Sign in" button during POST request (spinner within button)
- MOD-001 (Session Timeout Warning) fires at 13 min inactivity on any authenticated screen

---

### Flow: FL-003 — Patient AI Conversational Intake

**Flow ID**: FL-003
**Derived From**: UC-007, UC-009
**Personas Covered**: Patient
**Description**: Patient completes health intake via the AI conversational interface, reviews the AI-generated summary, and confirms it.

#### Flow Sequence

```text
1. Entry: SCR-003 (Patient Home) / Default
   - Trigger: "Start intake" CTA
   |
   v
2. Step: SCR-004 (AI Conversational Intake) / Default
   - Action: AI sends opening greeting; patient responds in multi-turn dialogue
   - UXR-504: Typing indicator during each AI inference
   |
   v
3. Decision (mode switch):
   +-- Continue AI   -> multi-turn continues until all fields collected
   +-- Switch Manual -> SCR-005 / Default (all AI-collected values pre-populated per UXR-103)
   |
   v
4. Step: SCR-004 / Summary review state
   - Action: AI presents structured summary; patient reviews and edits any field (PATCH /intake/ai/field)
   |
   v
5. Decision:
   +-- Confirm -> POST /intake/ai/confirm -> Toast "Intake saved." -> SCR-003
   +-- Edit    -> return to multi-turn dialogue at edited field
   |
   v
6. Exit: SCR-003 (Patient Home) / Default — intake complete badge shown
```

#### Required Interactions

- "Switch to manual form" link always visible in SCR-004 header
- Ollama unavailable → AlertBanner: "AI intake unavailable. Please use the manual form." + auto-switch CTA

---

### Flow: FL-004 — Patient Manual Intake Submission

**Flow ID**: FL-004
**Derived From**: UC-008, UC-009, UC-010
**Personas Covered**: Patient
**Description**: Patient completes intake using the multi-section manual form. Covers mode switch and validation failure.

#### Flow Sequence

```text
1. Entry: SCR-003 or SCR-004 (mode switch)
   |
   v
2. Step: SCR-005 (Manual Intake Form) / Default
   - Action: Patient fills Demographics, Medical History, Medications, Allergies, Chief Complaint tabs
   - Auto-save draft on tab navigation (POST /intake/draft)
   |
   v
3. Decision:
   +-- All required fields valid -> "Submit" -> POST /intake/manual -> Toast "Intake saved." -> SCR-003
   +-- Required field empty/invalid -> SCR-005 / Validation state (inline errors per UXR-601)
   +-- Switch to AI mode -> SCR-004 / Default (manual data pre-populated per UXR-103)
```

#### Required Interactions

- Tab progress indicator shows completion status per section
- Draft auto-save indicator: "Saving…" / "Saved" transient label

---

### Flow: FL-005 — Appointment Booking with Preferred Slot

**Flow ID**: FL-005
**Derived From**: UC-011, UC-012, UC-014, UC-033, UC-034
**Personas Covered**: Patient
**Description**: Patient browses available slots, optionally designates a preferred slot, passes insurance pre-check, and confirms booking.

#### Flow Sequence

```text
1. Entry: SCR-003 (Patient Home) / Default
   - Trigger: "Book appointment" CTA
   |
   v
2. Step: SCR-006 (Slot Calendar) / Loading → Default
   - Action: GET /slots?available=true; patient selects available slot
   |
   v
3. Step (optional): MOD-003 (Preferred Slot panel) / Default
   - Action: Patient selects a preferred (unavailable) slot; designation stored
   |
   v
4. Step: Insurance pre-check (inline in SCR-006)
   - POST /insurance/validate
   |
   v
5. Decision (insurance):
   +-- Match found    -> no banner shown; continue to MOD-002
   +-- No match found -> UXR-604: amber AlertBanner "Insurance not matched. Booking will continue." -> continue to MOD-002
   |
   v
6. Step: MOD-002 (Booking Confirmation dialog)
   - Action: Patient reviews slot details; clicks "Confirm"
   - POST /bookings (ACID transaction)
   |
   v
7. Decision (booking):
   +-- Success  -> SCR-007 (Booking Confirmation) / Default
   +-- Conflict (409) -> SCR-006 / Conflict state: UXR-602 3 alternative slots shown inline
   |
   v
8. Exit: SCR-007 (Booking Confirmation) / Default
```

#### Required Interactions

- Available slots show date/time + availability chip; unavailable slots greyed with shape-only differentiation (UXR-105)
- "Confirm" button in MOD-002 shows loading state during POST /bookings

---

### Flow: FL-006 — Post-Booking Calendar Sync

**Flow ID**: FL-006
**Derived From**: UC-018, UC-019, UC-020
**Personas Covered**: Patient
**Description**: After booking confirmation, patient optionally syncs the appointment to Google or Outlook Calendar.

#### Flow Sequence

```text
1. Entry: SCR-007 (Booking Confirmation) / Default
   - Trigger: Auto-rendered post-booking
   |
   v
2. Step: Patient clicks "Sync to Google Calendar" or "Sync to Outlook Calendar"
   - Action: OAuth 2.0 redirect to Google/Microsoft consent screen
   |
   v
3. Decision:
   +-- OAuth success -> POST /calendar/google/sync or /calendar/outlook/sync -> Toast "Appointment added to calendar." (success)
   +-- OAuth denied / network failure -> UXR-605: Toast "Calendar sync failed. Your booking is confirmed." (info-level, non-blocking)
   |
   v
4. Exit: SCR-007 remains; patient proceeds to SCR-003 via "Back to dashboard" link
```

#### Required Interactions

- Both sync buttons remain independently actionable
- Sync buttons show loading state during OAuth + API call

---

### Flow: FL-007 — Staff Walk-in Creation & Queue Management

**Flow ID**: FL-007
**Derived From**: UC-002, UC-021, UC-022, UC-035
**Personas Covered**: Staff
**Description**: Staff creates a walk-in booking, the queue table updates in real time via SignalR, and staff marks the patient as arrived.

#### Flow Sequence

```text
1. Entry: SCR-011 (Staff Queue Dashboard) / Default
   - Trigger: Staff post-login or ongoing queue monitoring
   |
   v
2. Step: "New Walk-in" button clicked
   - Action: Navigate to SCR-012 (Walk-in Booking Form)
   |
   v
3. Step: SCR-012 / Default
   - Action: Staff enters patient details; optionally creates account (MOD-004 toggle)
   |
   v
4. Decision:
   +-- Valid -> POST /walkins -> success Toast -> return to SCR-011
   +-- Invalid -> SCR-012 / Validation state (inline errors)
   |
   v
5. Step: SCR-011 / Default
   - Action: SignalR broadcasts new walk-in row; UXR-502 highlight animation on new row
   |
   v
6. Step: Staff clicks "Mark Arrived" on a queue row
   - Action: PATCH /bookings/{id}/status { status: "Arrived" } (inline, no navigation per UXR-106)
   |
   v
7. Exit: SCR-011 — row status updates to "Arrived"; SignalR broadcasts update to all connected staff
```

#### Required Interactions

- SignalR reconnect banner shows if hub connection is lost (AlertBanner: "Queue updates paused. Reconnecting…")
- Walk-in row distinct from scheduled row via StatusBadge ("Walk-in" vs "Scheduled")

---

### Flow: FL-008 — Clinical Document Upload & AI Processing

**Flow ID**: FL-008
**Derived From**: UC-024, UC-025, UC-026, UC-029
**Personas Covered**: Patient
**Description**: Patient uploads one or more clinical documents; the system validates, processes via AI extraction pipeline, and surfaces processing status.

#### Flow Sequence

```text
1. Entry: SCR-003 (Patient Home) or SCR-008 (Profile) / Default
   - Trigger: "Upload documents" CTA
   |
   v
2. Step: SCR-009 (Document Upload) / Default
   - Action: Patient drags file(s) or clicks "Select file" (≥375px: file input per UXR-303)
   |
   v
3. Decision (validation):
   +-- Valid (type + size) -> ProgressBar updates during upload -> POST /documents -> SCR-009 file shows "Uploaded"
   +-- Invalid type/size   -> UXR-603: InlineError with specific reason + allowed types; retry action
   |
   v
4. Step: Background AI extraction begins (server-side)
   - Action: SCR-009 / Loading → navigate to SCR-010 (Processing Status) / Loading
   |
   v
5. Step: SCR-010 / Default — polled status updates (≤5s interval per UXR-503)
   - Document status: Pending → Extracting → Complete / Failed
   |
   v
6. Decision:
   +-- Complete -> Toast "Extraction complete. View patient data." -> SCR-014 link
   +-- Failed   -> SCR-010 / Error state: "Extraction failed." + "Retry" CTA
```

#### Required Interactions

- File upload progress bar (UXR-503); DocumentStatusIndicator per file
- 120s extraction timeout: auto-transitions to Failed status (AIR-008)

---

### Flow: FL-009 — Staff 360° Patient Review

**Flow ID**: FL-009
**Derived From**: UC-027, UC-028, UC-029
**Personas Covered**: Staff
**Description**: Staff locates a patient, views their AI-consolidated 360° view, and resolves any data conflicts.

#### Flow Sequence

```text
1. Entry: SCR-011 (Queue Dashboard) or SCR-013 (Patient Search) / Default
   - Trigger: Patient name link in queue row, or search result
   |
   v
2. Step: SCR-014 (360° Patient View) / Loading → Default
   - Action: GET /patients/{id}/view; data renders across 5 tabs
   - ConflictBanners shown inline in affected tab (UCR-403: colour+icon+text)
   - AILabel shown on every AI-extracted entity (UXR-402)
   |
   v
3. Decision (conflicts present):
   +-- No conflicts   -> staff reviews data, continues to FL-010 (code review)
   +-- Conflicts exist -> staff clicks "Resolve" on ConflictBanner
   |
   v
4. Step: MOD-005 (Resolve Conflict Drawer) / Default
   - Action: Staff selects authoritative value; PATCH /conflicts/{id}/resolve
   |
   v
5. Exit: SCR-014 / Default — ConflictBanner removed for resolved conflict; remaining conflicts stay visible
```

#### Required Interactions

- Tab in-page navigation: Demographics / Intake / Documents / Extracted Data / Medical Codes
- Source-document traceability links on every extracted entity (opens document in new tab)

---

### Flow: FL-010 — Medical Code Review & Decision

**Flow ID**: FL-010
**Derived From**: UC-030, UC-031, UC-032
**Personas Covered**: Staff
**Description**: Staff reviews AI-suggested ICD-10 and CPT codes for a patient, accepting, rejecting, or correcting each suggestion before finalisation.

#### Flow Sequence

```text
1. Entry: SCR-014 (360° View) → "Review codes" CTA or SCR-014 Medical Codes tab
   |
   v
2. Step: SCR-015 (Medical Code Review) / Default
   - Action: GET /patients/{id}/codes?status=Pending; CodeSuggestionCard per suggestion
   - AILabel on each card; source data reference shown
   |
   v
3. Per-suggestion decision:
   +-- Accept   -> PATCH /suggestions/{id} { action: "accept" } -> card status "Accepted" (green badge)
   +-- Reject   -> ConfirmDialog (UXR-404) -> PATCH { action: "reject" } -> card status "Rejected" (neutral)
   +-- Correct  -> inline TextField pre-filled with suggestion -> validate ICD-10/CPT format -> PATCH { action: "correct", correctedCode: "..." }
   |
   v
4. Exit: SCR-015 / Empty state "All code suggestions reviewed." when all cards actioned
```

#### Required Interactions

- Correction input validates ICD-10 format (`[A-Z][0-9]{2}\.?[0-9]{0,4}`) and CPT format (5 digits) on blur
- AI-Human Agreement Rate updated server-side on every accept/reject/correct action

---

### Flow: FL-011 — Admin User Governance

**Flow ID**: FL-011
**Derived From**: UC-006, UC-036
**Personas Covered**: Admin
**Description**: Admin monitors platform KPI metrics and manages user accounts including role assignments.

#### Flow Sequence

```text
1. Entry: SCR-016 (Admin KPI Dashboard) / Default
   - Trigger: Admin post-login
   |
   v
2. Step: Sidebar nav → "Users"
   - Action: Navigate to SCR-017 (User Management)
   |
   v
3. Decision:
   +-- Create user -> "New User" button -> MOD-006 (Create User Modal) -> fill form -> POST /auth/register (admin) -> user appears in table
   +-- Edit user   -> "Edit" action on table row -> MOD-006 (Edit User Modal) -> modify fields/role -> PATCH /users/{id}
   +-- Deactivate  -> "Deactivate" button (destructive variant) -> ConfirmDialog (UXR-404) -> PATCH /users/{id}/deactivate -> status badge "Inactive"
   +-- Validation fail -> MOD-006 / Validation state (inline errors per UXR-601)
   |
   v
4. Exit: SCR-017 / Default — updated user table
```

#### Required Interactions

- KPI cards on SCR-016: total patients, total bookings, no-show rate %, AI-Human Agreement Rate %, critical conflicts count
- Deactivating own account blocked with InlineError (edge case per Section 8)

---

## 12. Export Requirements

### JPG Export Settings

| Setting | Value |
|---------|-------|
| Format | JPG |
| Quality | High (85%) |
| Scale — Mobile (375px) | 2x |
| Scale — Web (1280px+) | 2x |
| Color Profile | sRGB |

### Export Naming Convention

`UPACIP__<Platform>__<ScreenID>-<ScreenName>__<State>__v1.jpg`

Examples:
- `UPACIP__Web__SCR-001-Login__Default__v1.jpg`
- `UPACIP__Mobile__SCR-006-SlotCalendar__Conflict__v1.jpg`
- `UPACIP__Web__SCR-014-360View__Default__v1.jpg`

### Export Manifest

| Screen | States | Platform | Total JPGs |
|--------|--------|----------|-----------|
| SCR-001 Login | Default, Loading, Error, Validation, Lockout | Web + Mobile | 10 |
| SCR-002 Registration | Default, Loading, Error, Validation | Web + Mobile | 8 |
| SCR-003 Patient Home | Default, Loading, Empty | Web + Mobile | 6 |
| SCR-004 AI Intake | Default, Loading, Empty, Error | Web + Mobile | 8 |
| SCR-005 Manual Intake | Default, Loading, Error, Validation | Web + Mobile | 8 |
| SCR-006 Slot Calendar | Default, Loading, Empty, Error, Conflict | Web + Mobile | 10 |
| SCR-007 Booking Confirmation | Default, Loading, Error | Web + Mobile | 6 |
| SCR-008 Patient Profile | Default, Loading, Error, Validation | Web + Mobile | 8 |
| SCR-009 Document Upload | Default, Loading, Empty, Error, Validation | Web + Mobile | 10 |
| SCR-010 Doc Processing | Default, Loading, Empty, Error | Web + Mobile | 8 |
| SCR-011 Staff Queue | Default, Loading, Empty, Error | Web + Mobile | 8 |
| SCR-012 Walk-in Form | Default, Loading, Error, Validation | Web + Mobile | 8 |
| SCR-013 Patient Search | Default, Loading, Empty, Error | Web + Mobile | 8 |
| SCR-014 360° View | Default, Loading, Empty, Error | Web + Mobile | 8 |
| SCR-015 Code Review | Default, Loading, Empty, Error, Validation | Web + Mobile | 10 |
| SCR-016 Admin KPI | Default, Loading, Empty, Error | Web + Mobile | 8 |
| SCR-017 User Management | Default, Loading, Empty, Error, Validation | Web + Mobile | 10 |

### Total Export Count

- **Screens**: 17
- **Average states per screen**: 4.8
- **Platforms**: 2 (Web + Mobile)
- **Total JPGs**: ~144

---

## 13. Figma File Structure

### Page Organization

```text
UPACIP Figma File
+-- 00_Cover
|   +-- Project info, version (v1.0), date (2026-05-17), stakeholders
+-- 01_Foundations
|   +-- Color primitives (slate, blue, green, amber, red, sky, indigo scales)
|   +-- Color semantic tokens (mapped to primitives)
|   +-- Typography scale (IBM Plex Sans 12/14/16/18/20/24/28/32)
|   +-- Spacing scale (4px base grid)
|   +-- Border radius tokens (0/2/4/8/9999px)
|   +-- Elevation/shadow tiers (shadow-0 to shadow-3)
|   +-- Grid definitions (4-col mobile, 8-col tablet, 12-col desktop)
+-- 02_Components
|   +-- C/Actions/Button (all variants × sizes × states)
|   +-- C/Actions/IconButton
|   +-- C/Actions/Link
|   +-- C/Inputs/TextField, PasswordField, DatePicker, Select, MultiSelect
|   +-- C/Inputs/Checkbox, Radio, Toggle, FileUploadZone, Textarea
|   +-- C/Navigation/AppHeader, Sidebar, BottomTabNav, TopNav, Breadcrumb, Tabs
|   +-- C/Content/Card, ListItem, DataTable, Avatar, Badge, StatusBadge, ProgressBar, Skeleton
|   +-- C/Feedback/Modal, Drawer, Toast, AlertBanner, InlineError, Spinner, ConfirmDialog
|   +-- C/Clinical/ChatBubble, CodeSuggestionCard, ConflictBanner, RiskScoreBadge
|   +-- C/Clinical/DocumentStatusIndicator, AILabel
+-- 03_Patterns
|   +-- Auth form pattern (SCR-001/SCR-002)
|   +-- Intake wizard pattern (SCR-004/SCR-005 + mode switch)
|   +-- Calendar slot grid pattern (SCR-006)
|   +-- Data table + inline action pattern (SCR-011, SCR-015, SCR-017)
|   +-- 360° tabbed detail pattern (SCR-014)
|   +-- Error/Empty/Loading patterns
+-- 04_Screens
|   +-- SCR-001-Login / [Default, Loading, Error, Validation, Lockout]
|   +-- SCR-002-Registration / [Default, Loading, Error, Validation]
|   +-- SCR-003-PatientHome / [Default, Loading, Empty]
|   +-- SCR-004-AIIntake / [Default, Loading, Empty, Error]
|   +-- SCR-005-ManualIntake / [Default, Loading, Error, Validation]
|   +-- SCR-006-SlotCalendar / [Default, Loading, Empty, Error, Conflict]
|   +-- SCR-007-BookingConfirmation / [Default, Loading, Error]
|   +-- SCR-008-PatientProfile / [Default, Loading, Error, Validation]
|   +-- SCR-009-DocumentUpload / [Default, Loading, Empty, Error, Validation]
|   +-- SCR-010-DocProcessing / [Default, Loading, Empty, Error]
|   +-- SCR-011-StaffQueue / [Default, Loading, Empty, Error]
|   +-- SCR-012-WalkinForm / [Default, Loading, Error, Validation]
|   +-- SCR-013-PatientSearch / [Default, Loading, Empty, Error]
|   +-- SCR-014-360View / [Default, Loading, Empty, Error]
|   +-- SCR-015-CodeReview / [Default, Loading, Empty, Error, Validation]
|   +-- SCR-016-AdminKPI / [Default, Loading, Empty, Error]
|   +-- SCR-017-UserManagement / [Default, Loading, Empty, Error, Validation]
|   +-- MOD-001-SessionTimeout / [Default]
|   +-- MOD-002-BookingConfirm / [Default, Loading]
|   +-- MOD-003-PreferredSlot / [Default]
|   +-- MOD-004-WalkinAccount / [Default, Validation]
|   +-- MOD-005-ResolveConflict / [Default, Loading]
|   +-- MOD-006-CreateEditUser / [Default, Loading, Validation]
+-- 05_Prototype
|   +-- FL-001: Patient Registration & Login
|   +-- FL-002: Multi-Role Login & Role Routing
|   +-- FL-003: AI Conversational Intake
|   +-- FL-004: Manual Intake Submission
|   +-- FL-005: Appointment Booking with Preferred Slot
|   +-- FL-006: Post-Booking Calendar Sync
|   +-- FL-007: Staff Walk-in + Queue Management
|   +-- FL-008: Document Upload & AI Processing
|   +-- FL-009: Staff 360° Patient Review
|   +-- FL-010: Medical Code Review & Decision
|   +-- FL-011: Admin User Governance
+-- 06_Handoff
    +-- Token usage rules (semantic → primitive mapping)
    +-- Component usage guidelines per screen
    +-- Responsive layout specs (breakpoints, grid columns, gutters)
    +-- Edge case documentation (Section 8 of this spec)
    +-- Accessibility notes (WCAG 2.2 AA focus states, ARIA regions)
    +-- Export manifest (Section 12 of this spec)
```

---

## 14. Quality Checklist

### Pre-Export Validation

- [ ] All 17 screens + 6 modals have all required states (Default/Loading/Empty/Error/Validation where applicable)
- [ ] All components use design system semantic tokens exclusively (no hard-coded hex values — UXR-401)
- [ ] All text content meets WCAG 2.2 AA colour contrast (≥4.5:1 normal, ≥3:1 large/UI — UXR-201)
- [ ] Focus states defined for all interactive elements, meeting WCAG 2.2 AA SC 2.4.11 (UXR-203)
- [ ] Minimum 44×44px touch targets on all mobile-breakpoint screens (UXR-205)
- [ ] All 11 prototype flows wired and functional in Figma prototype mode
- [ ] Export naming convention followed: `UPACIP__<Platform>__<ScreenID>-<Name>__<State>__v1.jpg`
- [ ] Export manifest complete (Section 12) — ~144 total JPGs
- [ ] AI content carries AILabel component on every AI-sourced field/card (UXR-402)
- [ ] Clinical risk indicators use colour + icon + text label (never colour alone — UXR-403)
- [ ] Destructive actions use `variant="destructive"` button + ConfirmDialog (UXR-404)
- [ ] Navigation adapts correctly at 375px, 768px, 1280px, 1440px breakpoints (UXR-302)
- [ ] Tables reflow to card-list layout at 375px (UXR-304)
- [ ] Drag-and-drop degrades to file input on mobile (UXR-303)

### Post-Generation

- [ ] designsystem.md updated with all token definitions and component specifications
- [ ] Export manifest generated and verified against Section 12
- [ ] Handoff documentation complete in 06_Handoff Figma page
- [ ] All 31 UXR IDs traceable to at least one screen in this document
- [ ] Signal ledger updated with any REJECTED inferred UXRs (from Inferred Requirement Review)
