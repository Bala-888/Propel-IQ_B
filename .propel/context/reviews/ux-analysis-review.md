# UX Analysis Review — UPACIP Hi-Fi Wireframes

**Scope:** All 23 wireframe files (17 screens + 6 modals) in `.propel/context/wireframes/Hi-Fi/`
**Standard:** WCAG 2.1 Level AA
**Aesthetic direction:** Utilitarian (Epic EMR / NHS Digital / Linear precedents)
**Analysis date:** Sprint 1 completion

---

## Executive Summary

The wireframe set is structurally well-designed with strong accessibility foundations: focus traps on all modals, ARIA live regions, semantic tab widgets, and color-independent information encoding (UXR-105, UXR-403). Three issues rise to **WCAG AA failures** that block accessibility certification. Five additional **HIGH** issues will degrade usability in production. All findings are documented below with exact file locations and recommended fixes.

---

## CRITICAL — WCAG 2.1 AA Violations

### CRIT-001 · Bottom Tab Navigation absent from 7 of 8 Patient Portal screens

| | |
|---|---|
| **WCAG criterion** | 2.4.1 Bypass Blocks (Level A) |
| **Screens affected** | SCR-004, SCR-005, SCR-006, SCR-007, SCR-008, SCR-009, SCR-010 |
| **Not affected** | SCR-003 (bottom nav present and functional) |

**Evidence:** A grep across all 23 wireframes finds `.bottom-tab-nav` in exactly one file: `wireframe-SCR-003-patient-home-dashboard.html`. Every other patient portal screen hides the sidebar at `≤1279px` with `@media(max-width:1279px){.sidebar{display:none;}}` but provides no replacement navigation element. On tablets and mobile phones, once the patient navigates from SCR-003 to any other screen, they are stranded with no navigation.

**Impact:** Mobile and tablet users — the primary patient demographic — cannot navigate between portal sections after the first tap. This renders the patient portal non-functional on any viewport below 1280px.

**Fix:** Add the `bottom-tab-nav` component (already designed in SCR-003) to all 7 remaining patient portal screens. The component structure from SCR-003 is the reference implementation.

---

### CRIT-002 · AI Label color contrast fails for 11px text

| | |
|---|---|
| **WCAG criterion** | 1.4.3 Contrast (Minimum) — Level AA |
| **Token** | `--color-ai-accent: #6366F1` on `--color-ai-bg: #EEF2FF` |
| **Contrast ratio** | ≈ 4.0:1 (measured) |
| **Required** | 4.5:1 for normal text below 18pt / below 14pt bold |
| **Screens affected** | SCR-004 (AI message bubbles, AI label), SCR-010, SCR-014, SCR-015 |

**Evidence:** The `.ai-label` class uses `font-size: 11px; font-weight: 600`. WCAG 1.4.3 exempts "large text" (≥18pt / ≥14pt bold) from the 4.5:1 requirement, but 11px does not meet either large-text threshold. Measured luminance: `#6366F1` ≈ 0.186; `#EEF2FF` ≈ 0.895 → contrast = (0.895 + 0.05) / (0.186 + 0.05) = **4.00:1**.

**Impact:** AI-sourced data labels are unreadable to users with moderate visual impairments. In a clinical context where distinguishing AI-extracted vs. manually entered data is safety-critical, this is a patient safety concern, not just a compliance issue.

**Fix (two options):**
- Option A (preferred): Darken the accent to `#4F46E5` (contrast ~5.1:1 on `#EEF2FF`)
- Option B: Increase label font to `14px font-weight: 700` (qualifies as large text, 3:1 threshold)

```css
/* Option A — token update in design system */
--color-ai-accent: #4F46E5;   /* was #6366F1 */
```

---

### CRIT-003 · Skip navigation link absent from all 23 screens

| | |
|---|---|
| **WCAG criterion** | 2.4.1 Bypass Blocks (Level A) |
| **Screens affected** | All 17 screens (modals exempt — focus trap is equivalent) |

**Evidence:** No wireframe contains a "skip to main content" anchor link. Authenticated screens have a 240px sidebar with 4–5 navigation items; every keyboard user must Tab through the entire sidebar on every page load before reaching the main content area.

**Impact:** Users navigating by keyboard (including users of switch access, sip-and-puff devices, or sequential navigation) face significant overhead on every page transition. This is an A-level failure.

**Fix:** Add a visually-hidden skip link as the first focusable element on each authenticated screen, revealed on `:focus`:

```html
<a href="#main-content" class="skip-link">Skip to main content</a>
```

