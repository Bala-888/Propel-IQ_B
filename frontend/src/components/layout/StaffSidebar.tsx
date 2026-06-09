import { NavLink, useNavigate } from 'react-router-dom'
import { useAuth } from '../../context/AuthContext'

// ── Nav items per spec (information-architecture.md §3; wireframes SCR-011–015) ─────────────────
const STAFF_NAV = [
  { label: 'Queue',          to: '/queue'           },
  { label: 'Walk-in',        to: '/walkin/new'      },
  { label: 'Patient search', to: '/patients/search' },
  { label: 'Code review',    to: '/patients/search', hint: 'code-review' },
] as const

// ── Styles matching wireframe design tokens ────────────────────────────────────────────────────
const S = {
  sidebar: {
    width: '240px',
    minHeight: '100vh',
    background: 'var(--color-bg-surface, #FFFFFF)',
    borderRight: '1px solid var(--color-border, #E2E8F0)',
    display: 'flex',
    flexDirection: 'column' as const,
    flexShrink: 0,
  },
  logo: {
    padding: '24px',
    fontSize: '18px',
    fontWeight: 700,
    color: 'var(--color-primary, #1A56DB)',
    borderBottom: '1px solid var(--color-border, #E2E8F0)',
  },
  nav: {
    flex: 1,
    padding: '16px 12px',
    display: 'flex',
    flexDirection: 'column' as const,
    gap: '4px',
  },
  footer: {
    padding: '16px 12px',
    borderTop: '1px solid var(--color-border, #E2E8F0)',
  },
  userInfo: {
    display: 'flex',
    alignItems: 'center',
    gap: '12px',
    padding: '8px 12px',
  },
  avatar: {
    width: '32px',
    height: '32px',
    borderRadius: '50%',
    background: '#EBF3FE',
    color: '#1A56DB',
    fontSize: '12px',
    fontWeight: 700,
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    flexShrink: 0,
  },
  signOutBtn: {
    width: '100%',
    marginTop: '8px',
    padding: '8px 12px',
    borderRadius: '6px',
    border: '1px solid var(--color-border, #E2E8F0)',
    background: 'transparent',
    color: 'var(--color-text-secondary, #475569)',
    fontSize: '13px',
    fontWeight: 500,
    cursor: 'pointer',
    textAlign: 'left' as const,
  },
} as const

function getInitials(name: string): string {
  return name.split(' ').map(w => w[0] ?? '').join('').slice(0, 2).toUpperCase()
}

interface StaffSidebarProps {
  activeHint?: string
}

export function StaffSidebar({ activeHint }: StaffSidebarProps) {
  const { setAuth, patientName, role } = useAuth()
  const navigate = useNavigate()

  const displayName = patientName ?? role ?? 'Staff'

  function handleSignOut() {
    setAuth(null, null)
    sessionStorage.removeItem('refreshToken')
    sessionStorage.removeItem('redirectAfterLogin')
    navigate('/login', { replace: true })
  }

  return (
    <aside style={S.sidebar} role="navigation" aria-label="Staff navigation">
      <div style={S.logo}>UPACIP</div>

      <nav style={S.nav}>
        {STAFF_NAV.map(item => (
          <NavLink
            key={item.label}
            to={item.to}
            style={({ isActive }) => ({
              display: 'flex',
              alignItems: 'center',
              padding: '10px 12px',
              borderRadius: '6px',
              fontSize: '14px',
              fontWeight: 500,
              textDecoration: 'none',
              minHeight: '44px',
              // active hint for Code review (same path as Patient search)
              ...(activeHint && item.hint === activeHint
                ? { background: 'var(--color-primary-subtle, #EBF3FE)', color: 'var(--color-primary, #1A56DB)' }
                : isActive && !item.hint
                  ? { background: 'var(--color-primary-subtle, #EBF3FE)', color: 'var(--color-primary, #1A56DB)' }
                  : { color: 'var(--color-text-secondary, #475569)' }),
            })}
            aria-current={
              activeHint && item.hint === activeHint
                ? 'page'
                : undefined
            }
          >
            {item.label}
          </NavLink>
        ))}
      </nav>

      <div style={S.footer}>
        <div style={S.userInfo}>
          <div style={S.avatar} aria-hidden="true">{getInitials(displayName)}</div>
          <div>
            <div style={{ fontSize: '13px', fontWeight: 500, color: 'var(--color-text-primary, #0F172A)' }}>
              {displayName}
            </div>
            <div style={{ fontSize: '12px', color: 'var(--color-text-secondary, #475569)' }}>
              {role}
            </div>
          </div>
        </div>
        <button style={S.signOutBtn} onClick={handleSignOut} aria-label="Sign out">
          Sign out
        </button>
      </div>
    </aside>
  )
}
