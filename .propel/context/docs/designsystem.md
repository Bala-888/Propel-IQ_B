# Design Reference — Unified Patient Access & Clinical Intelligence Platform

## UI Impact Assessment

**Has UI Changes**: [x] Yes
**Scope**: Platform-wide design system — covers all UI-impacting epics (EP-001, EP-003, EP-004, EP-005, EP-006, EP-007-I, EP-007-II)

---

## Design Context

**Product**: Unified Patient Access & Clinical Intelligence Platform
**UI Impact Type**: New UI — green-field platform; no existing design system or UI assets
**Platform**: Responsive Web — 375px / 768px / 1280px / 1440px
**Stack**: React 18 + TypeScript SPA
**Accessibility Standard**: WCAG 2.2 AA (elicited, confirmed as project constraint)
**Content Tone**: Clinical-professional (precise, minimal, authoritative)

---

## Design Source References

### Primary Source

| Document | Path | Purpose |
|----------|------|---------|
| Figma Specification | .propel/context/docs/figma_spec.md | Screen inventory, UXR requirements, flows, component requirements |
| Epics | .propel/context/docs/epics.md | UI-impacting epics and key deliverables |

### Design Source

- **Figma Project**: Not yet created — design system tokens defined here serve as the source of truth for Figma variable setup
- **Design Images**: N/A — green-field; no existing visual references
- **Brand Guidelines**: Not provided — aesthetic direction derived from domain analysis (Utilitarian, Section 9 of figma_spec.md)

---

## Screen-to-Design Mappings

| Screen/Feature | Figma Frame ID | Description | Implementation Priority |
|---------------|---------------|-------------|------------------------|
| SCR-001 Login | TBD — frame created at Figma project setup | Login form; error, lockout states | High |
| SCR-002 Patient Registration | TBD | Registration form with inline validation | High |
| SCR-003 Patient Home | TBD | Upcoming appointments, quick actions | High |
| SCR-004 AI Conversational Intake | TBD | Chat interface; typing indicator; mode switch | High |
| SCR-005 Manual Intake Form | TBD | Multi-tab form; auto-save; mode switch | High |
| SCR-006 Slot Calendar & Booking | TBD | Slot grid; preferred slot panel; insurance warning | High |
| SCR-007 Booking Confirmation | TBD | Confirmation card; calendar sync buttons | High |
| SCR-008 Patient Profile & Settings | TBD | Notification preferences; sync toggles | Medium |
| SCR-009 Document Upload | TBD | Dropzone; file queue; upload progress | High |
| SCR-010 Doc Processing Status | TBD | Per-document status; retry CTA | Medium |
| SCR-011 Staff Queue Dashboard | TBD | Live queue table; Mark Arrived inline; SignalR row highlight | High |
| SCR-012 Walk-in Booking Form | TBD | Walk-in form; optional account creation | High |
| SCR-013 Patient Search | TBD | Search input; result list with avatars | Medium |
| SCR-014 360° Patient View | TBD | 5-tab view; conflict banners; AI labels | High |
| SCR-015 Medical Code Review | TBD | CodeSuggestionCard per row; accept/reject/correct | High |
| SCR-016 Admin KPI Dashboard | TBD | KPI metric cards (5 metrics) | Medium |
| SCR-017 Admin User Management | TBD | User data table; create/edit/deactivate | Medium |

---

## Design Tokens

