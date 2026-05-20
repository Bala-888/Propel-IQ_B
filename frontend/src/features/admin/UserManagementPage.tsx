/**
 * SCR-017 — Admin User Management screen.
 *
 * AC-001: admin can create a new user via the "New user" modal (MOD-006).
 * AC-002: admin can change a user's role via "Edit" → MOD-006.
 * AC-003: admin can deactivate a user via the "Deactivate" button + DeactivateDialog confirmation.
 * AC-003: admin can reactivate an inactive user via "Activate".
 * AC-004: admin cannot deactivate their own account — the Deactivate button is aria-disabled and
 *         clicking it shows an error banner rather than opening the dialog.
 * AC-005: duplicate email on create shows an inline error in the modal (handled by UserModal).
 * UXR-105: active/inactive status shown with icon + text, not colour alone.
 */
import { useEffect, useState } from 'react'
import { useAuth } from '../../context/AuthContext'
import { getUsers, patchUser, type AdminUser } from '../../api/adminUsersApi'
import { UserStatusBadge } from './UserStatusBadge'
import { UserModal } from './UserModal'
import { DeactivateDialog } from './DeactivateDialog'
import './UserManagementPage.css'

// Role badge helper — semantic class per role for distinct visual treatment (SCR-017)
function RoleBadge({ role }: { role: string }) {
  const cls = role.toLowerCase()
  return <span className={`um-role-badge um-role-badge--${cls}`}>{role}</span>
}

// Avatar — initials from name, colour seeded from id (SCR-017 wireframe)
function UserAvatar({ name, id }: { name: string; id: number }) {
  const initials = name
    .trim()
    .split(/\s+/)
    .map((w) => w[0]?.toUpperCase() ?? '')
    .slice(0, 2)
    .join('')
  const hue = (id * 67) % 360
  return (
    <span
      className="um-avatar"
      aria-hidden="true"
      style={{ background: `hsl(${hue}, 50%, 55%)` }}
    >
      {initials || '?'}
    </span>
  )
}

