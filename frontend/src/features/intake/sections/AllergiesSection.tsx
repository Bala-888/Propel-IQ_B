import { useFormContext, useFieldArray } from 'react-hook-form'
import type { ManualIntakeFormValues } from '../manualIntakeSchema'
import { FieldRow, ErrorMessage, inputStyle, AddButton, RemoveButton } from './shared'

/**
 * Allergies section — dynamic list of allergy items (Allergen + Reaction).
 * Allergen name is required per item; Reaction is optional.
 * Error messages use `role="alert"` with icon + text (UXR-105; WCAG 4.1.3).
 */
export function AllergiesSection() {
  const {
    register,
    control,
    formState: { errors },
  } = useFormContext<ManualIntakeFormValues>()

  const { fields, append, remove } = useFieldArray({ control, name: 'allergies.items' })

  const e = errors.allergies

  return (
    <div>
      {fields.map((field, i) => (
        <div
          key={field.id}
          style={{
            display:             'grid',
            gridTemplateColumns: '1fr 1fr 32px',
            gap:                 'var(--space-3)',
            alignItems:          'start',
            marginBottom:        'var(--space-3)',
            padding:             'var(--space-4)',
            background:          'var(--color-bg-subtle)',
            borderRadius:        'var(--radius-sm)',
            border:              '1px solid var(--color-border)',
          }}
        >
          <div>
            <FieldRow label="Allergen" required>
              <input
                {...register(`allergies.items.${i}.allergen`)}
                placeholder="e.g. Penicillin"
                style={inputStyle(!!e?.items?.[i]?.allergen)}
              />
              {e?.items?.[i]?.allergen && (
                <ErrorMessage message={e.items[i]!.allergen!.message ?? 'Allergen name is required.'} />
              )}
            </FieldRow>
          </div>

          <div>
            <FieldRow label="Reaction">
              <input
                {...register(`allergies.items.${i}.reaction`)}
                placeholder="e.g. Hives, anaphylaxis"
                style={inputStyle(false)}
              />
            </FieldRow>
          </div>

          <div style={{ paddingTop: 18 }}>
            <RemoveButton onClick={() => remove(i)} />
          </div>
        </div>
      ))}

      <AddButton onClick={() => append({ allergen: '', reaction: '' })} label="Add Allergy" />
    </div>
  )
}
