/**
 * Shared types, constants, and helpers for the document processing pipeline (us_039; SCR-010).
 *
 * `PIPELINE_STEPS`, `STATUS_LABELS`, and `TERMINAL_FAIL_STATUSES` are the single source of
 * truth for all status comparisons — raw API status strings never reach the UI (WCAG 2.1
 * SC 3.1.1; checklist: type-safe status comparisons; exhaustive TypeScript union).
 */

// ── Status union ──────────────────────────────────────────────────────────────────────────────────

/**
 * All possible values of `document_records.status` from the API.
 * Exhaustive union — adding a new backend status without updating this type causes compile errors.
 */
export type DocumentStatus =
  | 'Uploaded'
  | 'TextExtracted'
  | 'Chunked'
  | 'EmbeddingsComplete'
  | 'EntitiesExtracted'
  | 'TimedOut'
  | 'ExtractionFailed'

// ── DTO ───────────────────────────────────────────────────────────────────────────────────────────

/** Response shape for `GET /api/documents` and `GET /api/documents/{id}/status`. */
export interface DocumentStatusDto {
  id:                  string          // UUID
  fileName:            string
  status:              DocumentStatus
  processingStartedAt: string          // ISO 8601 string
}

// ── Pipeline steps ────────────────────────────────────────────────────────────────────────────────

/**
 * Ordered 5-step pipeline for the progress stepper (AC-001).
 * Keys map to `DocumentStatus` values for in-progress states.
 */
export const PIPELINE_STEPS: ReadonlyArray<{ key: DocumentStatus; label: string }> = [
  { key: 'Uploaded',           label: 'Pending'    },
  { key: 'TextExtracted',      label: 'Extracting' },
  { key: 'Chunked',            label: 'Chunking'   },
  { key: 'EmbeddingsComplete', label: 'Embedding'  },
  { key: 'EntitiesExtracted',  label: 'Complete'   },
] as const

/** Terminal failure statuses — both require the Retry CTA (AC-003, AC-005). */
export const TERMINAL_FAIL_STATUSES: ReadonlyArray<DocumentStatus> = [
  'TimedOut',
  'ExtractionFailed',
] as const

/** Terminal statuses — polling stops when a document enters any of these (AC-001). */
export const TERMINAL_STATUSES: ReadonlyArray<DocumentStatus> = [
  'EntitiesExtracted',
  'TimedOut',
  'ExtractionFailed',
] as const

/**
 * Human-readable badge labels for every `DocumentStatus` value.
 * Never expose raw API status strings to the UI (WCAG 2.1 SC 3.1.1; checklist).
 */
export const STATUS_LABELS: Record<DocumentStatus, string> = {
  Uploaded:           'Pending',
  TextExtracted:      'Extracting',
  Chunked:            'Chunking',
  EmbeddingsComplete: 'Embedding',
  EntitiesExtracted:  'Complete',
  TimedOut:           'Timed Out',
  ExtractionFailed:   'Failed',
}

// ── Step index map ────────────────────────────────────────────────────────────────────────────────

/**
 * Zero-based step index for each non-terminal status.
 * Used by `getStepState` to locate the active step.
 */
const STEP_INDEX: Partial<Record<DocumentStatus, number>> = {
  Uploaded:           0,
  TextExtracted:      1,
  Chunked:            2,
  EmbeddingsComplete: 3,
  EntitiesExtracted:  4,
}

// ── getStepState ──────────────────────────────────────────────────────────────────────────────────

/**
 * Returns the visual state of a single pipeline step dot.
 *
 * @param status    Current `DocumentStatus` of the document.
 * @param stepIndex 0-based index into `PIPELINE_STEPS`.
 *
 * Rules:
 * - Terminal failure (`TimedOut` | `ExtractionFailed`): step 0 = done; step 1 = error; rest = pending.
 * - `EntitiesExtracted`: all steps done.
 * - In-progress: steps before current = done; current = active; after = pending.
 *
 * TypeScript exhaustiveness: the `never` assertion below will cause a compile error if a new
 * `DocumentStatus` value is added without updating this function (checklist).
 */
export function getStepState(
  status:    DocumentStatus,
  stepIndex: number,
): 'done' | 'active' | 'pending' | 'error' {
  // Terminal failure — error dot at step 1 (Extracting), step 0 done, rest pending (wireframe SCR-010)
  if ((TERMINAL_FAIL_STATUSES as ReadonlyArray<string>).includes(status)) {
    const errorAt = 1
    if (stepIndex < errorAt)  return 'done'
    if (stepIndex === errorAt) return 'error'
    return 'pending'
  }

  if (status === 'EntitiesExtracted') return 'done'

  const currentIndex = STEP_INDEX[status] ?? 0
  if (stepIndex < currentIndex)  return 'done'
  if (stepIndex === currentIndex) return 'active'
  return 'pending'

  // Compile-time exhaustiveness guard — TypeScript will error if DocumentStatus grows
  // without this function being updated.
  // eslint-disable-next-line no-unreachable
  const _exhaustive: never = status as never
  void _exhaustive
}
