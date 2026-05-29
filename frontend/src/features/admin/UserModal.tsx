/**
 * MOD-006 — Create / Edit User modal.
 * Used by UserManagementPage for both creating new users and editing role/status of existing ones.
 *
 * Create mode: all fields editable; submits POST /admin/users.
 * Edit mode:   name + email are pre-populated and read-only (backend doesn't support editing them
 *              via PATCH); role field is editable and submits PATCH /admin/users/{id}.
 *
 * AC-001: new user form; AC-002: role change; UXR-202: focus trap; UXR-601: inline validation.
 */
import { useEffect, useRef, useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { createUser, patchUser, DuplicateEmailError, type AdminUser, type CreateUserBody } from '../../api/adminUsersApi'
import './UserModal.css'

const ROLES = ['Patient', 'Staff', 'Admin'] as const

const createSchema = z.object({
  name: z.string().min(1, 'Name is required'),
  email: z.string().email('A valid email address is required'),
  role: z.enum(ROLES),
  password: z.string().min(8, 'Password must be at least 8 characters'),
  confirmPassword: z.string(),
}).refine(d => d.password === d.confirmPassword, {
  message: 'Passwords do not match',
  path: ['confirmPassword'],
})

const editSchema = z.object({
  name: z.string(),
  email: z.string(),
  role: z.enum(ROLES),
})

type CreateFormValues = z.infer<typeof createSchema>
type EditFormValues = z.infer<typeof editSchema>

interface UserModalProps {
  mode: 'create' | 'edit'
  user?: AdminUser          // pre-populated in edit mode
  accessToken: string
  onSuccess: (user: AdminUser) => void
  onClose: () => void
}

export function UserModal({ mode, user, accessToken, onSuccess, onClose }: UserModalProps) {
  const isCreate = mode === 'create'
  const [formError, setFormError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const overlayRef = useRef<HTMLDivElement>(null)
  const firstFocusRef = useRef<HTMLElement | null>(null)

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<CreateFormValues | EditFormValues>({
    resolver: zodResolver(isCreate ? createSchema : editSchema),
    defaultValues: isCreate
      ? { name: '', email: '', role: 'Staff', password: '', confirmPassword: '' }
      : { name: user?.name ?? '', email: user?.email ?? '', role: (user?.role ?? 'Staff') as typeof ROLES[number] },
  })

  // Focus the first focusable element when modal opens
  useEffect(() => {
    const el = overlayRef.current?.querySelector<HTMLElement>(
      'input:not([disabled]), select:not([disabled]), button:not([disabled])',
    )
    el?.focus()
    firstFocusRef.current = el ?? null
  }, [])

  // Close on Escape; trap Tab within modal (UXR-202)
  useEffect(() => {
    function onKeyDown(e: KeyboardEvent) {
      if (e.key === 'Escape') { onClose(); return }
      if (e.key !== 'Tab' || !overlayRef.current) return

      const focusable = overlayRef.current.querySelectorAll<HTMLElement>(
        'button:not([disabled]), input:not([disabled]), select:not([disabled]), [tabindex]:not([tabindex="-1"])',
      )
      const first = focusable[0]
      const last = focusable[focusable.length - 1]

      if (e.shiftKey) {
        if (document.activeElement === first) { e.preventDefault(); last?.focus() }
      } else {
        if (document.activeElement === last) { e.preventDefault(); first?.focus() }
      }
    }
    document.addEventListener('keydown', onKeyDown)
    return () => document.removeEventListener('keydown', onKeyDown)
  }, [onClose])

  async function onSubmit(values: CreateFormValues | EditFormValues) {
    setFormError(null)
    setSubmitting(true)

    try {
      if (isCreate) {
        const v = values as CreateFormValues
        const body: CreateUserBody = { name: v.name, email: v.email, role: v.role, password: v.password }
        const { userId } = await createUser(accessToken, body)
        const newUser: AdminUser = {
          id: userId,
          name: v.name,
          email: v.email,
          role: v.role,
          isActive: true,
          createdAt: new Date().toISOString(),
        }
        onSuccess(newUser)
      } else {
        if (!user) return
        const v = values as EditFormValues
        await patchUser(accessToken, user.id, { role: v.role })
        onSuccess({ ...user, role: v.role })
      }
    } catch (err) {
      if (err instanceof DuplicateEmailError) {
        setFormError('A user with this email already exists')
      } else {
        setFormError('Something went wrong. Please try again.')
      }
    } finally {
      setSubmitting(false)
    }
  }

  const titleText = isCreate ? 'New user' : `Edit user \u2014 ${user?.name ?? ''}`

  return (
    <div
      className="um-modal-overlay"
      role="dialog"
      aria-modal="true"
      aria-labelledby="user-modal-title"
      ref={overlayRef}
      onClick={(e) => { if (e.target === e.currentTarget) onClose() }}
    >
      <div className="um-modal">
        <div className="um-modal__header">
          <h2 className="um-modal__title" id="user-modal-title">{titleText}</h2>
          <button
            type="button"
            className="um-modal__close"
            aria-label="Cancel and close"
            onClick={onClose}
          >
            ×
          </button>
        </div>

        {/* eslint-disable-next-line @typescript-eslint/no-misused-promises */}
        <form noValidate onSubmit={handleSubmit(onSubmit)}>
          <div className="um-modal__field">
            <label className="um-modal__label" htmlFor="um-name">Full name</label>
            <input
              id="um-name"
              type="text"
              className={`um-modal__input${errors.name ? ' um-modal__input--error' : ''}`}
              disabled={!isCreate}
              aria-describedby={errors.name ? 'um-name-err' : undefined}
              autoComplete="name"
              {...register('name')}
            />
            {errors.name && (
              <span id="um-name-err" className="um-modal__field-error" role="alert">
                {errors.name.message}
              </span>
            )}
          </div>

          <div className="um-modal__field">
            <label className="um-modal__label" htmlFor="um-email">Email address</label>
            <input
              id="um-email"
              type="email"
              className={`um-modal__input${errors.email ? ' um-modal__input--error' : ''}`}
              disabled={!isCreate}
              aria-describedby={errors.email ? 'um-email-err' : 'um-email-hint'}
              autoComplete="email"
              {...register('email')}
            />
            {isCreate && (
              <span id="um-email-hint" className="um-modal__hint">Used for login. Must be unique.</span>
            )}
            {errors.email && (
              <span id="um-email-err" className="um-modal__field-error" role="alert">
                {errors.email.message}
              </span>
            )}
          </div>

          {isCreate && (
            <div className="um-modal__field">
              <label className="um-modal__label" htmlFor="um-password">Password</label>
              <input
                id="um-password"
                type="password"
                className={`um-modal__input${'password' in errors && errors.password ? ' um-modal__input--error' : ''}`}
                aria-describedby={'password' in errors && errors.password ? 'um-password-err' : 'um-password-hint'}
                autoComplete="new-password"
                {...register('password')}
              />
              <span id="um-password-hint" className="um-modal__hint">Minimum 8 characters.</span>
              {'password' in errors && errors.password && (
                <span id="um-password-err" className="um-modal__field-error" role="alert">
                  {errors.password.message as string}
                </span>
              )}
            </div>
          )}

          {isCreate && (
            <div className="um-modal__field">
              <label className="um-modal__label" htmlFor="um-confirm-password">Confirm password</label>
              <input
                id="um-confirm-password"
                type="password"
                className={`um-modal__input${'confirmPassword' in errors && errors.confirmPassword ? ' um-modal__input--error' : ''}`}
                aria-describedby={'confirmPassword' in errors && errors.confirmPassword ? 'um-confirm-password-err' : undefined}
                autoComplete="new-password"
                {...register('confirmPassword')}
              />
              {'confirmPassword' in errors && errors.confirmPassword && (
                <span id="um-confirm-password-err" className="um-modal__field-error" role="alert">
                  {errors.confirmPassword.message as string}
                </span>
              )}
            </div>
          )}

          <div className="um-modal__field">
            <label className="um-modal__label" htmlFor="um-role">Role</label>
            <select
              id="um-role"
              className="um-modal__select"
              aria-label="User role"
              {...register('role')}
            >
              {ROLES.map((r) => (
                <option key={r} value={r}>{r}</option>
              ))}
            </select>
            {errors.role && (
              <span className="um-modal__field-error" role="alert">
                {errors.role.message}
              </span>
            )}
          </div>

          {formError && (
            <div id="um-form-error" className="um-modal__form-error" role="alert">
              {formError}
            </div>
          )}

          <div className="um-modal__actions">
            <button type="button" className="um-btn um-btn--secondary" onClick={onClose}>
              Cancel
            </button>
            <button type="submit" className="um-btn um-btn--primary" disabled={submitting}>
              {submitting ? 'Saving…' : 'Save user'}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}