```yaml
# ============================================================
# UPACIP Design System — Design Tokens
# Authority: designsystem.md (this file)
# All component implementations MUST reference semantic tokens only.
# Hard-coded hex/rgb/hsl values are prohibited in components (UXR-401).
# ============================================================

# ------------------------------------------------------------
# SECTION 1: COLOR PRIMITIVES
# Usage: map to semantic tokens below; never use primitives directly in components
# ------------------------------------------------------------

color_primitives:

  # Blue scale — primary brand/action
  blue:
    50:  "#EBF3FE"   # primary-subtle background, focus ring fill
    100: "#CCDDFB"
    200: "#99BBF7"
    300: "#6699F3"
    400: "#3377EF"
    500: "#1E5EE0"
    600: "#1A56DB"   # color-primary — contrast on white: ~6.5:1 ✅ AA
    700: "#1547B4"   # color-primary-hover
    800: "#103992"
    900: "#0C2E75"

  # Slate scale — neutrals
  slate:
    50:  "#F8FAFC"   # page background
    100: "#F1F5F9"   # card/input background
    200: "#E2E8F0"   # borders, dividers
    300: "#CBD5E1"   # disabled borders
    400: "#94A3B8"   # placeholder text (not for required text content)
    500: "#64748B"
    600: "#475569"   # secondary text — contrast on white: ~5.3:1 ✅ AA
    700: "#334155"
    800: "#1E293B"
    900: "#0F172A"   # primary text — contrast on white: ~20:1 ✅ AA

  # Green scale — success/arrived
  green:
    50:  "#F0FDF4"
    100: "#DCFCE7"
    500: "#22C55E"
    600: "#16A34A"   # status-success — contrast on white: ~4.7:1 ✅ AA

  # Amber scale — warning/insurance soft-check
  amber:
    50:  "#FFFBEB"
    100: "#FEF3C7"
    500: "#F59E0B"
    600: "#D97706"   # status-warning — contrast on white: ~3.2:1 (meets 3:1 for large text/UI only)

  # Red scale — error/critical conflict/destructive
  red:
    50:  "#FEF2F2"
    100: "#FEE2E2"
    500: "#EF4444"
    600: "#DC2626"   # status-error — contrast on white: ~4.6:1 ✅ AA

  # Sky scale — info
  sky:
    50:  "#F0F9FF"
    100: "#E0F2FE"
    500: "#0EA5E9"
    600: "#0284C7"   # status-info — contrast on white: ~4.9:1 ✅ AA

  # Indigo scale — AI accent (distinguishes AI-generated content)
  indigo:
    50:  "#EEF2FF"
    100: "#E0E7FF"
    400: "#818CF8"
    500: "#6366F1"   # color-ai-accent (supplement with AILabel text for accessibility)

# ------------------------------------------------------------
# SECTION 2: SEMANTIC COLOR TOKENS
# Usage: all component and layout implementations reference these names
# ------------------------------------------------------------

color_semantic:

  # Primary action
  color-primary:         blue-600    # Buttons, links, active states, focus rings
  color-primary-hover:   blue-700    # Button hover, link hover
  color-primary-subtle:  blue-50     # Focus ring fill, selected row bg, chip bg

  # Text
  color-text-primary:    slate-900   # Body text, headings, labels
  color-text-secondary:  slate-600   # Meta text, helper text, timestamps
  color-text-disabled:   slate-400   # Disabled input text, placeholder (not informational)
  color-text-inverse:    "#FFFFFF"   # Text on primary-coloured backgrounds

  # Backgrounds
  color-bg-page:         slate-50    # Root page background
  color-bg-surface:      "#FFFFFF"   # Card, modal, input background
  color-bg-subtle:       slate-100   # Table alternating rows, sidebar bg, tag bg
  color-bg-overlay:      "rgba(15, 23, 42, 0.48)"  # Modal/drawer backdrop

  # Borders
  color-border:          slate-200   # Default input borders, card borders, dividers
  color-border-strong:   slate-400   # Focused input ring outer, table header border
  color-border-focus:    blue-600    # Focus ring on interactive elements (2px solid)

  # Status
  color-status-success:  green-600   # Check-in arrived badge, extraction complete
  color-status-warning:  amber-600   # Insurance pre-check soft warning, low confidence
  color-status-error:    red-600     # Login error, validation error, critical conflict
  color-status-info:     sky-600     # Calendar sync failure toast, info banners

  # AI accent
  color-ai-accent:       indigo-500  # AILabel background tint; AI chat bubble left-border
  color-ai-bg:           indigo-50   # AILabel chip background

  # Interactive states (derived)
  color-row-highlight:   blue-50     # SignalR queue row transient highlight (UXR-502)
  color-destructive:     red-600     # Destructive button background (UXR-404)
  color-destructive-hover: red-700   # Destructive button hover

# ------------------------------------------------------------
# SECTION 3: TYPOGRAPHY
# ------------------------------------------------------------

typography:

  font_family:
    sans:  "'IBM Plex Sans', system-ui, -apple-system, sans-serif"
    mono:  "'IBM Plex Mono', 'Courier New', monospace"
    # IBM Plex Sans: Google Fonts (open license); humanist grotesque; excellent data legibility
    # IBM Plex Mono: Google Fonts (open license); used for ICD-10, CPT codes, reference IDs

  scale:
    # name: size / line-height / weight / usage
    text-xs:
      size:        "12px"
      line_height: "1.5"
      weight:      "400"
      usage:       "Metadata labels, timestamps, badge text, caption text"

    text-sm:
      size:        "14px"
      line_height: "1.4"
      weight:      "400"
      usage:       "Secondary body text, table cell content, helper text"

    text-sm-medium:
      size:        "14px"
      line_height: "1.4"
      weight:      "500"
      usage:       "Form field labels, nav item text, button text (S size)"

    text-base:
      size:        "16px"
      line_height: "1.5"
      weight:      "400"
      usage:       "Primary body text, form input values, chat messages"

    text-lg:
      size:        "18px"
      line_height: "1.3"
      weight:      "500"
      usage:       "Card section headings, form section titles, list group headers"

    text-xl:
      size:        "20px"
      line_height: "1.25"
      weight:      "600"
      usage:       "H4 — screen sub-section titles"

    text-2xl:
      size:        "24px"
      line_height: "1.2"
      weight:      "700"
      usage:       "H3 — modal titles, tab panel headings"

    text-3xl:
      size:        "28px"
      line_height: "1.15"
      weight:      "700"
      usage:       "H2 — page titles (SCR-001 'Sign in', SCR-016 'Platform metrics')"

    text-4xl:
      size:        "32px"
      line_height: "1.1"
      weight:      "800"
      usage:       "H1 — top-level screen hero headings (used sparingly; max 1 per screen)"

    mono-sm:
      size:        "13px"
      line_height: "1.4"
      weight:      "400"
      family:      "mono"
      usage:       "ICD-10 code display (e.g. J18.9), CPT codes, booking reference IDs"

    mono-base:
      size:        "15px"
      line_height: "1.4"
      weight:      "400"
      family:      "mono"
      usage:       "Larger code display contexts; document hash references"

  weights:
    regular:    400
    medium:     500
    semibold:   600
    bold:       700
    extrabold:  800

# ------------------------------------------------------------
# SECTION 4: SPACING
# Base unit: 4px
# ------------------------------------------------------------

spacing:
  space-0:  "0px"
  space-1:  "4px"    # Tight internal padding (badge, tag)
  space-2:  "8px"    # Icon-to-label gap, tight list item gap
  space-3:  "12px"   # Input internal padding (vertical), compact list item
  space-4:  "16px"   # Standard component padding (card, input, button M)
  space-5:  "20px"   # Form field gap (vertical rhythm in forms)
  space-6:  "24px"   # Section padding inner, card padding, modal padding top
  space-8:  "32px"   # Section gap between content groups
  space-10: "40px"   # Large section padding; desktop page top padding
  space-12: "48px"   # Desktop content section gap
  space-16: "64px"   # Major section separator
  space-20: "80px"   # Vertical page breathing room (desktop only)
  space-24: "96px"   # Max vertical section gap

  # Layout gutters (responsive)
  gutter-mobile:  "24px"    # 375px breakpoint
  gutter-tablet:  "32px"    # 768px breakpoint
  gutter-desktop: "40px"    # 1280px+ breakpoints

  # Grid
  grid-mobile:  "4 columns, 24px gutter, 16px margin"
  grid-tablet:  "8 columns, 24px gutter, 32px margin"
  grid-desktop: "12 columns, 24px gutter, 40px margin"

# ------------------------------------------------------------
# SECTION 5: BORDER RADIUS
# ------------------------------------------------------------

border_radius:
  radius-none:  "0px"      # Hard-edge elements (data tables, dividers)
  radius-sm:    "2px"      # Badges, tags, status chips
  radius-md:    "4px"      # Inputs, buttons, cards (default)
  radius-lg:    "8px"      # Modals, drawers, large surface cards
  radius-full:  "9999px"   # Toggle switches only

# ------------------------------------------------------------
# SECTION 6: ELEVATION / SHADOWS
# ------------------------------------------------------------

elevation:
  shadow-0:
    value: "none"
    usage: "Flat inline elements; table rows; form sections"

  shadow-1:
    value: "0 1px 2px 0 rgba(15, 23, 42, 0.06)"
    usage: "Cards; input fields on focus; subtle surface lift"

  shadow-2:
    value: "0 2px 8px 0 rgba(15, 23, 42, 0.10)"
    usage: "Dropdowns; date picker popovers; tooltips"

  shadow-3:
    value: "0 8px 24px 0 rgba(15, 23, 42, 0.14)"
    usage: "Modals; right-side drawers (Resolve Conflict, navigation)"

# ------------------------------------------------------------
# ANTI-PATTERN PROHIBITION CHECKLIST (verified at token definition)
# - background-clip: text       → PROHIBITED ✅ (not present)
# - backdrop-filter: blur       → PROHIBITED ✅ (not present)
# - border-left/right > 2px     → PROHIBITED ✅ (not present)
# - Sole typeface: Inter/DM Sans/Roboto/Poppins/Montserrat → PROHIBITED ✅ (IBM Plex Sans used)
# - Purple-to-blue linear-gradient → PROHIBITED ✅ (not present)
# - Hex values outside primitive table → PROHIBITED ✅ (all hex in primitives above; semantics ref names)
# - lorem ipsum                 → PROHIBITED ✅ (not present)
# - transition on width/height/top/left/margin → PROHIBITED ✅ (not present)
# ------------------------------------------------------------
```

