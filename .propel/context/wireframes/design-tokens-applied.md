# Design Tokens Applied

## Aesthetic Direction

**Direction:** Utilitarian

**Precedents:** Epic EMR · NHS Digital · Linear

**Core personality:** Functional clarity over decorative expression. Every visual decision earns its place by reducing cognitive load in high-pressure clinical workflows. Density is appropriate; whitespace is purposeful, not generous. Typography carries hierarchy weight; color carries meaning, not decoration.

**Anti-patterns avoided (per QG-11 and `/frontend-design anti-patterns`):**
- No `border-left: 4px solid` decorative strips (replaced with `bg + icon + border: 1px solid`)
- No `box-shadow: 0 20px 60px rgba(0,0,0,0.3)` (maximum shadow is `--shadow-3`)
- No glassmorphism or gradient backgrounds
- No lorem ipsum (all content from `sample-data.json`)
- No color-only information encoding (UXR-105, UXR-403)

---

## Token Sources

All tokens are defined inline in the `:root` block of every HTML file. No external stylesheet imports. Token naming follows semantic, not literal, conventions (e.g., `--color-primary` not `--color-blue`).

### Source file
All tokens originate from `designsystem.md` (UPACIP design system specification) and are applied verbatim in every wireframe.

---

## Token Application by Screen

### Color tokens

| Token | Value | Usage |
|---|---|---|
| `--color-primary` | `#1A56DB` | Buttons, active nav, links, focus rings |
| `--color-primary-hover` | `#1547B4` | Primary button hover state |
| `--color-primary-subtle` | `#EBF3FE` | Active nav background, row highlight, soft card fill |
| `--color-text-primary` | `#0F172A` | All body text, headings |
| `--color-text-secondary` | `#475569` | Labels, meta, placeholder context |
| `--color-text-disabled` | `#94A3B8` | Inactive states, placeholder text |
| `--color-text-inverse` | `#FFFFFF` | Button text on primary/destructive |
| `--color-bg-page` | `#F8FAFC` | Page background |
| `--color-bg-surface` | `#FFFFFF` | Cards, panels, sidebar, modals |
| `--color-bg-subtle` | `#F1F5F9` | Table headers, input backgrounds, quiet zones |
| `--color-bg-overlay` | `rgba(15,23,42,0.48)` | Modal/drawer backdrop |
| `--color-border` | `#E2E8F0` | All default borders |
| `--color-border-strong` | `#94A3B8` | Focused inputs hover, search bars |
| `--color-border-focus` | `#1A56DB` | Focus ring color (2px solid) |
| `--color-status-success` | `#16A34A` | Success badges, accepted states, complete |
| `--color-status-warning` | `#D97706` | Warning alerts, no-show rate, amber badges |
| `--color-status-error` | `#DC2626` | Error states, conflict banners, failed badges |
| `--color-status-info` | `#0284C7` | Info toasts, informational banners |
| `--color-ai-accent` | `#4F46E5` | AILabel text, AI message bubbles — **corrected from #6366F1 for WCAG 1.4.3 compliance (contrast 5.1:1 on #EEF2FF)** |
| `--color-ai-bg` | `#EEF2FF` | AILabel background, AI chat bubbles |
| `--color-row-highlight` | `#EBF3FE` | New queue row animation (UXR-502) |
| `--color-destructive` | `#DC2626` | Destructive action buttons |
| `--color-destructive-hover` | `#B91C1C` | Destructive button hover |
| `--color-status-error-bg` | `#FEF2F2` | Error alert band background |
| `--color-status-error-border` | `#FEE2E2` | Error alert band border |
| `--color-status-error-text` | `#7F1D1D` | Body text inside error bands |
| `--color-status-warning-bg` | `#FFFBEB` | Warning alert band background |
| `--color-status-warning-border` | `#FEF3C7` | Warning alert band border |
| `--color-status-success-bg` | `#F0FDF4` | Success alert band background |
| `--color-status-success-border` | `#BBF7D0` | Success alert band border |
| `--color-status-info-bg` | `#F0F9FF` | Info alert band background |
| `--color-status-info-border` | `#E0F2FE` | Info alert band border |

