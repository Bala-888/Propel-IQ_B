# Navigation Map

## Flow Index

| Flow ID | Name | Entry | Exit | Modal layers |
|---|---|---|---|---|
| FL-001 | User registration | SCR-001 | SCR-001 (post-registration) | None |
| FL-002 | Patient login | SCR-001 | SCR-003 (patient) / SCR-011 (staff) / SCR-016 (admin) | None |
| FL-003 | Session timeout | Any authenticated screen | SCR-001 (sign-out) | MOD-001 |
| FL-004 | AI intake | SCR-003 | SCR-003 (confirmed) | None |
| FL-005 | Book appointment | SCR-003 / SCR-006 | SCR-007 | MOD-002 (step 7) |
| FL-006 | Preferred slot | SCR-006 | SCR-006 (updated) | MOD-003 |
| FL-007 | Walk-in booking | SCR-011 | SCR-011 (patient queued) | MOD-004 (optional) |
| FL-008 | Document upload & extraction | SCR-009 | SCR-014 (extracted data) | None |
| FL-009 | Patient lookup | SCR-013 | SCR-014 | None |
| FL-010 | Medical code review | SCR-014 | SCR-014 (codes resolved) | ConfirmDialog (reject) |
| FL-011 | Conflict resolution | SCR-014 | SCR-014 (conflict resolved) | MOD-005 |

---

## Screen-to-Screen Links

### Public / Authentication

| Source | Trigger | Destination |
|---|---|---|
| SCR-001 (Login) | Sign in (patient) | SCR-003 |
| SCR-001 (Login) | Sign in (staff) | SCR-011 |
| SCR-001 (Login) | Sign in (admin) | SCR-016 |
| SCR-001 (Login) | Register link | SCR-002 |
| SCR-002 (Registration) | Submit / Back | SCR-001 |

### Patient Portal

| Source | Trigger | Destination |
|---|---|---|
| SCR-003 (Home) | Book appointment CTA | SCR-006 |
| SCR-003 (Home) | Start intake CTA | SCR-004 |
| SCR-003 (Home) | Upload documents CTA | SCR-009 |
| SCR-003 (Home) | Appointment card → reschedule | SCR-006 |
| SCR-003 (Home) | Document list item → view status | SCR-010 |
| SCR-004 (AI Intake) | Switch to manual form | SCR-005 |
| SCR-004 (AI Intake) | Confirm intake | SCR-003 |
| SCR-005 (Manual Intake) | Switch to AI mode | SCR-004 |
| SCR-005 (Manual Intake) | Submit | SCR-003 |
| SCR-006 (Calendar) | Confirm booking | MOD-002 → SCR-007 |
| SCR-006 (Calendar) | Preferred slot link | MOD-003 |
| SCR-006 (Calendar) | Back to dashboard | SCR-003 |
| SCR-007 (Confirmation) | Sync calendar (Google/Outlook) | Toast (stays on screen) |
| SCR-007 (Confirmation) | Back to dashboard | SCR-003 |
| SCR-009 (Upload) | View processing status | SCR-010 |
| SCR-010 (Status) | View extracted data | SCR-014 |

### Staff Portal

| Source | Trigger | Destination |
|---|---|---|
| SCR-011 (Queue) | Patient name row | SCR-014 |
| SCR-011 (Queue) | View → button | SCR-014 |
| SCR-011 (Queue) | New walk-in button | SCR-012 |
| SCR-011 (Queue) | Mark Arrived (inline, UXR-106) | Stays on SCR-011 (inline update) |
| SCR-012 (Walk-in Form) | Add to queue / Cancel | SCR-011 |
| SCR-012 (Walk-in Form) | Account creation toggle | MOD-004 inline section |
| SCR-013 (Patient Search) | Result row | SCR-014 |
| SCR-014 (360° Patient View) | Back | SCR-013 |
| SCR-014 (360° Patient View) | Review codes → | SCR-015 |
| SCR-014 (360° Patient View) | Resolve conflict → | MOD-005 |
| SCR-015 (Code Review) | Back | SCR-014 |
| SCR-015 (Code Review) | Accept button | Inline state change (stays on SCR-015) |
| SCR-015 (Code Review) | Reject button | ConfirmDialog → stays on SCR-015 |
| SCR-015 (Code Review) | Correct button | Inline field (stays on SCR-015) |

### Admin Console

| Source | Trigger | Destination |
|---|---|---|
| SCR-016 (KPI) | Users nav | SCR-017 |
| SCR-017 (Users) | Metrics nav | SCR-016 |
| SCR-017 (Users) | New user button | MOD-006 |
| SCR-017 (Users) | Edit button (any row) | MOD-006 |
| SCR-017 (Users) | Deactivate button | ConfirmDialog → inline state update |
| SCR-017 (Users) | Activate button | Inline state update |

### Modal Returns

| Modal | Primary action | Destination | Secondary action | Destination |
|---|---|---|---|---|
| MOD-001 | Stay signed in | Stays on originating screen | Sign out | SCR-001 |
| MOD-002 | Confirm booking | SCR-007 | Cancel | SCR-006 |
| MOD-003 | Set as preferred | SCR-006 | Cancel / × | SCR-006 |
| MOD-004 | Create account + add to queue | SCR-011 | Skip | SCR-011 |
| MOD-005 | Resolve conflict | SCR-014 | Cancel / × | SCR-014 |
| MOD-006 | Save user | SCR-017 | Cancel / × | SCR-017 |

---

## Dead Ends and Exceptions

### Intentional dead ends (no back navigation needed)
- SCR-007 (Booking Confirmation) — terminal success state; "Back to dashboard" is the only exit. Calendar sync stays on page via Toast.
- MOD-001 (Session Timeout) — blocking modal; user must choose "Stay signed in" or "Sign out now". Clicking overlay has no effect.

### Known navigation stubs (wireframe-only)
- SCR-016 KPI chart placeholders — "rendered in production" label; no click targets.
- SCR-004 Ollama unavailable AlertBanner — dismiss closes banner but does not navigate.
- SCR-011 "Mark Arrived" inline action — updates row state in-place (simulated with JS); no navigation.
- SCR-015 Accept/Reject/Correct — all in-place state changes; no navigation until "Back" is clicked.

### Edge cases handled
- **Admin self-deactivation (SCR-017):** Deactivate button disabled for the logged-in admin row; `aria-disabled="true"` + `title` tooltip + self-deactivate error banner on JS trigger.
- **Long patient names (SCR-011, SCR-013, SCR-017):** Priya Krishnamurthy-Nair truncated with ellipsis + `title` attribute for full name.
- **Missing insurance ID (SCR-006, SCR-013):** Maria Chen shown with amber "No insurance ID" badge and soft AlertBanner (not blocking, UXR-604).
- **File extraction failure (SCR-009, SCR-010):** Retry CTA + specific error reason (UXR-603).
- **Slot 409 conflict (SCR-006):** Hidden conflict panel reveals 3 alternative slots inline (UXR-602).
