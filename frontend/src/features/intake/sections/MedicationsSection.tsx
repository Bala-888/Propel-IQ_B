import { useFormContext, useFieldArray } from 'react-hook-form'
import type { ManualIntakeFormValues } from '../manualIntakeSchema'
import { FieldRow, AdvisoryMessage, inputStyle, AddButton, RemoveButton } from './shared'

/**
 * Medications section — dynamic list of medication items (Name, Dosage, Frequency).
 *
 * Advisory: when any item has a non-empty Name and an empty Dosage, a non-blocking
 * `<span role="status">` advisory "Consider adding dosage for clarity" is shown with icon + text.
 * This advisory does NOT set a `formState.errors` entry and does NOT prevent form submission
 * (Edge: brand-only medication; UXR-105; WCAG 1.4.1).
 */
export function MedicationsSection() {
  const {
    register,
    control,
    watch,
  } = useFormContext<ManualIntakeFormValues>()

  const { fields, append, remove } = useFieldArray({ control, name: 'medications.items' })

  // Watch the entire medications.items array to detect brand-only entries reactively
  const items = watch('medications.items')
  const hasBrandOnly = items.some(m => m.name.trim() !== '' && m.dosage.trim() === '')

  return (
    <div>
      {fields.map((field, i) => (
        <div
          key={field.id}
          style={{
            padding:             'var(--space-4)',
            background:          'var(--color-bg-subtle)',
            borderRadius:        'var(--radius-sm)',
            border:              '1px solid var(--color-border)',
            marginBottom:        'var(--space-3)',
          }}
        >
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr 32px', gap: 'var(--space-3)', alignItems: 'start' }}>
            <FieldRow label="Medication Name">
              <input
                {...register(`medications.items.${i}.name`)}
                placeholder="e.g. Metformin"
                style={inputStyle(false)}
              />
            </FieldRow>

            <FieldRow label="Dosage">
              <input
                {...register(`medications.items.${i}.dosage`)}
                placeholder="e.g. 500 mg"
                style={inputStyle(false)}
              />
            </FieldRow>

            <FieldRow label="Frequency">
              <input
                {...register(`medications.items.${i}.frequency`)}
                placeholder="e.g. Twice daily"
                style={inputStyle(false)}
              />
            </FieldRow>

            <div style={{ paddingTop: 18 }}>
              <RemoveButton onClick={() => remove(i)} />
            </div>
          </div>
        </div>
      ))}

      <AddButton onClick={() => append({ name: '', dosage: '', frequency: '' })} label="Add Medication" />

      {/* Non-blocking advisory — icon + text required; does not set formState.errors (UXR-105) */}
      {hasBrandOnly && (
        <div style={{ marginTop: 'var(--space-3)' }}>
          <AdvisoryMessage message="Consider adding dosage for clarity" />
        </div>
      )}
    </div>
  )
}
