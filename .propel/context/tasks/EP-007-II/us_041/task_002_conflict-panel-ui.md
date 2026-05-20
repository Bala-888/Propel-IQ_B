# Task - TASK_002

## Requirement Reference
- **User Story:** us_041
- **Story Location:** .propel/context/tasks/EP-007-II/us_041/us_041.md
- **Acceptance Criteria:**
  - AC-003: Open conflicts are displayed on SCR-014 in the Extracted data panel; each conflict shows `conflictType`, `description`, and a severity badge with text + icon; "Resolve conflict →" button is present per conflict
- **Edge Cases:**
  - No open conflicts: if `summary.conflicts` is empty, no conflict banners are rendered — the Extracted data panel shows only the entity list (absence of banners is the correct no-conflict state)

---

## Design References
| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes — modifies SCR-014 Extracted data panel |
| **Figma URL** | N/A |
| **Wireframe Status** | AVAILABLE |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | .propel/context/wireframes/Hi-Fi/wireframe-SCR-014-360-patient-view.html |
| **Screen Spec** | SCR-014 (360° Patient View — conflict banner in Extracted data panel, `panel-extracted`) |
| **UXR Requirements** | UXR-105 (conflict severity badge uses icon + text label, never color alone) |
| **Design Tokens** | `--color-status-error: #DC2626`, `--color-ai-accent: #6366F1`, `--color-ai-bg: #EEF2FF`, conflict banner: `background: #FEF2F2`, `border: 1px solid #FECACA`, conflict icon bg: `#FEE2E2`, button danger: `background: #FEF2F2; color: --color-status-error; border: #FECACA` |

---

## AI References
| Reference Type | Value |
|----------------|-------|
| **AI Impact** | No — this task only renders AI-detected conflicts; no AI model is invoked in the UI |
| **AIR Requirements** | AIR-005 — conflict data surfaced in the UI originates from the AI conflict detection pipeline; UXR-402 context: the conflict banner description communicates that conflicts were identified by AI analysis |
| **AI Pattern** | N/A |
| **Prompt Template Path** | N/A |
| **Guardrails Config** | N/A |
| **Model Provider** | N/A |

---

## Mobile References
| Reference Type | Value |
|----------------|-------|
| **Mobile Impact** | No |
| **Platform Target** | N/A |
| **Min OS Version** | N/A |
| **Mobile Framework** | N/A |

---

## Applicable Technology Stack

| Layer | Technology | Version | Justification |
|-------|------------|---------|---------------|
| Frontend | React + TypeScript | 18.x / 5.x | TR-001 — `<ConflictBanner>` and `<SeverityBadge>` components; `PatientViewPage` modification; TypeScript type additions (AC-003; UXR-105) |
| Routing | React Router v6 | v6 | TR-001 — "Resolve conflict →" button stub placeholder navigates to `/patients/${patientId}/conflicts/${conflictId}/resolve` (future MOD-005 drawer) |
| Styling | CSS custom properties | project standard | TR-002 — conflict banner design tokens from SCR-014 wireframe; severity badge colours (with text+icon never color-alone); `btn-danger` style (AC-003; UXR-105) |

---

## Task Overview

Extend SCR-014 `PatientViewPage` (us_040) to render clinical conflict banners in the Extracted data panel. Update `PatientSummaryDto` TypeScript type to include `conflicts: ConflictDto[]`. Implement `<ConflictBanner>` and `<SeverityBadge>` components matching the SCR-014 wireframe. Each banner uses `role="alert"` with conflict icon, description, severity badge (text + icon per UXR-105), and a stub "Resolve conflict →" button. No banners are rendered when the conflicts array is empty.

---

## Dependent Tasks
- task_001 (us_041) — `GET /patients/{id}/summary` must include `conflicts` array in response before the frontend can render conflict banners
- task_002 (us_040) — `PatientViewPage` and `PatientSummaryDto` TypeScript type must exist for modification

---

## Impacted Components
- `src/web/src/types/patient.ts` — modified (us_040 file): add `SeverityLevel`, `ConflictDto`, `EntitySummaryDto` types; add `conflicts: ConflictDto[]` to `PatientSummaryDto`
- `src/web/src/pages/PatientViewPage.tsx` — modified (us_040 file): add conflict banner list rendering at top of Extracted data panel
- `src/web/src/components/ConflictBanner.tsx` — new: conflict banner with icon, title, description, severity badge, and resolve stub button
- `src/web/src/components/SeverityBadge.tsx` — new: text + icon severity indicator (Low/Medium/High)
- `src/web/src/components/ConflictBanner.module.css` — new: banner background, border, icon circle, danger button styles from SCR-014 wireframe tokens