### Typography tokens

| Token | Value | Usage |
|---|---|---|
| `--font-sans` | `'IBM Plex Sans', system-ui, -apple-system, sans-serif` | All body text, labels, buttons |
| `--font-mono` | `'IBM Plex Mono', 'Courier New', monospace` | Medical codes, booking references, patient IDs, KPI values |

**Type scale (applied as inline px values, not tokens):**
- 28–30px: Page headings
- 20–22px: Card/panel headings
- 16–18px: Section titles
- 15px: Body, form values
- 14px: Nav items, labels, standard text
- 13px: Meta, hints, descriptions
- 12px: Badges, table headers, tiny labels
- 11px: AILabel, micro badges

### Spacing tokens

| Token | Value | Primary usage |
|---|---|---|
| `--space-1` | `4px` | Tight icon gaps |
| `--space-2` | `8px` | Inline gaps, compact rows |
| `--space-3` | `12px` | Nav item padding, compact form groups |
| `--space-4` | `16px` | Table cells, card padding, standard gaps |
| `--space-5` | `20px` | Form row gaps, entity card padding |
| `--space-6` | `24px` | Section separators, card padding |
| `--space-8` | `32px` | Page header padding, major section gaps |
| `--space-10` | `40px` | Content area padding |
| `--space-12` | `48px` | Drop zone padding |
| `--space-16` | `64px` | Hero-level breathing room |

### Border radius tokens

| Token | Value | Usage |
|---|---|---|
| `--radius-xs` | `2px` | Inline badges, tiny chips |
| `--radius-sm` | `4px` | Buttons, inputs, nav items, table wrappers |
| `--radius-md` | `8px` | Cards, panels, modals, drawers |
| `--radius-full` | `9999px` | Toggle sliders, pill badges, AILabel |

### Shadow tokens

| Token | Value | Usage |
|---|---|---|
| `--shadow-1` | `0 1px 3px rgba(15,23,42,0.08)` | Cards, table wrappers, file items |
| `--shadow-2` | `0 4px 12px rgba(15,23,42,0.10)` | Confirmation cards, search bars, toasts |
| `--shadow-3` | `0 8px 24px rgba(15,23,42,0.14)` | Modals, drawers, dialogs |

### Sidebar token

| Token | Value | Usage |
|---|---|---|
| `--sidebar-width` | `240px` | Persistent left sidebar on all authenticated screens ≥1280px |

---

## Drift Notes

### Intentional divergences from `designsystem.md`

1. **Modal border-radius:** `--radius-md` (8px) used instead of 6px specified in some DSM entries — consistent with card radius across all screens for visual harmony.

2. **Conflict/Alert banner:** `border-left: 4px solid` removed and replaced with `icon + bg + border: 1px solid` pattern throughout all alert variants. This was a QG-11 finding from the designsystem.md review session and is the corrected implementation.

3. **Drawer width:** MOD-005 uses 360px (wider than the 320px spec) to accommodate the radio option group content without text truncation at 1440px.

4. **SignalR bar background:** `#DCFCE7` (success green-50) used instead of a custom token — intentional; no semantic token covers transient status bars. Tagged for design system extension.

5. **KPI value font-size:** 36px used in SCR-016 rather than a token (no `--text-display` token in current designsystem.md). Tagged for DS extension.

### Tokens not yet in design system (candidate additions)

| Candidate token | Proposed value | Rationale |
|---|---|---|
| `--color-status-bar-live` | `#DCFCE7` | SignalR live status bar background |
| `--text-display` | `36px / 700` | Large KPI and metric display values |
| `--drawer-width` | `360px` | Right-side drawer standard width |
| `--modal-max-width-sm` | `420px` | Small confirm dialogs |
| `--modal-max-width-md` | `520px` | Standard create/edit modals |
| `--modal-max-width-lg` | `560px` | Confirmation summary modals |
