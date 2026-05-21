import type { PaginationMeta } from '../../api/slotsApi'

// ── Icons ─────────────────────────────────────────────────────────────────────────────────────────

function ChevronLeftIcon() {
  return (
    <svg aria-hidden="true" width="14" height="14" viewBox="0 0 24 24" fill="none"
         stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"
         style={{ flexShrink: 0 }}>
      <polyline points="15 18 9 12 15 6" />
    </svg>
  )
}

function ChevronRightIcon() {
  return (
    <svg aria-hidden="true" width="14" height="14" viewBox="0 0 24 24" fill="none"
         stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"
         style={{ flexShrink: 0 }}>
      <polyline points="9 18 15 12 9 6" />
    </svg>
  )
}

// ── SlotPagination ────────────────────────────────────────────────────────────────────────────────

export interface SlotPaginationProps {
  pagination:   PaginationMeta
  onPageChange: (newPage: number) => void
  isLoading?:   boolean
}

/**
 * Previous / Next pagination controls for the slot calendar (SCR-006; us_019; AC-002).
 *
 * - "Previous" is `disabled` when `page === 1`; "Next" is `disabled` when at the last page.
 * - Each button includes a chevron icon alongside text — disabled state is never
 *   communicated by colour change alone (WCAG 1.4.1; WCAG 4.1.2; UXR-105).
 * - Minimum 44×44 px touch target on each button (WCAG 2.5.5).
 * - Current page indicator "Page N of M" is rendered between the two buttons.
 */
export function SlotPagination({ pagination, onPageChange, isLoading = false }: SlotPaginationProps) {
  const { page, totalPages } = pagination
  const atFirst = page <= 1
  const atLast  = page >= totalPages || totalPages === 0

  const btnBase: React.CSSProperties = {
    display:      'inline-flex',
    alignItems:   'center',
    gap:          'var(--space-1)',
    minHeight:    44,
    minWidth:     44,
    padding:      '0 var(--space-4)',
    fontFamily:   'var(--font-sans)',
    fontSize:     '13px',
    fontWeight:   600,
    borderRadius: 'var(--radius-sm)',
    border:       '1px solid var(--color-border)',
    background:   'var(--color-bg-surface)',
    cursor:       'pointer',
    userSelect:   'none',
  }

  return (
    <nav
      aria-label="Slot calendar pagination"
      style={{
        display:        'flex',
        alignItems:     'center',
        justifyContent: 'center',
        gap:            'var(--space-5)',
        paddingTop:     'var(--space-6)',
      }}
    >
      <button
        type="button"
        onClick={() => onPageChange(page - 1)}
        disabled={atFirst || isLoading}
        aria-disabled={atFirst || isLoading}
        aria-label="Previous page"
        style={{
          ...btnBase,
          color:   (atFirst || isLoading) ? 'var(--color-text-disabled)' : 'var(--color-text-primary)',
          opacity: (atFirst || isLoading) ? 0.5 : 1,
          cursor:  (atFirst || isLoading) ? 'default' : 'pointer',
        }}
      >
        {/* Icon + text — disabled state not communicated by colour alone (UXR-105; WCAG 1.4.1) */}
        <ChevronLeftIcon />
        <span>Previous</span>
      </button>

      <span
        aria-live="polite"
        aria-atomic="true"
        style={{ fontSize: '13px', color: 'var(--color-text-secondary)', fontWeight: 500 }}
      >
        Page {page} of {totalPages === 0 ? 1 : totalPages}
      </span>

      <button
        type="button"
        onClick={() => onPageChange(page + 1)}
        disabled={atLast || isLoading}
        aria-disabled={atLast || isLoading}
        aria-label="Next page"
        style={{
          ...btnBase,
          color:   (atLast || isLoading) ? 'var(--color-text-disabled)' : 'var(--color-text-primary)',
          opacity: (atLast || isLoading) ? 0.5 : 1,
          cursor:  (atLast || isLoading) ? 'default' : 'pointer',
        }}
      >
        <span>Next</span>
        <ChevronRightIcon />
      </button>
    </nav>
  )
}
