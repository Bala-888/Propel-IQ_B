# Component Inventory

## 1. Document Purpose

This document catalogs all UI components used across the UPACIP hi-fi wireframe set (23 HTML files). Components follow the Utilitarian aesthetic direction with IBM Plex Sans typography and semantic CSS custom properties.

---

## 2. Layout Components

### AppShell
- **Variants:** Patient · Staff · Admin
- **Structure:** Sidebar (240px) + main wrapper (flex-1)
- **Responsive:** Sidebar hides at ≤1279px; bottom tab bar at ≤768px (max 5 tabs, UXR-302)
- **Used in:** SCR-003 through SCR-017
- **Required:** Must include `<a href="#main-content" class="skip-link">Skip to main content</a>` as first child of `<body>` (WCAG 2.4.1 — see SkipLink component)

### SkipLink *(required on all authenticated screens — CRIT-003)*
- **Placement:** First focusable element in `<body>`, before sidebar
- **Target:** `<main id="main-content">` wrapping `.main-wrapper`
- **Default state:** Visually hidden (`position: absolute; left: -9999px`)
- **Focused state:** Revealed at `top: var(--space-3); left: var(--space-4); z-index: 9999`
- **Styles:**
  ```css
  .skip-link {
    position: absolute; left: -9999px; top: var(--space-3);
    z-index: 9999; background: var(--color-bg-surface); color: var(--color-primary);
    padding: var(--space-2) var(--space-4); border: 2px solid var(--color-border-focus);
    border-radius: var(--radius-sm); font-size: 14px; font-weight: 600;
    text-decoration: none;
  }
  .skip-link:focus { left: var(--space-4); }
  ```

### Sidebar
- **Contents:** Logo lockup · nav items · user info chip · sign-out
- **Active state:** `background: var(--color-primary-subtle); color: var(--color-primary)`
- **Nav item height:** 44px min (UXR-205)

### AppHeader
- **Height:** 56px
- **Variants:** Page title only · Back link + page title · Back link + title + actions
- **Border:** `1px solid var(--color-border)` bottom

### BottomTabNav (mobile)
- **Display:** `display:flex` at ≤768px
- **Max tabs:** 5 (UXR-302)
- **Used in:** All patient screens

### ContentArea
- **Padding:** `var(--space-10)` desktop; `var(--space-6)` tablet; `var(--space-4)` mobile
- **Overflow:** `overflow-y: auto`

---

## 3. Form Components

### TextField / Input
- **Border:** `1px solid var(--color-border)`; hover: `var(--color-border-strong)`; focus: `var(--color-border-focus)` + 3px box-shadow
- **Padding:** `var(--space-3) var(--space-4)`
- **Font:** `var(--font-sans)` **16px** (minimum — prevents iOS Safari auto-zoom; UX-HIGH-003)
- **Error state:** `border-color: var(--color-status-error)` + `aria-invalid="true"` + FieldError message (UXR-601)
- **Used in:** SCR-001, SCR-002, SCR-005, SCR-008, SCR-012, SCR-015, MOD-004, MOD-006

### Select
- Same visual spec as TextField
- **Used in:** SCR-002, SCR-005, SCR-008, SCR-012, SCR-017, MOD-006

### Textarea
- **Min-height:** 80px; `resize: vertical`
- **Used in:** SCR-004, SCR-005, SCR-012, MOD-005

### FormLabel
- **Font:** 14px / 500
- **Required indicator:** Implicit (no asterisk — field-level error on submit)

### FieldError (UXR-601)
- **Color:** `var(--color-status-error)` 12px
- **Icon:** `⚠` prefix
- **`role="alert"`** on inline errors

### ToggleSwitch
- **Track:** 44×24px; active: `var(--color-primary)`; inactive: `var(--color-border-strong)`
- **Thumb:** 18×18px white
- **Focus:** `outline: 2px solid var(--color-border-focus)` on `:focus-within`
- **Used in:** SCR-008, MOD-006

### DatePicker
- Native `<input type="date">` — degrades gracefully; max-width 200px
- **Used in:** SCR-002, SCR-008, SCR-012

---

## 4. Action Components

### Button — Primary
- **Background:** `var(--color-primary)` / hover: `var(--color-primary-hover)`
- **Color:** `var(--color-text-inverse)`
- **Min-height:** 44px (UXR-205)
- **Loading state:** Spinner + loading text (UXR-501)

### Button — Secondary
- **Background:** `var(--color-bg-surface)` / border: `1px solid var(--color-border)`
- **Hover:** `var(--color-bg-subtle)`

### Button — Destructive (UXR-404)
- **Background:** `var(--color-destructive)` (`#DC2626`)
- **Used only in:** ConfirmDialog before destructive action executes

### Button — Danger (muted)
- **Background:** `#FEF2F2`; color: `var(--color-status-error)`; border: `1px solid #FECACA`
- **Used in:** Row-level deactivate actions, conflict CTAs

### Link-styled button
- Used where navigation semantics required (`<a href="…">`) styled as button

---

## 5. Feedback Components

### AlertBanner
- **Structure:** Icon + coloured background + border (1px solid) — no `border-left > 2px` (QG-11 remediation)
- **Variants:**
  - `info`: `background: var(--color-primary-subtle)`; border: `var(--color-primary)`
  - `warning` (amber): `background: #FEF9C3`; border: `#FCD34D` — used for insurance soft warning (SCR-006)
  - `error`: `background: #FEF2F2`; border: `#FECACA`
  - `success`: `background: #DCFCE7`; border: `#BBF7D0`