```css
.skip-link {
  position: absolute;
  left: -9999px;
  top: var(--space-3);
  z-index: 9999;
  background: var(--color-bg-surface);
  color: var(--color-primary);
  padding: var(--space-2) var(--space-4);
  border: 2px solid var(--color-border-focus);
  border-radius: var(--radius-sm);
  font-size: 14px;
  font-weight: 600;
  text-decoration: none;
}
.skip-link:focus {
  left: var(--space-4);
}
```

The `<main id="main-content">` target should wrap the `.main-wrapper` on each screen.

---

## HIGH — Significant Usability Issues

### HIGH-001 · Touch target size violations across six screens

| | |
|---|---|
| **WCAG criterion** | 2.5.5 Target Size (Level AAA advisory; required for WCAG 2.2 AA) |
| **Expected minimum** | 44×44 CSS px (UXR-205 in design spec) |

**Affected elements by file:**

| Screen | Element | Actual size | Correct fix |
|---|---|---|---|
| `SCR-011` | `.btn` (toolbar buttons) | `min-height: 36px` | Raise to `44px` |
| `SCR-011` | `.btn-sm` (inline table actions "Mark Arrived") | `min-height: 32px` | Raise to `44px` or use icon-only with tooltip |
| `SCR-011` | `.filter-btn` (status filter chips) | `min-height: 36px` | Raise to `44px` |
| `SCR-016` | `.btn` (all action buttons) | `min-height: 32px` | Raise to `44px` |
| `SCR-016` | `.period-btn` (time period filter) | `min-height: 36px` | Raise to `44px` |
| `SCR-003` | Inline card action buttons | `min-height: 36px` (inline style) | Raise to `44px` |
| `SCR-009` | `.remove-btn` (file remove) | `min-height: 32px; min-width: 32px` | Raise to `44×44px` |
| `SCR-013` | Clear search button | `min-height: 32px; min-width: 32px` | Raise to `44×44px` |
| `SCR-017` | `.btn` (all row actions) | `min-height: 32px` | Raise to `44px` |

The pattern of sub-44px buttons appears specifically in data-dense screens (queue, admin, search) where space is at a premium. The fix is to increase the `min-height` while using tighter padding to contain visual size, or to rely on `line-height` to give visual compactness while the clickable area meets the 44px requirement:

```css
/* Compact-looking but accessible approach */
.btn-sm {
  min-height: 44px;     /* accessibility floor */
  padding: 0 12px;      /* visual compactness via horizontal-only padding */
  font-size: 12px;
}
```

---

### HIGH-002 · `aria-live="polite"` on countdown timer will flood screen readers

| | |
|---|---|
| **File** | `wireframe-MOD-001-session-timeout-warning.html` line 54 |
| **Pattern** | `<div role="timer" aria-live="polite" id="countdown">13:00</div>` |

**Evidence:** The JavaScript updates the countdown's `textContent` every 1 second via `setInterval`. With `aria-live="polite"`, every update is queued for announcement. Over 13 minutes this generates 780 announcements. Screen readers (NVDA, JAWS, VoiceOver) queue interrupting speech, making the modal completely unusable without sight.

**Impact:** Screen reader users are bombarded with time announcements every second. They cannot read the modal content or activate buttons while announcements are in the queue.

**Fix:** Remove `aria-live` from the timer display element. Instead, use a separate live region that announces only at key intervals (5 min, 2 min, 1 min, 30 sec):

```html
<!-- Visual timer — NOT live -->
<div class="timer" role="timer" aria-label="Time remaining before session expires" id="countdown">13:00</div>

<!-- Screen reader announcements — separate, throttled -->
<div id="timer-announce" aria-live="polite" aria-atomic="true" class="sr-only"></div>
```

```javascript
// In the interval callback — announce only at key thresholds
var announceAt = [300, 120, 60, 30]; // seconds
if (announceAt.includes(seconds)) {
  document.getElementById('timer-announce').textContent =
    `Session expires in ${Math.ceil(seconds / 60)} minutes`;
}
```

---

### HIGH-003 · Form inputs use `font-size: 15px` — triggers iOS Safari auto-zoom

| | |
|---|---|
| **Files** | `wireframe-SCR-005-manual-intake-form.html`, `wireframe-SCR-012-walkin-booking-form.html`, `wireframe-SCR-004-ai-conversational-intake.html` (chat textarea) |
| **Issue** | iOS Safari zooms the viewport when a focused input has `font-size < 16px` |

**Evidence:**
- SCR-005 `.form-input, .form-select, .form-textarea { font-size: 15px }` — all form fields in the 5-step intake form
- SCR-012 same `.form-input` rule at 15px covers name, phone, chief complaint fields
- SCR-004 `.chat-input { font-size: 15px }` (separate from body)