---

## Implementation Plan
1. TypeScript type additions in `src/web/src/types/patient.ts`: `type SeverityLevel = 'Low' | 'Medium' | 'High'`; `interface EntitySummaryDto { id: string; type: string; value: string }`; `interface ConflictDto { id: string; entityA: EntitySummaryDto; entityB: EntitySummaryDto; conflictType: string; description: string; severity: SeverityLevel; status: 'Open' | 'Resolved' }`; add `conflicts: ConflictDto[]` to `PatientSummaryDto` — `conflicts` is always an array (never null) because task_001 returns `[]` when no conflicts exist; `SeverityLevel` is a union type to prevent unknown severity strings reaching render logic (AC-003; TypeScript safety)
2. Conflict banner integration in `PatientViewPage.tsx`: inside the Extracted data panel (`panel-extracted`) render block, add `const openConflicts = summary.conflicts.filter(c => c.status === 'Open')` before the `<AiLabel>` disclaimer; render `{openConflicts.map(c => (<ConflictBanner key={c.id} conflict={c} patientId={id} />))}` — banners appear above entity cards matching the wireframe order; when `openConflicts.length === 0` the `map` renders nothing, which is the correct no-conflict state (no conditional or empty-state element needed) (AC-003; Edge: no conflicts)
3. `<ConflictBanner conflict={ConflictDto} patientId={string} />` component: `<div role="alert" aria-live="assertive" aria-label={`Clinical conflict detected: ${conflict.conflictType}`} className={styles.banner}>` containing: conflict icon circle `<div aria-hidden="true" className={styles.iconCircle}><ErrorCircleIcon /></div>`; content div with `<div className={styles.title}>{formatConflictType(conflict.conflictType)}</div>` (e.g., "Drug-allergy conflict detected") + `<p className={styles.desc}>{conflict.description}</p>` + `<SeverityBadge severity={conflict.severity} />` + "Resolve conflict →" stub button; CSS uses wireframe tokens: `background: #FEF2F2`, `border: 1px solid #FECACA`, icon bg `#FEE2E2`; `formatConflictType` maps enum values to human-readable labels (AC-003; wireframe `conflict-banner` pattern; UXR-105)
4. `<SeverityBadge severity={SeverityLevel} />` component: renders `<span className={styles.badge}>` containing `<SeverityIcon aria-hidden="true" />` + visible text `{severityText[severity]}`; `severityText` constant: `{ High: 'High severity', Medium: 'Medium severity', Low: 'Low severity' }`; icons: High → WarningCircle (red), Medium → WarningTriangle (amber), Low → InfoCircle (grey) — all `aria-hidden="true"` because text label is always present; badge `className` varies by severity for background tinting but text + icon are always rendered regardless of colour (UXR-105; WCAG 2.1 AA SC 1.4.1 — information not conveyed by colour alone)
5. "Resolve conflict →" stub button: `<button aria-label={`Resolve conflict: ${formatConflictType(conflict.conflictType)}`} onClick={() => { /* MOD-005 resolve drawer — not yet implemented */ console.warn('Conflict resolution drawer (MOD-005) not yet implemented') }} className={styles.resolveBtn}>Resolve conflict →</button>`; styled with wireframe `btn-danger` tokens (`background: #FEF2F2; color: var(--color-status-error); border: 1px solid #FECACA`); `min-height: 44px` for touch target compliance; accessible label includes conflict type to distinguish multiple "Resolve" buttons on the same page (wireframe `resolve-conflict-btn`; WCAG 2.1 AA SC 2.5.5 — 44px target; SC 2.4.6 — descriptive label)

---