- **Used in:** SCR-006 (insurance warning), SCR-014 (conflict banner), SCR-004 (Ollama unavailable)

### ConflictBanner
- **Subtype of AlertBanner (error variant)**
- Icon: warning SVG in `#FEE2E2` circle
- **`aria-live="assertive"`** (WCAG 2.2 AA)
- **Used in:** SCR-014 (Amoxicillin / Penicillin conflict)

### Toast Notification (UXR-605) *(HIGH-005 — spec expanded)*
- **Position:** `position: fixed; bottom: 24px; right: 24px` (desktop); `bottom: 16px; left: 50%; transform: translateX(-50%)` (≤768px)
- **Non-blocking:** Stays on same screen
- **Auto-dismiss:** 4 seconds (success / info); **no auto-dismiss** (error)
- **Dismiss button:** `×` with `aria-label="Dismiss notification"`, `min-height: 44px; min-width: 44px`
- **Max stacked:** 1 visible at a time; queue subsequent
- **Variants:**
  - `success` — `background: var(--color-status-success-bg); border: 1px solid var(--color-status-success-border); color: var(--color-status-success)`
  - `error` — `background: var(--color-status-error-bg); border: 1px solid var(--color-status-error-border); color: var(--color-status-error)`
  - `info` — `background: var(--color-status-info-bg); border: 1px solid var(--color-status-info-border); color: var(--color-status-info)`
- **`role="status"` + `aria-live="polite"`** on container; `aria-atomic="true"` per toast
- **Used in:** SCR-007 (calendar sync), SCR-008 (save settings), SCR-009 (upload success — HIGH-005), SCR-015 (code accepted — HIGH-005)

### Spinner / Loading indicator
- CSS border animation: `border-top-color` animated
- **Duration:** 0.6s linear infinite
- **Used in:** SCR-004 (typing indicator), MOD-002 (confirm loading), SCR-007 (sync)

### ProgressBar (UXR-503)
- **Height:** 4px; `border-radius: 9999px`
- **`role="progressbar"` + `aria-valuenow` + `aria-valuemin` + `aria-valuemax`**
- **Used in:** SCR-009 (file upload)

### SignalR Status Bar
- **Live variant:** green `●` pulse dot + "Live · Queue updates active"
- **Error variant:** red + "Disconnected — retrying…"
- **`role="status"` + `aria-live="polite"`**
- **Used in:** SCR-011

---

## 6. Data Display Components

### DataTable
- **Headers:** `text-transform: uppercase; font-size: 12px; color: var(--color-text-secondary)`
- **Row hover:** `background: var(--color-bg-subtle)`
- **Responsive:** Collapses to CardLayout at 375px (UXR-304)
- **Used in:** SCR-011, SCR-017

### CardLayout (mobile DataTable fallback, UXR-304)
- **Used in:** SCR-011 mobile view

### KPICard
- **Value font:** `var(--font-mono)` 36px
- **Label:** uppercase 13px
- **Trend row:** colour-coded up/down/neutral
- **Used in:** SCR-016

### EntityCard
- **Subtype:** Diagnosis · Vital · Medication · Allergy
- **AILabel badge** on all extracted entities (UXR-402)
- **Used in:** SCR-014

### CodeSuggestionCard
- **Code:** `var(--font-mono)` 18px
- **Confidence meter:** `role="meter"` bar
- **Status badge:** pending · accepted · rejected
- **AILabel** on each card (UXR-402)
- **Used in:** SCR-015

### FileQueueItem
- **Columns:** file icon, name/meta, status badge, remove button
- **Upload state:** ProgressBar inline
- **Used in:** SCR-009

---

## 7. Overlay Components

### Modal (centred)
- **Max-width:** 420–560px depending on content
- **Focus trap:** Tab key cycles within modal (UXR-202)
- **Dismiss:** Escape key + explicit close/cancel button
- **`role="dialog"` + `aria-modal="true"`**
- **Used in:** MOD-001, MOD-002, MOD-003, MOD-004, MOD-006

### Drawer (right-side)
- **Width:** 360px (mobile: 100%)
- **Focus trap:** Tab key cycles within drawer (UXR-202)
- **Escape:** Returns to originating screen
- **Used in:** MOD-005

### ConfirmDialog (UXR-404)
- **Triggered by:** Destructive actions (reject code, deactivate user)
- **Must show:** Destructive button + Cancel; no default destructive action on Enter
- **Used in:** SCR-015, SCR-017

---

## 8. Navigation Components

### AILabel (UXR-402)
- **Style:** `background: var(--color-ai-bg); color: var(--color-ai-accent)`; `border-radius: 9999px`; 11px 600 weight
- **Text:** "✦ AI"
- **Used in:** SCR-004, SCR-014, SCR-015

### RiskBadge (UXR-403)
- **Rule:** Colour + icon + text (never colour alone)
- Low: green ✓ · Medium: amber ● · High: red ⚠
- **Used in:** SCR-011

### StatusBadge
- Confirmed · Walk-in · Arrived · In Progress · Complete · Extracting · Failed · Pending · Active · Inactive
- **Used in:** SCR-009, SCR-010, SCR-011, SCR-014, SCR-015, SCR-017

### SearchBar
- **Contains:** search icon + input + clear button
- **Autofocus** on page load (UXR-201)
- `role="search"` on container
- **Used in:** SCR-013, SCR-017