SCR-001 correctly uses `font-size: 16px` on its form inputs. The inconsistency means patients filling out intake on an iPhone will have the page zoomed in for every tap on a form field, disrupting the visual layout.

**Fix:** Standardize all form inputs to `font-size: 16px` minimum. The visual difference at 15px vs 16px is imperceptible; the behavioural difference on iOS is significant.

---

### HIGH-004 · No loading, empty, or error states designed for data-heavy screens

| | |
|---|---|
| **Screens affected** | SCR-011 (queue), SCR-013 (search), SCR-015 (code review), SCR-016 (KPI dashboard) |

**Evidence:** Every data-heavy screen is designed only in its "populated with live data" state. There are no wireframes or component specifications for:
- **Loading skeleton** — while waiting for API response
- **Empty state** — no appointments today, no search results, no pending codes
- **API error state** — network failure, 500 error, timeout

**Impact:** The React implementation (Sprint 2) will need to invent these states. Without a specification, different developers will create inconsistent loading and empty state experiences. In the staff queue, a missing loading indicator could be mistaken for an empty queue, causing staff to miss that data is still loading.

**Fix:** Add a sub-wireframe or annotation section to SCR-011, SCR-013, SCR-015, SCR-016 covering:
1. Skeleton card/row (3–5 shimmer rows during load)
2. Empty state illustration + CTA (e.g., "No appointments today · Add a walk-in")
3. Error banner + retry action

---

### HIGH-005 · Toast component missing from all wireframes except SCR-007 and SCR-008

| | |
|---|---|
| **Files with toast** | `wireframe-SCR-007-booking-confirmation.html` (calendar sync toast), `wireframe-SCR-008-patient-profile-settings.html` (save confirmation toast) |
| **Flows referencing toast** | FL-005 step 8 (calendar sync → toast), FL-008 (upload success), FL-010 (code accepted) |

**Evidence:** The navigation map and flow definitions reference "toast" as the outcome of calendar sync, profile save, and booking confirmation actions. A `<div class="toast-container" role="status" aria-live="polite">` exists in SCR-007, and a `.toast` element in SCR-008, but neither file defines the toast component's visual states (success, error, info), dismiss behaviour, or auto-dismiss timing. No other screen that triggers a toast (SCR-009 upload success, SCR-015 code acceptance) defines the toast pattern.

**Fix:** Add a Toast component specification to `component-inventory.md` covering:
- Visual variants: success, error, info
- Auto-dismiss: 4s (success/info), no auto-dismiss (error)
- Dismiss button with `aria-label="Dismiss notification"`
- Maximum 1 stacked toast (queue subsequent ones)
- Position: bottom-right at desktop; bottom-center at mobile

---

## MEDIUM — Design Consistency Issues

### MED-001 · Error/warning background colors hardcoded instead of using design tokens

| | |
|---|---|
| **Files** | SCR-001, SCR-006, SCR-014 (and others using `.alert-banner.error`) |
| **Hardcoded values** | `background: #FEF2F2`, `border-color: #FEE2E2`, conflict-desc `color: #7F1D1D` |

The design token system defines semantic foreground colors (`--color-status-error: #DC2626`) but not the corresponding background and border tokens for status bands. This forces inline hardcoded values across multiple screens. In production, a single design system change would require editing every file individually.

**Fix:** Add to the token set:
```css
--color-status-error-bg:     #FEF2F2;
--color-status-error-border: #FEE2E2;
--color-status-error-text:   #7F1D1D;   /* for body text in error bands */
--color-status-warning-bg:   #FFFBEB;
--color-status-warning-border: #FEF3C7;
--color-status-success-bg:   #F0FDF4;
--color-status-success-border: #BBF7D0;
--color-status-info-bg:      #F0F9FF;
--color-status-info-border:  #E0F2FE;
```

---

### MED-002 · Design tokens duplicated inline across all 23 wireframe files

| | |
|---|---|
| **Risk** | Token drift in production (SCR-003 tokens may differ from SCR-016 tokens if edited independently) |

All tokens are re-declared in the `:root {}` block of each individual file. This is acceptable for wireframe prototyping but signals a maintenance gap: any token change (e.g., tuning `--color-primary`) requires editing all 23 files.

**Fix (implementation guidance for Sprint 2):** When implementing the React component library, establish a single `tokens.css` or CSS-in-JS token source. The wireframes themselves do not need to be changed — this is a production-code concern. Document this in the design system notes.

---

## Confirmed Compliant — Patterns Verified Correct