## Current Project State
```
src/
└── web/
    └── src/
        ├── pages/
        │   └── PatientViewPage.tsx             (MODIFY — add conflict banner list in panel-extracted)
        ├── components/
        │   ├── (ConflictBanner.tsx             — CREATE)
        │   ├── (SeverityBadge.tsx              — CREATE)
        │   └── (ConflictBanner.module.css      — CREATE)
        └── types/
            └── patient.ts                      (MODIFY — add ConflictDto, SeverityLevel, update PatientSummaryDto)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | src/web/src/types/patient.ts | Add SeverityLevel, ConflictDto, EntitySummaryDto; add conflicts[] to PatientSummaryDto |
| MODIFY | src/web/src/pages/PatientViewPage.tsx | Render openConflicts.map(<ConflictBanner>) above entity cards in Extracted data panel |
| CREATE | src/web/src/components/ConflictBanner.tsx | role="alert" banner with icon, title, description, SeverityBadge, resolve stub button |
| CREATE | src/web/src/components/SeverityBadge.tsx | Text + icon severity indicator; icon aria-hidden; text always visible |
| CREATE | src/web/src/components/ConflictBanner.module.css | Banner, icon circle, danger button styles using SCR-014 wireframe tokens |

---

## External References
- .propel/context/wireframes/Hi-Fi/wireframe-SCR-014-360-patient-view.html (SCR-014 — `.conflict-banner`, `.conflict-icon`, `.conflict-title`, `.conflict-desc`, `.btn-danger` CSS classes; `role="alert"` + `aria-live="assertive"` on banner; `aria-label="Clinical conflict detected: Amoxicillin allergy to Penicillin-class"` pattern; `resolve-conflict-btn` button)
- https://www.w3.org/WAI/WCAG21/Understanding/use-of-color.html (WCAG SC 1.4.1 — text + icon required; UXR-105 severity badge must not use colour as the only visual means of conveying severity level)
- https://developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/Roles/alert_role (`role="alert"` + `aria-live="assertive"` — conflict detection is a high-priority announcement; AC-003)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] Load SCR-014 for a patient with 1 open conflict; verify conflict banner appears above entity cards in the Extracted data panel with icon, title, description, severity badge, and "Resolve conflict →" button (AC-003)
- [ ] Verify `<SeverityBadge severity="High">` renders visible text "High severity" and an icon; verify no severity information is conveyed by colour alone (UXR-105; WCAG SC 1.4.1)
- [ ] Load SCR-014 for a patient with 0 conflicts; verify no conflict banners are rendered and the Extracted data panel shows only the entity list (Edge: no conflicts)
- [ ] Run axe accessibility audit on SCR-014 with a conflict; verify `role="alert"` on each banner, each "Resolve conflict →" button has a descriptive `aria-label`, and no colour-only failure (WCAG 2.1 AA; UXR-105)
- [ ] Verify multiple "Resolve conflict →" buttons on the same page each have distinct `aria-label` values (e.g., "Resolve conflict: Drug-allergy conflict" vs "Resolve conflict: Drug interaction") — screen readers must distinguish them (WCAG 2.1 AA SC 2.4.6)
- [ ] Verify TypeScript compilation succeeds with no `any` types in the conflict rendering path; `SeverityLevel` union prevents passing an unknown severity string to `<SeverityBadge>` (TypeScript safety)

---

## Implementation Checklist
- [ ] `conflicts: ConflictDto[]` in `PatientSummaryDto` is a required field (not optional `?`) — the backend always returns an array, so `undefined` cannot reach the `.filter()` call in `PatientViewPage`; if needed add `?? []` as a defensive fallback (Edge: no conflicts; type safety)
- [ ] The `openConflicts.map(...)` call is placed **above** the `<AiLabel>` disclaimer and entity cards in the render output — matching the wireframe where the conflict banner appears at the top of the Extracted data panel before the entity list (AC-003; wireframe fidelity)
- [ ] `<ConflictBanner>` uses `role="alert"` with `aria-live="assertive"` — this combination causes screen readers to announce the conflict immediately when the component mounts; multiple banners each have their own `role="alert"` div so each is announced independently (AC-003; WCAG 2.1 AA SC 4.1.3)
- [ ] `<SeverityBadge>` renders the severity icon with `aria-hidden="true"` and the text label as visible DOM text (not `title`, `alt`, or `aria-label`) — the label must be readable by both sighted users and screen readers; CSS `color` alone is not sufficient under UXR-105 (WCAG 2.1 AA SC 1.4.1)
- [ ] The "Resolve conflict →" stub button does not navigate or perform a network request — it calls `console.warn(...)` only; the button is fully accessible (keyboard-focusable, ARIA-labelled) so that when MOD-005 is implemented, only the `onClick` handler needs to change (wireframe `resolve-conflict-btn`; WCAG 2.1 AA SC 2.1.1 — keyboard accessible placeholder)
- [ ] `formatConflictType(conflictType: string)` converts the backend enum values to human-readable labels: `DrugInteraction → "Drug interaction detected"`, `DrugAllergyConflict → "Drug-allergy conflict detected"`, `DuplicateDiagnosis → "Duplicate diagnosis detected"`; unknown values fall through to the raw string so future conflict types are still displayable without a frontend deploy (WCAG 2.1 AA SC 3.1.1 — comprehensible labels)
