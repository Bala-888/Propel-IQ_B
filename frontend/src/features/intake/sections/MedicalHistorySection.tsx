import { useFormContext, useFieldArray } from 'react-hook-form'
import type { ManualIntakeFormValues } from '../manualIntakeSchema'
import { FieldRow, ErrorMessage, inputStyle, AddButton, RemoveButton } from './shared'

/**
 * Medical History section — fields: LastPhysicalExam (optional date), dynamic Conditions list
 * (name + diagnosed date), and dynamic Surgeries list (name + date).
 *
 * Date fields validate against `YYYY-MM-DD` and `MM/DD/YYYY`; any other non-empty value renders
 * `<span role="alert">` with icon + text "Please enter a valid date" without clearing adjacent
 * fields (Edge: invalid date; UXR-105; WCAG 1.4.1; WCAG 4.1.3).
 */
export function MedicalHistorySection() {
  const {
    register,
    control,
    formState: { errors },
  } = useFormContext<ManualIntakeFormValues>()

  const {
    fields: conditionFields,
    append: appendCondition,
    remove: removeCondition,
  } = useFieldArray({ control, name: 'medicalHistory.conditions' })

  const {
    fields: surgeryFields,
    append: appendSurgery,
    remove: removeSurgery,
  } = useFieldArray({ control, name: 'medicalHistory.surgeries' })

  const e = errors.medicalHistory

  return (
    <div>
      {/* ── Last Physical Exam ─────────────────────────────────────────────── */}
      <FieldRow label="Last Physical Exam" hint="YYYY-MM-DD or MM/DD/YYYY">
        <input
          {...register('medicalHistory.lastPhysicalExam')}
          placeholder="YYYY-MM-DD"
          style={inputStyle(!!e?.lastPhysicalExam)}
        />
        {e?.lastPhysicalExam && (
          <ErrorMessage message={e.lastPhysicalExam.message ?? 'Please enter a valid date'} />
        )}
      </FieldRow>

      {/* ── Conditions list ────────────────────────────────────────────────── */}
      <div style={{ marginTop: 'var(--space-5)' }}>
        <span
          style={{
            display:       'block',
            fontSize:      '12px',
            fontWeight:    600,
            color:         'var(--color-text-secondary)',
            textTransform: 'uppercase',
            letterSpacing: '0.4px',
            marginBottom:  'var(--space-2)',
          }}
        >
          Conditions
        </span>

        {conditionFields.map((field, i) => (
          <div
            key={field.id}
            style={{
              display:       'grid',
              gridTemplateColumns: '1fr 160px 32px',
              gap:           'var(--space-3)',
              alignItems:    'start',
              marginBottom:  'var(--space-3)',
              padding:       'var(--space-3)',
              background:    'var(--color-bg-subtle)',
              borderRadius:  'var(--radius-sm)',
              border:        '1px solid var(--color-border)',
            }}
          >
            <div>
              <input
                {...register(`medicalHistory.conditions.${i}.name`)}
                placeholder="Condition name"
                style={inputStyle(!!e?.conditions?.[i]?.name)}
              />
              {e?.conditions?.[i]?.name && (
                <ErrorMessage message={e.conditions[i]!.name!.message ?? 'Required'} />
              )}
            </div>
            <div>
              <input
                {...register(`medicalHistory.conditions.${i}.diagnosedDate`)}
                placeholder="YYYY-MM-DD"
                style={inputStyle(!!e?.conditions?.[i]?.diagnosedDate)}
              />
              {e?.conditions?.[i]?.diagnosedDate && (
                <ErrorMessage message={e.conditions[i]!.diagnosedDate!.message ?? 'Please enter a valid date'} />
              )}
            </div>
            <RemoveButton onClick={() => removeCondition(i)} />
          </div>
        ))}

        <AddButton onClick={() => appendCondition({ name: '', diagnosedDate: '' })} label="Add Condition" />
      </div>

      {/* ── Surgeries list ─────────────────────────────────────────────────── */}
      <div style={{ marginTop: 'var(--space-5)' }}>
        <span
          style={{
            display:       'block',
            fontSize:      '12px',
            fontWeight:    600,
            color:         'var(--color-text-secondary)',
            textTransform: 'uppercase',
            letterSpacing: '0.4px',
            marginBottom:  'var(--space-2)',
          }}
        >
          Surgeries
        </span>

        {surgeryFields.map((field, i) => (
          <div
            key={field.id}
            style={{
              display:             'grid',
              gridTemplateColumns: '1fr 160px 32px',
              gap:                 'var(--space-3)',
              alignItems:          'start',
              marginBottom:        'var(--space-3)',
              padding:             'var(--space-3)',
              background:          'var(--color-bg-subtle)',
              borderRadius:        'var(--radius-sm)',
              border:              '1px solid var(--color-border)',
            }}
          >
            <div>
              <input
                {...register(`medicalHistory.surgeries.${i}.name`)}
                placeholder="Surgery name"
                style={inputStyle(!!e?.surgeries?.[i]?.name)}
              />
              {e?.surgeries?.[i]?.name && (
                <ErrorMessage message={e.surgeries[i]!.name!.message ?? 'Required'} />
              )}
            </div>
            <div>
              <input
                {...register(`medicalHistory.surgeries.${i}.date`)}
                placeholder="YYYY-MM-DD"
                style={inputStyle(!!e?.surgeries?.[i]?.date)}
              />
              {e?.surgeries?.[i]?.date && (
                <ErrorMessage message={e.surgeries[i]!.date!.message ?? 'Please enter a valid date'} />
              )}
            </div>
            <RemoveButton onClick={() => removeSurgery(i)} />
          </div>
        ))}

        <AddButton onClick={() => appendSurgery({ name: '', date: '' })} label="Add Surgery" />
      </div>
    </div>
  )
}