The following patterns were initially uncertain but were confirmed correct after inspecting the wireframe HTML:

| Pattern | Verification |
|---|---|
| Focus trap in MOD-001 (session timeout) | ✅ `keydown` Tab/Shift+Tab trap implemented |
| Focus trap in MOD-005, MOD-006 | ✅ Same pattern applied |
| Tab widget ARIA (SCR-014, SCR-005) | ✅ `role="tablist"`, `role="tab"`, `aria-selected`, `aria-controls` |
| Chat messages live region (SCR-004) | ✅ `role="log"` + `aria-live="polite"` on messages area |
| Chat textarea labeling (SCR-004) | ✅ `aria-label="Type your response"` |
| Conflict banners assertive (SCR-014, SCR-006) | ✅ `role="alert"` + `aria-live="assertive"` |
| Slot state encoding (SCR-006) | ✅ Shape + pattern + text label (UXR-105) |
| Risk badge encoding (SCR-011) | ✅ Color + icon + text label (UXR-403) |
| Focus rings on all interactive elements | ✅ `outline: 2px solid var(--color-border-focus); outline-offset: 2px` |
| `aria-current="page"` on nav items | ✅ Present on `.nav-item.active` |
| `autofocus` on primary modal button | ✅ MOD-001, MOD-002 |
| `aria-modal="true"` on dialogs | ✅ MOD-001 and all modals |
| Form inputs trigger no iOS zoom (SCR-001) | ✅ `font-size: 16px` on login form inputs |
| `lang="en"` on all HTML elements | ✅ All 23 files |
| `autocomplete` attributes on auth fields | ✅ `autocomplete="username"`, `autocomplete="current-password"` |
| Color contrast — body text | ✅ `#0F172A` on white ≈ 19.5:1 |
| Color contrast — secondary text | ✅ `#475569` on white ≈ 5.4:1 |
| Color contrast — primary button | ✅ `#FFFFFF` on `#1A56DB` ≈ 4.8:1 |
| `role="timer"` on countdown | ✅ Correct semantic role |
| `aria-live="polite"` on save status (SCR-005) | ✅ Auto-save indicator announced politely |
| `aria-live="polite"` on SignalR bar (SCR-011) | ✅ Connection status announced |
| `role="status"` on toast containers | ✅ SCR-007, SCR-008 |
| Bottom tab nav present | ✅ SCR-003 only (gap in SCR-004–010 documented in CRIT-001) |

---

## Issue Prioritization Matrix

| ID | Severity | WCAG Criterion | Sprint to fix | File(s) |
|---|---|---|---|---|
| CRIT-001 | Critical | 2.4.1 Bypass Blocks | Sprint 2 | SCR-004–010 |
| CRIT-002 | Critical | 1.4.3 Contrast | Sprint 2 | SCR-004, SCR-010, SCR-014, SCR-015 |
| CRIT-003 | Critical | 2.4.1 Bypass Blocks | Sprint 2 | All screens |
| HIGH-001 | High | 2.5.5 Target Size | Sprint 2–3 | SCR-003, SCR-009, SCR-011, SCR-013, SCR-016, SCR-017 |
| HIGH-002 | High | Best practice | Sprint 3 (MOD impl.) | MOD-001 |
| HIGH-003 | High | UAAG / iOS UX | Sprint 2 | SCR-004, SCR-005, SCR-012 |
| HIGH-004 | High | UX completeness | Design phase | SCR-011, SCR-013, SCR-015, SCR-016 |
| HIGH-005 | High | UX completeness | Design phase | SCR-009, SCR-015 |
| MED-001 | Medium | Design system | Sprint 2 | Multiple |
| MED-002 | Medium | Maintainability | Sprint 2 | All wireframes |

---

## Recommended Actions Before Sprint 2 Implementation

1. **Wire CRIT-001 fix into the wireframes** — add `bottom-tab-nav` HTML to SCR-004 through SCR-010 before the React team implements routing and navigation, to avoid building the wrong mobile pattern.

2. **Update `design-tokens-applied.md`** — add the `--color-ai-accent` corrected value (`#4F46E5`) and the 8 new status band tokens (MED-001).

3. **Add Toast component spec to `component-inventory.md`** — the React implementation needs a spec before any screen that triggers toasts is built.

4. **Annotate loading/empty/error states** — add a section to SCR-011, SCR-013, SCR-015, SCR-016 wireframes before those screens are implemented.

5. **Fix `aria-live` on MOD-001 timer** — a one-line change in the wireframe that documents the correct production pattern.

6. **Add skip link CSS to `component-inventory.md`** — document the `.skip-link` pattern as a required layout component so every screen template includes it.