export function UserManagementPage() {
  const { accessToken, userId: currentUserId } = useAuth()

  const [users, setUsers] = useState<AdminUser[]>([])
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState<string | null>(null)

  // Search + filter state
  const [search, setSearch] = useState('')
  const [roleFilter, setRoleFilter] = useState('')
  const [statusFilter, setStatusFilter] = useState('')

  // Modal state
  const [modalOpen, setModalOpen] = useState(false)
  const [editingUser, setEditingUser] = useState<AdminUser | null>(null)

  // Deactivate dialog state
  const [dialogUser, setDialogUser] = useState<AdminUser | null>(null)
  const [dialogTargetState, setDialogTargetState] = useState(false)

  // AC-004: self-deactivation error banner
  const [selfDeactivateError, setSelfDeactivateError] = useState(false)

  useEffect(() => {
    if (!accessToken) return
    setLoading(true)
    getUsers(accessToken)
      .then((data) => {
        setUsers(data)
        setLoadError(null)
      })
      .catch(() => setLoadError('Failed to load users. Please refresh the page.'))
      .finally(() => setLoading(false))
  }, [accessToken])

  // Client-side filter
  const filteredUsers = users.filter((u) => {
    const matchesSearch =
      !search ||
      u.name.toLowerCase().includes(search.toLowerCase()) ||
      u.email.toLowerCase().includes(search.toLowerCase())
    const matchesRole = !roleFilter || u.role === roleFilter
    const matchesStatus =
      !statusFilter ||
      (statusFilter === 'active' ? u.isActive : !u.isActive)
    return matchesSearch && matchesRole && matchesStatus
  })

  function openCreateModal() {
    setEditingUser(null)
    setSelfDeactivateError(false)
    setModalOpen(true)
  }

  function openEditModal(user: AdminUser) {
    setEditingUser(user)
    setSelfDeactivateError(false)
    setModalOpen(true)
  }

  function openDeactivateDialog(user: AdminUser, targetState: boolean) {
    // AC-004: own-account guard — do not open dialog; show error banner instead
    if (!targetState && user.id.toString() === currentUserId) {
      setSelfDeactivateError(true)
      return
    }
    setSelfDeactivateError(false)
    setDialogUser(user)
    setDialogTargetState(targetState)
  }

  function handleModalSuccess(updatedUser: AdminUser) {
    setUsers((prev) => {
      const idx = prev.findIndex((u) => u.id === updatedUser.id)
      if (idx === -1) return [...prev, updatedUser]           // create: append
      const next = [...prev]
      next[idx] = updatedUser                                 // edit: replace in-place
      return next
    })
    setModalOpen(false)
    setEditingUser(null)
  }

  async function handleDialogConfirm() {
    if (!dialogUser || !accessToken) return
    await patchUser(accessToken, dialogUser.id, { isActive: dialogTargetState })
    // Optimistic update — no re-fetch needed
    setUsers((prev) =>
      prev.map((u) => (u.id === dialogUser.id ? { ...u, isActive: dialogTargetState } : u)),
    )
    setDialogUser(null)
  }

  return (
    <div className="um-page">
      {/* Minimal sidebar */}
      <nav className="um-sidebar" aria-label="Admin navigation">
        <div className="um-sidebar__brand">UPACIP</div>
        <ul className="um-sidebar__nav">
          <li>
            <a href="/admin/users" className="um-sidebar__link um-sidebar__link--active" aria-current="page">
              Users
            </a>
          </li>
        </ul>
      </nav>

      <main className="um-main">
        {/* Page header */}
        <div className="um-page__header">
          <div>
            <h1 className="um-page__title">User management</h1>
            <p className="um-page__subtitle">{users.length} user{users.length !== 1 ? 's' : ''} total</p>
          </div>
          <button type="button" className="um-btn-primary" onClick={openCreateModal}>
            <PlusIcon />
            New user
          </button>
        </div>

        {/* AC-004: self-deactivate error banner */}
        {selfDeactivateError && (
          <div className="um-alert um-alert--error" role="alert" aria-live="assertive">
            <strong>Cannot deactivate your own account.</strong>{' '}
            Contact another admin to make this change.
          </div>
        )}

        {/* Filter row */}
        <div className="um-filters">
          <input
            type="search"
            className="um-filter__search"
            placeholder="Search by name or email…"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            aria-label="Search users"
          />
          <select
            className="um-filter__select"
            value={roleFilter}
            onChange={(e) => setRoleFilter(e.target.value)}
            aria-label="Filter by role"
          >
            <option value="">All roles</option>
            <option value="Patient">Patient</option>
            <option value="Staff">Staff</option>
            <option value="Admin">Admin</option>
          </select>
          <select
            className="um-filter__select"
            value={statusFilter}
            onChange={(e) => setStatusFilter(e.target.value)}
            aria-label="Filter by status"
          >
            <option value="">All statuses</option>
            <option value="active">Active</option>
            <option value="inactive">Inactive</option>
          </select>
        </div>

        {/* Content area */}
        {loading && <p className="um-state-msg">Loading users…</p>}
        {loadError && (
          <div className="um-alert um-alert--error" role="alert">{loadError}</div>
        )}

        {!loading && !loadError && (
          <div className="um-table-wrapper">
            <table className="um-table" aria-label="User list">
              <thead>
                <tr>
                  <th scope="col">Name</th>
                  <th scope="col">Role</th>
                  <th scope="col">Status</th>
                  <th scope="col">
                    <span className="sr-only">Actions</span>
                  </th>
                </tr>
              </thead>
              <tbody>
                {filteredUsers.length === 0 ? (
                  <tr>
                    <td colSpan={4} className="um-table__empty">No users match the current filter.</td>
                  </tr>
                ) : (
                  filteredUsers.map((user) => {
                    const isSelf = user.id.toString() === currentUserId
                    return (
                      <tr key={user.id}>
                        <td>
                          <div className="um-name-cell">
                            <UserAvatar name={user.name} id={user.id} />
                            <div>
                              <div className="um-name-cell__name">{user.name || <em className="um-name-cell__unnamed">Unnamed user</em>}</div>
                              <div className="um-name-cell__email">{user.email}</div>
                            </div>
                          </div>
                        </td>
                        <td><RoleBadge role={user.role} /></td>
                        <td><UserStatusBadge isActive={user.isActive} /></td>
                        <td>
                          <div className="um-actions">
                            <button
                              type="button"
                              className="um-action-btn"
                              onClick={() => openEditModal(user)}
                            >
                              Edit
                            </button>
                            {user.isActive ? (
                              <button
                                type="button"
                                className={`um-action-btn um-action-btn--danger${isSelf ? ' um-action-btn--disabled' : ''}`}
                                onClick={() => openDeactivateDialog(user, false)}
                                aria-disabled={isSelf || undefined}
                                aria-label={isSelf ? 'Cannot deactivate your own account' : `Deactivate ${user.name}`}
                              >
                                Deactivate
                              </button>
                            ) : (
                              <button
                                type="button"
                                className="um-action-btn"
                                onClick={() => openDeactivateDialog(user, true)}
                                aria-label={`Activate ${user.name}`}
                              >
                                Activate
                              </button>
                            )}
                          </div>
                        </td>
                      </tr>
                    )
                  })
                )}
              </tbody>
            </table>
          </div>
        )}
      </main>

      {/* Create / Edit modal */}
      {modalOpen && accessToken && (
        <UserModal
          mode={editingUser ? 'edit' : 'create'}
          user={editingUser ?? undefined}
          accessToken={accessToken}
          onSuccess={handleModalSuccess}
          onClose={() => { setModalOpen(false); setEditingUser(null) }}
        />
      )}

      {/* Deactivate / Activate confirmation dialog */}
      {dialogUser && (
        <DeactivateDialog
          userName={dialogUser.name}
          targetState={dialogTargetState}
          /* eslint-disable-next-line @typescript-eslint/no-misused-promises */
          onConfirm={handleDialogConfirm}
          onCancel={() => setDialogUser(null)}
        />
      )}
    </div>
  )
}

function PlusIcon() {
  return (
    <svg aria-hidden="true" focusable="false" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
      <line x1="12" y1="5" x2="12" y2="19" />
      <line x1="5" y1="12" x2="19" y2="12" />
    </svg>
  )
}