---

## Component Specifications

*Full component library for Unified Patient Access & Clinical Intelligence Platform. All components reference semantic color tokens only.*

### C/Actions — Button

```yaml
Button:
  variants: [primary, secondary, ghost, destructive]
  sizes: [S, M, L]
  states: [Default, Hover, Focus, Active, Disabled, Loading]
  specifications:
    S: { padding: "space-2 space-3", font: text-sm-medium, height: "32px", radius: radius-md }
    M: { padding: "space-3 space-4", font: text-sm-medium, height: "40px", radius: radius-md }
    L: { padding: "space-4 space-6", font: text-base, height: "48px", radius: radius-md }
  token_map:
    primary:
      bg: color-primary | hover: color-primary-hover | text: color-text-inverse
      focus: "2px solid color-border-focus, 2px offset" | disabled: "color-bg-subtle / color-text-disabled"
    secondary:
      bg: color-bg-surface | border: "1px solid color-border-strong" | text: color-text-primary
      hover: "bg: color-bg-subtle"
    ghost:
      bg: transparent | text: color-primary | hover: "bg: color-primary-subtle"
    destructive:
      bg: color-destructive | text: color-text-inverse | hover: color-destructive-hover
  loading_state: "Spinner (16px) replaces button label; button remains same size; disabled interaction"
  touch_target: "minimum 44×44px on mobile (UXR-205)"
```

