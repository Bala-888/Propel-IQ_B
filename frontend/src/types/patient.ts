/**
 * Patient domain types for SCR-013 (patient search) and SCR-014 (360° patient view).
 * us_040 — task_002; us_041 — task_002
 *
 * `getConfidenceLabel` is the single source of truth for mapping a raw confidence
 * float → display label; `ConfidenceLevel` is the exhaustive union used by ConfidenceChip.
 */

// ── Confidence ────────────────────────────────────────────────────────────────────────────────────

export type ConfidenceLevel = 'High' | 'Medium' | 'Low – unverified'

/**
 * Maps a raw confidence float to a human-readable level (UXR-105; AC-003).
 * High ≥ 0.8 · Medium ≥ 0.5 · Low – unverified < 0.5
 */
export function getConfidenceLabel(confidence: number): ConfidenceLevel {
  if (confidence >= 0.8) return 'High'
  if (confidence >= 0.5) return 'Medium'
  return 'Low – unverified'
}

// ── Search DTO ────────────────────────────────────────────────────────────────────────────────────

/** Shape returned by `GET /api/patients/search?q=<term>` (AC-001). */
export interface PatientSearchResultDto {
  id:          string
  fullName:    string
  dateOfBirth: string   // ISO-8601 "yyyy-MM-dd"
  patientCode: string   // e.g. "PAT-00001"
}

// ── Summary DTOs ──────────────────────────────────────────────────────────────────────────────────

/** Demographic and intake data for a patient (AC-002; SCR-014 Demographics + Intake tabs). */
export interface DemographicsDto {
  id:          string
  firstName:   string
  lastName:    string
  dateOfBirth: string   // ISO-8601 "yyyy-MM-dd"
  email:       string
  phone:       string
  insurance:   string
  patientCode: string
  intake: {
    smoking:        string
    alcohol:        string
    exercise:       string
    chiefComplaint: string
    submittedAt:    string   // ISO-8601
  } | null
}

/** A single AI-extracted clinical entity (AC-003; UXR-105; UXR-402). */
export interface EntityDto {
  type:         string
  value:        string
  confidence:   number
  lowConfidence: boolean
  source?:      string
  extractedAt?: string
  code?:        string
}

/** Compact booking row shown on the patient summary (AC-002). */
export interface BookingSummaryDto {
  bookingId: number
  slotDate:  string
  slotTime:  string
  status:    string
}

/** Compact document row shown on the Documents tab (AC-002; Edge: pagination). */
export interface DocumentSummaryDto {
  documentId: string
  fileName:   string
  uploadedAt: string   // ISO-8601
  status:     string
}

/**
 * Full payload returned by `GET /api/patients/{id}/summary` (AC-002).
 * Only `documents`, `totalDocumentCount`, `currentPage`, and `totalPages` change
 * when paginating — the other fields are stable and must not be re-requested (Edge: 100+ docs).
 */
export interface PatientSummaryDto {
  demographics:       DemographicsDto
  entities:           EntityDto[]
  activeBookings:     BookingSummaryDto[]
  documents:          DocumentSummaryDto[]
  totalDocumentCount: number
  currentPage:        number
  totalPages:         number
  /** Open clinical conflicts detected by the AI pipeline (us_041/AC-003). Always an array — never null. */
  conflicts:          ConflictDto[]
}

// ── Conflict DTOs (us_041/AC-003; UXR-105) ────────────────────────────────────────────────────────

/** Severity union — prevents unknown severity strings reaching render logic (UXR-105; TypeScript safety). */
export type SeverityLevel = 'Low' | 'Medium' | 'High'

/** Compact entity reference embedded in a conflict pair. */
export interface EntitySummaryDto {
  id:    string
  type:  string
  value: string
}

/** A clinical conflict detected by the AI pipeline (us_041/AC-003). */
export interface ConflictDto {
  id:           string
  entityA:      EntitySummaryDto
  entityB:      EntitySummaryDto
  conflictType: string
  description:  string
  severity:     SeverityLevel
  status:       'Open' | 'Resolved'
}
