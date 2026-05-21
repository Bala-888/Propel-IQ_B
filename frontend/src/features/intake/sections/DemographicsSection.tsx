import { useFormContext } from 'react-hook-form'
import type { ManualIntakeFormValues } from '../manualIntakeSchema'
import { FieldRow, ErrorMessage, inputStyle } from './shared'

/**
 * Demographics section — Fields: FirstName, LastName, DateOfBirth (required + format-validated),
 * Gender (required), Phone (optional), Address (optional).
 * Registered into the parent `<FormProvider>` via `useFormContext` (AC-001; AC-003).
 * Error messages use `role="alert"` with icon + text — never colour-only (UXR-105; WCAG 1.4.1).
 */
export function DemographicsSection() {
  const {
    register,
    formState: { errors },
  } = useFormContext<ManualIntakeFormValues>()

  const e = errors.demographics

  return (
    <div>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 'var(--space-4)' }}>
        <FieldRow label="First Name" required>
          <input
            {...register('demographics.firstName')}
            placeholder="Jane"
            style={inputStyle(!!e?.firstName)}
          />
          {e?.firstName && <ErrorMessage message={e.firstName.message ?? 'Required'} />}
        </FieldRow>

        <FieldRow label="Last Name" required>
          <input
            {...register('demographics.lastName')}
            placeholder="Doe"
            style={inputStyle(!!e?.lastName)}
          />
          {e?.lastName && <ErrorMessage message={e.lastName.message ?? 'Required'} />}
        </FieldRow>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 'var(--space-4)', marginTop: 'var(--space-4)' }}>
        <FieldRow label="Date of Birth" required hint="YYYY-MM-DD or MM/DD/YYYY">
          <input
            {...register('demographics.dateOfBirth')}
            placeholder="YYYY-MM-DD"
            style={inputStyle(!!e?.dateOfBirth)}
          />
          {e?.dateOfBirth && <ErrorMessage message={e.dateOfBirth.message ?? 'Required'} />}
        </FieldRow>

        <FieldRow label="Gender" required>
          <select {...register('demographics.gender')} style={inputStyle(!!e?.gender)}>
            <option value="">Select…</option>
            <option value="Male">Male</option>
            <option value="Female">Female</option>
            <option value="Non-binary">Non-binary</option>
            <option value="Prefer not to say">Prefer not to say</option>
            <option value="Other">Other</option>
          </select>
          {e?.gender && <ErrorMessage message={e.gender.message ?? 'Required'} />}
        </FieldRow>
      </div>

      <div style={{ marginTop: 'var(--space-4)' }}>
        <FieldRow label="Phone Number">
          <input
            {...register('demographics.phone')}
            placeholder="e.g. 555-123-4567"
            style={inputStyle(false)}
          />
        </FieldRow>
      </div>

      <div style={{ marginTop: 'var(--space-4)' }}>
        <FieldRow label="Address">
          <textarea
            {...register('demographics.address')}
            placeholder="Street, City, State, ZIP"
            rows={2}
            style={{ ...inputStyle(false), resize: 'vertical' }}
          />
        </FieldRow>
      </div>
    </div>
  )
}