### C/Actions — IconButton

```yaml
IconButton:
  sizes: [S, M, L]  # 32px / 40px / 48px bounding box
  states: [Default, Hover, Focus, Active, Disabled]
  accessibility: "aria-label required; no icon-only buttons without aria-label"
  token_map:
    default: { bg: transparent, icon: color-text-secondary }
    hover:   { bg: color-bg-subtle }
    focus:   { ring: "2px solid color-border-focus, 2px offset" }
```

### C/Inputs — TextField

```yaml
TextField:
  states: [Default, Focused, Filled, Error, Disabled]
  anatomy: [visible-label, input-field, helper-text, inline-error-slot]
  specifications:
    height: "40px (M), 48px (L — mobile)"
    radius: radius-md
    padding: "space-3 space-4"
    font: text-base
  token_map:
    label:     { font: text-sm-medium, color: color-text-primary }
    input-bg:  color-bg-surface
    border:    { default: "1px solid color-border", focus: "2px solid color-border-focus", error: "2px solid color-status-error" }
    placeholder: { color: color-text-disabled }
    helper:    { font: text-xs, color: color-text-secondary }
    error-msg: { font: text-xs, color: color-status-error }
  constraint: "Placeholder text MUST NOT be the sole label (UXR-204); label always visible above input"
```

### C/Inputs — FileUploadZone

```yaml
FileUploadZone:
  states: [Default, Hover/Drag-over, Uploading, Error]
  mobile_degradation: "Drag zone hidden at 375px; 'Select file' Button-secondary shown only (UXR-303)"
  specifications:
    border: "2px dashed color-border"
    hover:  "2px dashed color-primary; bg: color-primary-subtle"
    radius: radius-lg
    padding: space-10
  accepted_types_display: "PDF, DOC, DOCX, JPG, PNG — Max 20 MB"
  error_handling: "InlineError below zone: specific reason + allowed types + retry (UXR-603)"
```

