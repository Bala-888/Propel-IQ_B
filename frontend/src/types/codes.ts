/**
 * Shared type definitions for the medical code review feature (us_043, us_044).
 * Single source of truth for ReviewStatus — import everywhere, never use inline string literals.
 * (DRY; TypeScript type-safety; task checklist item 6)
 */

/**
 * Valid states for a `code_suggestions` row (AC-001).
 * State machine: Pending → Accepted | Rejected | Corrected (no transition back to Pending).
 */
export type ReviewStatus = 'Pending' | 'Accepted' | 'Rejected' | 'Corrected'
