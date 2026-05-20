import { z } from 'zod'

// Decision[2026-05-20]: Wireframe has separate firstName/lastName fields; the API
// POST body requires a single `name` field. Schema uses firstName + lastName; the
// submit handler concatenates them. DOB future-date is validated server-side only
// (task edge-case note: "validated server-side and surfaced as server errors").
export const registrationSchema = z.object({
  firstName: z.string().min(1, 'First name is required'),
  lastName: z.string().min(1, 'Last name is required'),
  dateOfBirth: z
    .string()
    .min(1, 'A valid date of birth is required')
    .refine((val) => !isNaN(new Date(val).getTime()), 'A valid date of birth is required'),
  email: z.string().email('Enter a valid email address'),
  phone: z.string().min(1, 'Phone number is required'),
  // Insurance fields optional — omitting does not trigger client-side errors (Edge: insurance optional)
  insuranceProvider: z.string().optional(),
  insuranceId: z.string().optional(),
})

export type RegisterFormValues = z.infer<typeof registrationSchema>
