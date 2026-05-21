import { useFormContext } from 'react-hook-form'
import type { ManualIntakeFormValues } from '../manualIntakeSchema'
import { FieldRow, ErrorMessage, inputStyle } from './shared'

/**
 * Chief Complaint section — a single mandatory textarea (AC-003).
 * Submission is blocked when Description is empty; an `<ErrorMessage>` ("Chief complaint is
 * required") is shown with icon + text in this section, and the tab bar shows an `ErrorBadge`
 * on the "Chief Complaint" tab label — no data in other sections is cleared (AC-003; UXR-105;
 * WCAG 1.4.1; WCAG 4.1.3).
 */
export function ChiefComplaintSection() {
  const {
    register,
    formState: { errors },
  } = useFormContext<ManualIntakeFormValues>()

  const e = errors.chiefComplaint

  return (
    <div>
      <FieldRow label="Chief Complaint" required>
        <textarea
          {...register('chiefComplaint.description')}
          placeholder="Describe your main reason for this visit…"
          rows={5}
          aria-describedby={e?.description ? 'chief-complaint-error' : undefined}
          style={{
            ...inputStyle(!!e?.description),
            resize: 'vertical',
            lineHeight: 1.5,
          }}
        />
        {e?.description && (
          <span id="chief-complaint-error">
            <ErrorMessage message={e.description.message ?? 'Chief complaint is required'} />
          </span>
        )}
      </FieldRow>

      <p
        style={{
          fontSize:   '12px',
          color:      'var(--color-text-disabled)',
          marginTop:  'var(--space-2)',
        }}
      >
        Please describe your primary symptom or concern in as much detail as possible.
      </p>
    </div>
  )
}