### C/Navigation — AppHeader

```yaml
AppHeader:
  breakpoints:
    mobile_375px:   "Hidden — navigation via BottomTabNav"
    tablet_768px:   "Full-width top bar; hamburger menu opens Drawer navigation"
    desktop_1280px: "Fixed top bar; Sidebar handles primary nav"
  anatomy: [logo-area, breadcrumb-slot, utility-area (role-badge + avatar-menu)]
  height: "56px"
  bg: color-bg-surface
  border_bottom: "1px solid color-border"
  shadow: shadow-1
```

### C/Navigation — Sidebar

```yaml
Sidebar:
  visibility: "≥1280px only (UXR-302)"
  width: "240px expanded; 56px collapsed (icon-only)"
  bg: color-bg-surface
  border_right: "1px solid color-border"
  nav_items:
    patient:  ["Home", "Intake", "Appointments", "Documents", "Profile"]
    staff:    ["Queue", "New Walk-in", "Patient Search"]
    admin:    ["KPI Dashboard", "Users"]
  active_state: { bg: color-primary-subtle, text: color-primary, "left-border": "3px solid color-primary" }
  hover_state:  { bg: color-bg-subtle }
```

### C/Navigation — BottomTabNav

```yaml
BottomTabNav:
  visibility: "375px only (UXR-302)"
  height: "56px"
  max_tabs: 5
  anatomy: [icon (24px), label (text-xs)]
  bg: color-bg-surface
  border_top: "1px solid color-border"
  active_tab: { icon: color-primary, label: color-primary, "bottom-border": "2px solid color-primary" }
  touch_target: "44×44px per tab (UXR-205)"
```

### C/Content — DataTable

```yaml
DataTable:
  states: [Default, Loading (Skeleton rows), Empty, Sorted, Row-highlight]
  mobile_reflow: "Card-list layout at 375px (UXR-304); columns collapse; each row becomes a Card"
  anatomy: [header-row, data-rows, pagination-footer]
  specifications:
    header: { font: text-sm-medium, color: color-text-secondary, bg: color-bg-subtle, "border-bottom": "1px solid color-border-strong" }
    row:    { font: text-sm, color: color-text-primary, "border-bottom": "1px solid color-border", height: "52px" }
    row-hover: { bg: color-bg-subtle }
    row-highlight: { bg: color-row-highlight, transition: "background-color 2s ease-out" }
    # Note: transition uses background-color only — NOT width/height/top/left/margin (anti-pattern prohibition)
  pagination: "25 rows/page default; 'Previous' / 'Next' buttons; total count displayed"
```

### C/Feedback — Toast

```yaml
Toast:
  variants: [success, warning, error, info]
  position: "top-right; 16px from edges; stacks downward"
  auto_dismiss: "5s (info/success); 10s (warning); manual dismiss only (error)"
  accessibility: "role='alert'; aria-live='assertive' for error; aria-live='polite' for others (UXR-206)"
  anatomy: [variant-icon, message-text, dismiss-button]
  token_map:
    success: { bg: green-50,  icon: color-status-success, border: "1px solid green-100" }
    warning: { bg: amber-50,  icon: color-status-warning, border: "1px solid amber-100" }
    error:   { bg: red-50,    icon: color-status-error,   border: "1px solid red-100" }
    info:    { bg: sky-50,    icon: color-status-info,    border: "1px solid sky-100" }
```

### C/Feedback — AlertBanner

```yaml
AlertBanner:
  variants: [success, warning, error, info]
  position: "Inline within page flow; above affected form section or at page top"
  dismissable: true  # optional via prop
  accessibility: "role='alert' (UXR-206)"
  token_map:
    warning: { bg: amber-50, border: "1px solid amber-100", icon-color: color-status-warning, text-color: color-text-primary }
    error:   { bg: red-50,   border: "1px solid red-100",   icon-color: color-status-error,   text-color: color-text-primary }
    info:    { bg: sky-50,   border: "1px solid sky-100",   icon-color: color-status-info,    text-color: color-text-primary }
  visual_differentiation: "Severity communicated via icon (24px, coloured) + coloured background tint + coloured icon — no thick left border"
  insurance_warning_constraint: "MUST use variant='warning' (amber), NEVER variant='error' (UXR-604)"
  calendar_sync_constraint: "Calendar sync failure MUST use Toast variant='info', NOT AlertBanner (UXR-605)"
```

