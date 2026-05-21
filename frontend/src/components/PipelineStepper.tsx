/**
 * PipelineStepper — 5-step progress indicator for document processing (us_039; AC-001; SCR-010).
 *
 * Renders PIPELINE_STEPS as a horizontal row of step dots connected by lines.
 * Visual states:
 *   done    → green ✓ dot  (var(--color-status-success))
 *   active  → blue  dot + CSS spinner (var(--color-primary))
 *   pending → grey  … dot  (var(--color-border))
 *   error   → red   ✗ dot  (var(--color-status-error))
 *
 * ARIA: container has role="img" + aria-label="Processing pipeline"; each dot has
 * aria-label="{step.label} — {state}" for screen readers (UXR-206; WCAG 2.1 SC 1.3.3).
 * No color-only information — each state uses both color AND a symbol/shape (UXR-603).
 */

import { Fragment, type CSSProperties } from 'react'
import { PIPELINE_STEPS, getStepState, type DocumentStatus } from '../types/document'

// ── Props ─────────────────────────────────────────────────────────────────────────────────────────

interface PipelineStepperProps {
  status: DocumentStatus
}

// ── State → symbol map (UXR-603 — never color alone) ─────────────────────────────────────────────

const STATE_SYMBOL: Record<'done' | 'active' | 'pending' | 'error', string> = {
  done:    '✓',
  active:  '⟳',
  pending: '…',
  error:   '✗',
}

const STATE_BG: Record<'done' | 'active' | 'pending' | 'error', string> = {
  done:    'var(--color-status-success)',
  active:  'var(--color-primary)',
  pending: 'var(--color-border)',
  error:   'var(--color-status-error)',
}

// ── Styles ────────────────────────────────────────────────────────────────────────────────────────

const pipelineStyle: CSSProperties = {
  display:     'flex',
  alignItems:  'center',
  gap:         0,
  marginTop:   'var(--space-3)',
}

const stepStyle: CSSProperties = {
  display:        'flex',
  flexDirection:  'column',
  alignItems:     'center',
  flex:           1,
}

const dotBaseStyle = (state: 'done' | 'active' | 'pending' | 'error'): CSSProperties => ({
  width:          '24px',
  height:         '24px',
  borderRadius:   '50%',
  background:     STATE_BG[state],
  color:          'white',
  display:        'flex',
  alignItems:     'center',
  justifyContent: 'center',
  fontSize:       '11px',
  fontWeight:     700,
  position:       'relative',
  flexShrink:     0,
})

const spinnerRingStyle: CSSProperties = {
  position:        'absolute',
  inset:           0,
  borderRadius:    '50%',
  border:          '2px solid rgba(255,255,255,0.35)',
  borderTopColor:  'white',
  animation:       'spin 0.6s linear infinite',
}

const connectorStyle = (isDone: boolean): CSSProperties => ({
  height:     '2px',
  flex:       1,
  background: isDone ? 'var(--color-status-success)' : 'var(--color-border)',
  marginTop:  '-12px',  // vertically align with centre of the dot row above
  flexShrink: 0,
})

const labelStyle: CSSProperties = {
  fontSize:   '11px',
  color:      'var(--color-text-secondary)',
  marginTop:  '4px',
  textAlign:  'center',
  lineHeight: 1.2,
}

// ── Component ─────────────────────────────────────────────────────────────────────────────────────

export function PipelineStepper({ status }: PipelineStepperProps) {
  return (
    <>
      {/* @keyframes for active spinner — scoped to this render tree */}
      <style>{`@keyframes spin { to { transform: rotate(360deg); } }`}</style>

      <div
        role="img"
        aria-label="Processing pipeline"
        style={pipelineStyle}
      >
        {PIPELINE_STEPS.map((step, index) => {
          const state   = getStepState(status, index)
          const isLast  = index === PIPELINE_STEPS.length - 1
          // Connector after this step is green only when this step itself is done
          const connectorDone = state === 'done'

          return (
            <Fragment key={step.key}>
              <div style={stepStyle}>
                <div
                  style={dotBaseStyle(state)}
                  aria-label={`${step.label} — ${state}`}
                >
                  {state === 'active'
                    ? (
                      <>
                        <span aria-hidden="true" style={spinnerRingStyle} />
                        <span aria-hidden="true">{STATE_SYMBOL.active}</span>
                      </>
                    )
                    : <span aria-hidden="true">{STATE_SYMBOL[state]}</span>
                  }
                </div>
                <span style={labelStyle}>{step.label}</span>
              </div>

              {!isLast && (
                <div
                  style={connectorStyle(connectorDone)}
                  aria-hidden="true"
                />
              )}
            </Fragment>
          )
        })}
      </div>
    </>
  )
}
