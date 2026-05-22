/**
 * AiLabel — reusable badge indicating AI-extracted content (UXR-402; us_040/task_002).
 *
 * Renders the literal text "✦ AI extracted" as visible text (not tooltip/title) so it is
 * perceivable by screen readers (WCAG 2.1 AA SC 1.3.1). The badge is never aria-hidden.
 * Styles use CSS custom properties `--color-ai-accent` and `--color-ai-bg` from index.css.
 */

const labelStyle: React.CSSProperties = {
  display:        'inline-flex',
  alignItems:     'center',
  gap:            '4px',
  fontSize:       '11px',
  fontWeight:     600,
  color:          'var(--color-ai-accent)',
  background:     'var(--color-ai-bg)',
  padding:        '2px 8px',
  borderRadius:   'var(--radius-full)',
  whiteSpace:     'nowrap',
}

export function AiLabel() {
  return (
    <span style={labelStyle}>
      ✦ AI extracted
    </span>
  )
}