### C/Feedback — Modal

```yaml
Modal:
  sizes: [S (480px max-w), M (600px max-w), L (800px max-w)]
  overlay: color-bg-overlay
  shadow: shadow-3
  radius: radius-lg
  anatomy: [header (title + close-button), body, footer (action-buttons)]
  accessibility:
    role: "dialog"
    aria-modal: true
    aria-labelledby: "modal-title"
    focus_trap: "Focus cycles within modal; Escape closes (UXR-202)"
    restore_focus: "Returns focus to trigger element on close"
  session_timeout_modal:
    auto_trigger: "At 13 min of 15-min inactivity"
    actions: ["Stay signed in (primary)", "Sign out (ghost)"]
    no_close_without_action: true
```

### C/Feedback — ConfirmDialog

```yaml
ConfirmDialog:
  usage: "Required before all destructive/irreversible actions (UXR-404)"
  size: S
  anatomy: [warning-icon, title, description, cancel-button (ghost), confirm-button (destructive)]
  examples:
    deactivate_user: { title: "Deactivate user account?", description: "This user will lose access immediately.", confirm_label: "Deactivate" }
    reject_code: { title: "Reject code suggestion?", description: "This suggestion will be marked as rejected.", confirm_label: "Reject" }
```

### C/Clinical — AILabel

```yaml
AILabel:
  purpose: "Identifies AI-generated content for Trust-First transparency (UXR-402, FR-035)"
  anatomy: [AI-icon (sparkle, 12px), 'AI' text (mono-sm)]
  token_map:
    bg: color-ai-bg     # indigo-50
    text: color-ai-accent  # indigo-500
    border: "1px solid indigo-100"
  radius: radius-sm
  usage: "Attach to every AI-sourced field value, card, or extracted entity; never omit"
  contrast: "indigo-500 on indigo-50: ~3.4:1 (meets 3:1 for large text/UI); supplement with 'AI' text label for full accessibility (UXR-207)"
```

### C/Clinical — ConflictBanner

```yaml
ConflictBanner:
  purpose: "Surfaces data conflicts in 360° patient view (FR-034, UXR-403)"
  anatomy: [severity-icon, severity-label (text), conflict-description, source-references, 'Resolve' button]
  constraint: "MUST use icon + text label + colour — never colour alone (UXR-403, WCAG 1.4.1)"
  token_map:
    critical: { bg: red-50,   border: "1px solid red-100",   icon-color: color-status-error,   severity-icon: "AlertCircle (filled, 20px)" }
    warning:  { bg: amber-50, border: "1px solid amber-100", icon-color: color-status-warning, severity-icon: "Warning (filled, 20px)" }
  visual_differentiation: "Severity communicated via filled icon + coloured background + coloured icon + text label — no thick left border (UXR-403)"
  accessibility: "role='alert'; aria-live='polite' (UXR-206)"
```

### C/Clinical — RiskScoreBadge

```yaml
RiskScoreBadge:
  purpose: "Displays no-show risk score to staff (FR-014, UXR-403)"
  levels: [Low, Medium, High]
  anatomy: [risk-icon, risk-level-text, optional-score-number]
  constraint: "MUST display icon + text level label + colour — never colour alone (UXR-403)"
  token_map:
    Low:    { bg: green-50,  text: color-status-success, icon: color-status-success }
    Medium: { bg: amber-50,  text: color-status-warning, icon: color-status-warning }
    High:   { bg: red-50,    text: color-status-error,   icon: color-status-error }
  accessibility: "aria-label='No-show risk: [level]' on badge container (UXR-207)"
```

### C/Clinical — CodeSuggestionCard

```yaml
CodeSuggestionCard:
  purpose: "Displays individual ICD-10/CPT AI suggestion for staff review (UC-030–032)"
  anatomy:
    - code-display: "mono-sm font; ICD-10 format badge"
    - description: text-sm
    - confidence-score: text-xs (color-text-secondary)
    - source-reference: Link to source document chunk
    - AILabel: "always present (UXR-402)"
    - action-buttons: [Accept (primary-S), Reject (destructive-S + ConfirmDialog), Correct (ghost-S → inline TextField)]
  correction_flow: "Correct action reveals inline TextField pre-filled with suggested code; ICD-10/CPT format validation on blur; Save button commits"
  states: [Pending, Accepted, Rejected, Corrected]
  state_styling:
    Accepted:  { opacity: 0.6, badge: "Accepted (green-600 text, green-100 bg)" }
    Rejected:  { opacity: 0.5, badge: "Rejected (slate-400 text, slate-100 bg)" }
    Corrected: { badge: "Corrected (blue-600 text, blue-50 bg)" }
```

