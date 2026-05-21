// ── Types ─────────────────────────────────────────────────────────────────────────────────────────

export type RiskTier = 'High' | 'Medium' | 'Low' | 'Unknown'

interface RiskBadgeProps {
  tier: RiskTier
}

// ── Icons (aria-hidden — visible text label is the sole accessibility signal; UXR-403, UXR-105) ──

function WarningIcon() {
  return (
    <svg aria-hidden="true" width="12" height="12" viewBox="0 0 20 20" fill="none">
      <path d="M10 3L17.794 17H2.206L10 3Z" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
      <path d="M10 9v3" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
      <circle cx="10" cy="14" r="0.75" fill="currentColor" />
    </svg>
  )
}

function CautionIcon() {
  return (
    <svg aria-hidden="true" width="12" height="12" viewBox="0 0 20 20" fill="none">
      <circle cx="10" cy="10" r="8" stroke="currentColor" strokeWidth="1.5" />
      <path d="M10 7v4" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
      <circle cx="10" cy="13" r="0.75" fill="currentColor" />
    </svg>
  )
}

function CheckIcon() {
  return (
    <svg aria-hidden="true" width="12" height="12" viewBox="0 0 20 20" fill="none">
      <polyline points="4,10 8,14 16,6" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  )
}

function InfoIcon() {
  return (
    <svg aria-hidden="true" width="12" height="12" viewBox="0 0 20 20" fill="none">
      <circle cx="10" cy="10" r="8" stroke="currentColor" strokeWidth="1.5" />
      <path d="M10 9v5" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
      <circle cx="10" cy="6.5" r="0.75" fill="currentColor" />
    </svg>
  )
}

// ── Tier config (wireframe tokens; all four tiers explicit — "Unknown" is not a blank cell; UXR-105) ──

interface TierConfig {
  background: string
  color:      string
  icon:       JSX.Element
  label:      string
}

function resolveTierConfig(tier: RiskTier): TierConfig {
  switch (tier) {
    case 'High':
      return { background: '#FEF2F2', color: '#DC2626', icon: <WarningIcon />, label: 'High Risk' }
    case 'Medium':
      return { background: '#FEF9C3', color: '#A16207', icon: <CautionIcon />, label: 'Medium Risk' }
    case 'Low':
      return { background: '#DCFCE7', color: '#16A34A', icon: <CheckIcon />,   label: 'Low Risk' }
    case 'Unknown':
      return { background: '#F1F5F9', color: '#64748B', icon: <InfoIcon />,    label: 'Unknown' }
  }
}

// ── Component ─────────────────────────────────────────────────────────────────────────────────────

/**
 * Risk Tier badge for the queue dashboard (SCR-011).
 *
 * Renders a coloured pill with an SVG icon AND a visible text label for every tier value —
 * including "Unknown". Colour is supplementary; icon + text are the primary signals
 * (AC-002; UXR-403; UXR-105; WCAG 2.1 SC 1.4.1 — no colour-only encoding).
 *
 * @param tier  Risk tier string received from the API; always one of the four union members.
 */
export function RiskBadge({ tier }: RiskBadgeProps) {
  const { background, color, icon, label } = resolveTierConfig(tier)
  return (
    <span
      style={{
        display:       'inline-flex',
        alignItems:    'center',
        gap:           '4px',
        fontSize:      '12px',
        fontWeight:    600,
        padding:       '3px 8px',
        borderRadius:  '9999px',
        background,
        color,
      }}
      aria-label={`Risk: ${label}`}
    >
      {icon}
      {label}
    </span>
  )
}
