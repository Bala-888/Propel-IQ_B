import { Link } from 'react-router-dom'
import { Header } from '../components/layout/Header'

const cards = [
  {
    to: '/admin/users',
    title: 'User Management',
    description: 'Create, edit, activate and deactivate staff and patient accounts.',
    icon: '👤',
  },
  {
    to: '/admin/kpi',
    title: 'KPI Dashboard',
    description: 'Monitor appointment volumes, queue wait times, and clinical metrics.',
    icon: '📊',
  },
  {
    to: '/queue',
    title: 'Queue',
    description: 'View and manage the live patient queue.',
    icon: '🗂️',
  },
  {
    to: '/walkin/new',
    title: 'Walk-in Booking',
    description: 'Register a new walk-in patient appointment.',
    icon: '🚶',
  },
]

export function AdminPage() {
  return (
    <>
      <Header />
      <main style={{ padding: '2rem', maxWidth: '960px', margin: '0 auto' }}>
        <h1 style={{ fontSize: '1.75rem', fontWeight: 700, marginBottom: '0.5rem' }}>
          Admin Dashboard
        </h1>
        <p style={{ color: 'var(--color-text-secondary)', marginBottom: '2rem' }}>
          Select a section to manage.
        </p>
        <div
          style={{
            display: 'grid',
            gridTemplateColumns: 'repeat(auto-fill, minmax(220px, 1fr))',
            gap: '1rem',
          }}
        >
          {cards.map((card) => (
            <Link
              key={card.to}
              to={card.to}
              style={{
                display: 'flex',
                flexDirection: 'column',
                gap: '0.5rem',
                padding: '1.25rem',
                border: '1px solid var(--color-border)',
                borderRadius: '8px',
                background: 'var(--color-bg-surface)',
                textDecoration: 'none',
                color: 'inherit',
                transition: 'box-shadow 0.15s',
              }}
              onMouseEnter={(e) =>
                ((e.currentTarget as HTMLAnchorElement).style.boxShadow =
                  '0 2px 8px rgba(0,0,0,0.12)')
              }
              onMouseLeave={(e) =>
                ((e.currentTarget as HTMLAnchorElement).style.boxShadow = 'none')
              }
            >
              <span style={{ fontSize: '2rem' }}>{card.icon}</span>
              <strong style={{ fontSize: '1rem' }}>{card.title}</strong>
              <span style={{ fontSize: '0.875rem', color: 'var(--color-text-secondary)' }}>
                {card.description}
              </span>
            </Link>
          ))}
        </div>
      </main>
    </>
  )
}