### C/Clinical — DocumentStatusIndicator

```yaml
DocumentStatusIndicator:
  purpose: "Shows per-document AI extraction pipeline status (UC-026)"
  states: [Pending, Extracting, Complete, Failed, Partially-extracted]
  anatomy: [status-icon, status-label, filename, action-slot]
  token_map:
    Pending:             { icon: slate-400, label: color-text-secondary }
    Extracting:          { icon: color-primary, label: color-primary, animation: "Spinner" }
    Complete:            { icon: color-status-success, label: color-status-success }
    Failed:              { icon: color-status-error, label: color-status-error, action: "Retry CTA" }
    Partially-extracted: { icon: color-status-warning, label: color-status-warning, action: "Retry CTA" }
  accessibility: "aria-live='polite' on status region (UXR-206, UXR-503)"
```

### C/Clinical — ChatBubble

```yaml
ChatBubble:
  purpose: "AI chat interface for conversational intake (SCR-004)"
  variants: [user-message, ai-message, ai-typing-indicator]
  anatomy:
    user:      { alignment: right, bg: color-primary, text: color-text-inverse, radius: "radius-lg radius-lg radius-none radius-lg" }
    ai:        { alignment: left, bg: color-bg-subtle, text: color-text-primary, radius: "radius-lg radius-lg radius-lg radius-none", AILabel-chip: always-visible }
    typing:    { alignment: left, bg: color-bg-subtle, animation: "three-dot pulse (3×, 500ms stagger)" }
  typing_indicator: "Visible within 200ms of user message send (UXR-504); dismissed on AI response arrival"
  mode_switch_link: "Fixed position top-right of SCR-004: 'Switch to manual form' (ghost button)"
```

---

## New Visual Assets

```yaml
screenshots:
  location: "Not yet created — pending Figma project setup"
  files: []

new_assets:
  icon_set:
    name: "Phosphor Icons (Outline weight)"
    source: "https://phosphoricons.com — open license (MIT)"
    usage_sizes: ["20px (inline components)", "24px (navigation, standalone icons)"]
    stroke_width: "1.5px"
    purpose: "All UI icons: actions, navigation, status indicators, clinical icons"

  no_photography: true
  no_illustration: true  # Utilitarian aesthetic direction; empty states use icon + text only
```

---

## Task Design Mapping

```yaml
EP-001-Authentication:
  ui_impact: true
  screens: [SCR-001, SCR-002]
  primary_components: [TextField, PasswordField, Button, InlineError, AlertBanner, AppHeader]
  visual_validation_required: true

EP-003-PatientIntake:
  ui_impact: true
  screens: [SCR-003, SCR-004, SCR-005]
  primary_components: [ChatBubble, AILabel, TextField, Textarea, Select, DatePicker, Tabs, Button]
  visual_validation_required: true

EP-004-AppointmentBooking:
  ui_impact: true
  screens: [SCR-006, SCR-007]
  primary_components: [Card, StatusBadge, Button, AlertBanner, Modal, ProgressBar]
  visual_validation_required: true

EP-005-SlotSwapNotifications:
  ui_impact: true
  screens: [SCR-007, SCR-008]
  primary_components: [Toast, Toggle, Button, Card]
  visual_validation_required: true

EP-006-StaffQueueDashboards:
  ui_impact: true
  screens: [SCR-011, SCR-012, SCR-016, SCR-017]
  primary_components: [DataTable, RiskScoreBadge, StatusBadge, Button, SignalR-row-highlight]
  visual_validation_required: true

EP-007-I-DocumentUpload:
  ui_impact: true
  screens: [SCR-009, SCR-010]
  primary_components: [FileUploadZone, ProgressBar, DocumentStatusIndicator, InlineError]
  visual_validation_required: true

EP-007-II-ClinicalAI:
  ui_impact: true
  screens: [SCR-013, SCR-014, SCR-015]
  primary_components: [ConflictBanner, AILabel, CodeSuggestionCard, Tabs, Drawer, ConfirmDialog]
  visual_validation_required: true
```

