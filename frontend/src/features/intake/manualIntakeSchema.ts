import { z } from 'zod'

// ── Date format regex ─────────────────────────────────────────────────────────────────────────────
// Accepts `YYYY-MM-DD` or `MM/DD/YYYY` (Edge: invalid date; AC-003; UXR-105).
const DATE_RE = /^(\d{4}-\d{2}-\d{2}|\d{2}\/\d{2}\/\d{4})$/

/**
 * Optional date field: an empty string is allowed (not required); a non-empty string must match
 * one of the two accepted formats — any other value produces "Please enter a valid date"
 * without affecting adjacent fields (Edge: invalid date; UXR-105; WCAG 1.4.1).
 */
const optionalDate = z
  .string()
  .refine(v => v === '' || DATE_RE.test(v), 'Please enter a valid date')

// ── Section schemas ───────────────────────────────────────────────────────────────────────────────

/** Demographics — FirstName, LastName, DateOfBirth, and Gender are mandatory (AC-003). */
export const demographicsSchema = z.object({
  firstName:   z.string().min(1, 'First name is required'),
  lastName:    z.string().min(1, 'Last name is required'),
  dateOfBirth: z
    .string()
    .min(1, 'Date of birth is required')
    .refine(v => DATE_RE.test(v), 'Please enter a valid date'),
  gender:  z.string().min(1, 'Gender is required'),
  phone:   z.string(),
  address: z.string(),
})

/** Chief Complaint — Description is mandatory; failure blocks submit (AC-003). */
export const chiefComplaintSchema = z.object({
  description: z.string().min(1, 'Chief complaint is required'),
})

/** Medical History — all fields optional; date fields validated when non-empty (Edge: invalid date). */
export const medicalHistorySchema = z.object({
  conditions: z.array(
    z.object({
      name:          z.string().min(1, 'Condition name is required'),
      diagnosedDate: optionalDate,
    }),
  ),
  surgeries: z.array(
    z.object({
      name: z.string().min(1, 'Surgery name is required'),
      date: optionalDate,
    }),
  ),
  lastPhysicalExam: optionalDate,
})

/**
 * Medications — no required fields; a medication with a name but no dosage is a non-blocking
 * advisory, not a validation error (Edge: brand-only medication; AC-002).
 */
export const medicationsSchema = z.object({
  items: z.array(
    z.object({
      name:      z.string(),
      dosage:    z.string(),
      frequency: z.string(),
    }),
  ),
})

/** Allergies — allergen name required per item when the list is present. */
export const allergiesSchema = z.object({
  items: z.array(
    z.object({
      allergen: z.string().min(1, 'Allergen name is required'),
      reaction: z.string(),
    }),
  ),
})

// ── Combined full-form schema ─────────────────────────────────────────────────────────────────────

/**
 * Combined schema for the complete 5-section manual intake form.
 * Used with `zodResolver` in `ManualIntakeForm` to validate all sections before `POST /intake/manual`.
 * Section-level errors are surfaced per-section so the tab bar can show `ErrorBadge` indicators
 * without clearing valid sections (AC-003; WCAG 1.4.1).
 */
export const manualIntakeSchema = z.object({
  demographics:   demographicsSchema,
  chiefComplaint: chiefComplaintSchema,
  medicalHistory: medicalHistorySchema,
  medications:    medicationsSchema,
  allergies:      allergiesSchema,
})

export type ManualIntakeFormValues = z.infer<typeof manualIntakeSchema>

// ── Section definitions — used by ManualIntakeForm for tab rendering ──────────────────────────────

/** Ordered tab section metadata (UXR-302 — max 5 tabs). */
export const FORM_SECTIONS = [
  { label: 'Demographics',    key: 'demographics'   as const },
  { label: 'Medical History', key: 'medicalHistory' as const },
  { label: 'Medications',     key: 'medications'    as const },
  { label: 'Allergies',       key: 'allergies'      as const },
  { label: 'Chief Complaint', key: 'chiefComplaint' as const },
] as const

export type SectionKey = typeof FORM_SECTIONS[number]['key']

/** Default empty form values — used by `useForm` and after a successful submit reset. */
export const defaultFormValues: ManualIntakeFormValues = {
  demographics:   { firstName: '', lastName: '', dateOfBirth: '', gender: '', phone: '', address: '' },
  chiefComplaint: { description: '' },
  medicalHistory: { conditions: [], surgeries: [], lastPhysicalExam: '' },
  medications:    { items: [] },
  allergies:      { items: [] },
}
