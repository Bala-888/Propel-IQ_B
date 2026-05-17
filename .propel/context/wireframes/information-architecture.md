# Information Architecture

## 1. Document Purpose

This document describes the information architecture for the UPACIP platform hi-fi wireframe set. It maps all screens, navigation structures, content hierarchies, and user flows for the 3-role responsive web application.

- **Platform:** UPACIP — Unified Patient and Clinician Intelligent Platform
- **Wireframe set:** Hi-Fi (23 files)
- **Roles covered:** Patient · Staff · Admin
- **Viewport:** 1440px desktop primary; responsive to 375px mobile

---

## 2. Site Map

```
UPACIP
├── Public
│   ├── SCR-001 Login
│   └── SCR-002 Patient Registration
│
├── Patient Portal (authenticated, role: patient)
│   ├── SCR-003 Home Dashboard  ← post-login landing
│   ├── SCR-004 AI Conversational Intake
│   ├── SCR-005 Manual Intake Form
│   ├── SCR-006 Appointment Slot Calendar
│   ├── SCR-007 Booking Confirmation
│   ├── SCR-008 Patient Profile & Settings
│   ├── SCR-009 Document Upload
│   └── SCR-010 Document Processing Status
│
├── Staff Portal (authenticated, role: staff)
│   ├── SCR-011 Staff Queue Dashboard  ← post-login landing
│   ├── SCR-012 Walk-in Booking Form
│   ├── SCR-013 Patient Search
│   ├── SCR-014 360° Patient View
│   └── SCR-015 Medical Code Review
│
├── Admin Console (authenticated, role: admin)
│   ├── SCR-016 Admin KPI Dashboard  ← post-login landing
│   └── SCR-017 Admin User Management
│
└── Modals (overlaid on authenticated screens)
    ├── MOD-001 Session Timeout Warning  (all authenticated screens)
    ├── MOD-002 Booking Confirmation Dialog  (SCR-006)
    ├── MOD-003 Preferred Slot Selection  (SCR-006)
    ├── MOD-004 Walk-in Account Creation  (SCR-012)
    ├── MOD-005 Resolve Conflict Drawer  (SCR-014)
    └── MOD-006 Create / Edit User Modal  (SCR-017)
```

---

## 3. Role-Specific Navigation

### Patient sidebar navigation
| Label | Target |
|---|---|
| Dashboard | SCR-003 |
| Intake | SCR-004 |
| Appointments | SCR-006 |
| Documents | SCR-009 |
| Profile | SCR-008 |

### Staff sidebar navigation
| Label | Target |
|---|---|
| Queue | SCR-011 |
| Walk-in | SCR-012 |
| Patient search | SCR-013 |
| Code review | SCR-015 |

### Admin sidebar navigation
| Label | Target |
|---|---|
| Metrics | SCR-016 |
| Users | SCR-017 |

---

## 4. Content Hierarchy by Screen

| Screen | Primary content | Secondary content |
|---|---|---|
| SCR-001 | Login form, role selector | Register link |
| SCR-002 | Registration form | Back to login |
| SCR-003 | Quick actions, upcoming appointments | Recent documents, status strip |
| SCR-004 | AI chat, summary column | Switch to manual link |
| SCR-005 | Multi-tab form (5 tabs) | Auto-save status, switch to AI |
| SCR-006 | Calendar grid, booking panel | Insurance alert, 409 conflict panel |
| SCR-007 | Confirmation card, booking reference | Calendar sync options |
| SCR-008 | Settings menu, personal details form | Notification toggles |
| SCR-009 | Drop zone, file queue | View status link |
| SCR-010 | Document status list, extraction pipeline | Retry CTA |
| SCR-011 | Queue data table, SignalR bar | Filter tabs, new walk-in button |
| SCR-012 | Walk-in form | Optional account creation section |
| SCR-013 | Search bar, results list | Patient badges |
| SCR-014 | Patient header, 5-tab view | Conflict banner, AILabel entities |
| SCR-015 | Code suggestion cards | Accept/reject/correct actions, confirm dialog |
| SCR-016 | 2×3 KPI grid | Trend chart placeholders, date filter |
| SCR-017 | Users data table | Filter row, new user button |

---

## 5. User Flow Mapping

| Flow ID | Name | Screens |
|---|---|---|
| FL-001 | User registration | SCR-001 → SCR-002 → SCR-001 |
| FL-002 | Patient login | SCR-001 → SCR-003 |
| FL-003 | Session timeout | Any authenticated → MOD-001 → SCR-001 |
| FL-004 | AI intake | SCR-003 → SCR-004 → SCR-003 |
| FL-005 | Book appointment | SCR-003 → SCR-006 → MOD-002 → SCR-007 → SCR-003 |
| FL-006 | Preferred slot | SCR-006 → MOD-003 → SCR-006 |
| FL-007 | Walk-in booking | SCR-011 → SCR-012 → (MOD-004) → SCR-011 |
| FL-008 | Document upload & extraction | SCR-009 → SCR-010 → SCR-014 |
| FL-009 | Patient lookup | SCR-013 → SCR-014 |
| FL-010 | Medical code review | SCR-014 → SCR-015 → SCR-014 |
| FL-011 | Conflict resolution | SCR-014 → MOD-005 → SCR-014 |

---

## 6. Breakpoint Strategy

| Breakpoint | Width | Layout changes |
|---|---|---|
| Mobile | 375px | Sidebar hidden; bottom tab bar (max 5 tabs); DataTable → card layout |
| Tablet | 768px | Sidebar hidden; top header + hamburger menu |
| Laptop | 1280px | Sidebar visible (240px); main content area |
| Desktop | 1440px | Sidebar visible; wider content columns |

---

## 7. Navigation Principles

- Persistent left sidebar for all authenticated screens (≥1280px)
- Role-appropriate nav items only (no cross-role links)
- Active state: `background: var(--color-primary-subtle); color: var(--color-primary)`
- Back navigation: breadcrumb-style `← Back` link in app header
- Modal/drawer overlay: always include Escape key handler and focus trap (UXR-202)
- Bottom tab navigation at 375px: max 5 tabs per UXR-302

---

## 8. Content Taxonomy

**Clinical data types:** Diagnosis · Vital sign · Medication · Allergy · Medical code (ICD-10 / CPT)

**Booking data types:** Appointment slot · Booking reference · Walk-in record · Queue entry

**User data types:** Patient profile · Staff record · Admin account

**Document types:** PDF · DOCX · JPG · PNG (max 20 MB)

**AI artefact types:** Code suggestion · Extracted entity · Intake conversation · Conflict flag

---

## 9. Metadata and SEO

All wireframe HTML pages use `<title>` format: `SCR-NNN — Screen Name | UPACIP Hi-Fi Wireframe`

Language: `lang="en"` on all HTML elements.

---

## 10. Accessibility Architecture

- WCAG 2.2 AA compliance target
- Semantic HTML landmarks: `<header>`, `<nav>`, `<main>`, `<aside>`, `<footer>`
- `aria-current="page"` on active nav items
- `aria-live="polite"` on dynamic regions (queue updates, toast notifications)
- `aria-live="assertive"` on conflict banners and critical alerts
- Focus trap on all modal/drawer components (UXR-202)
- Min touch target: 44×44px (UXR-205)

---

## 11. Change Log

| Version | Date | Change |
|---|---|---|
| 1.0 | 17 May 2026 | Initial creation — all 23 wireframe screens |