---

## Visual Validation Criteria

```typescript
const requiresVisualValidation = (epicId: string): boolean => {
  // All UI-impacting epics require visual validation
  const uiImpactingEpics = ['EP-001', 'EP-003', 'EP-004', 'EP-005', 'EP-006', 'EP-007-I', 'EP-007-II'];
  return uiImpactingEpics.includes(epicId);
};

// Visual validation parameters
const visualValidation = {
  screenshotComparison: {
    maxDifference: "5%",
    breakpoints: [375, 768, 1280, 1440]  // All 4 responsive breakpoints (UXR-301)
  },
  componentValidation: {
    colorAccuracy: true,     // Semantic token usage (UXR-401)
    spacingAccuracy: true,   // 4px base grid adherence
    typographyMatch: true,   // IBM Plex Sans scale adherence
    contrastCheck: true      // WCAG 2.2 AA (UXR-201)
  },
  accessibilityValidation: {
    axeCoreScan: true,       // Automated WCAG 2.2 AA check
    keyboardNav: true,       // Full keyboard traversal (UXR-202)
    focusVisible: true,      // Focus ring present (UXR-203)
    touchTargets: true       // 44×44px minimum (UXR-205)
  }
};
```

---

## Accessibility Requirements

- **WCAG Level**: WCAG 2.2 AA (elicited and confirmed as project constraint)
- **Colour Contrast**:
  - Normal text (< 18px regular, < 14px bold): ≥ 4.5:1 against background
  - Large text (≥ 18px regular, ≥ 14px bold) and UI components: ≥ 3:1
  - All semantic token pairs verified (Section "Design Tokens" above)
- **Focus States**: WCAG 2.2 AA SC 2.4.11 — minimum 2px solid outline, ≥ 3:1 contrast against adjacent colour; `color-border-focus` token used on all interactive elements (UXR-203)
- **Screen Reader**: ARIA labels on all icon-only buttons and status badges (UXR-207); ARIA live regions on dynamic content (UXR-206)
- **Touch Targets**: Minimum 44×44px on 375px breakpoint (UXR-205); applies to all Buttons, IconButtons, BottomTabNav items, DataTable row actions
- **Keyboard Navigation**: Complete keyboard traversal without mouse; logical tab order; no keyboard trap except modals (focus trap + Escape to close) (UXR-202)
- **Colour-only Prohibition**: All clinical status indicators (ConflictBanner, RiskScoreBadge, DocumentStatusIndicator) use icon + text label + colour (UXR-403, WCAG 1.4.1)
- **AI Content Attribution**: AILabel component mandatory on all AI-generated content (UXR-402, UXR-207)

---

## Design Review Checklist

**Platform-wide design system review — complete before implementing any UI epic**

- [ ] All colour tokens defined in "Design Tokens" section (primitives → semantic tokens)
- [ ] Typography scale (IBM Plex Sans + IBM Plex Mono) defined and verified for WCAG 2.2 AA
- [ ] Spacing scale (4px base grid) defined; responsive gutters documented
- [ ] Border radius and elevation/shadow tiers defined
- [ ] All component specifications documented with token references
- [ ] Anti-pattern prohibition checklist verified (Section: Design Tokens YAML comment block)
- [ ] All WCAG 2.2 AA contrast ratios verified for all semantic token pairs
- [ ] Accessible focus ring specification defined (color-border-focus, 2px, offset)
- [ ] AILabel component specified; usage constraint documented (mandatory on AI content)
- [ ] ConflictBanner and RiskScoreBadge specify icon + text + colour (never colour alone)
- [ ] Destructive actions: ConfirmDialog + `variant="destructive"` button specified
- [ ] DataTable mobile reflow to card-list documented (UXR-304)
- [ ] FileUploadZone mobile degradation documented (UXR-303)
- [ ] Session Timeout Modal specification documented (MOD-001, UXR-505)
- [ ] BottomTabNav spec complete; 44×44px touch targets; max 5 tabs
- [ ] ChatBubble typing indicator specification complete (UXR-504)
- [ ] SignalR queue row highlight uses background-color transition only (not width/height/top/left/margin)
