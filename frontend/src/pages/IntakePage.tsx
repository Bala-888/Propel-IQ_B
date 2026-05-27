import { Link } from 'react-router-dom'
import { Header } from '../components/layout/Header'

const cards = [
  {
    to: '/intake/ai',
    title: 'AI Intake',
    description: 'Complete your intake form using our AI-guided conversational assistant.',
    icon: '🤖',
  },
  {
    to: '/intake/manual',
    title: 'Manual Intake',
    description: 'Fill in your intake form manually at your own pace.',
    icon: '📋',
  },
  {
    to: '/slots',
    title: 'Book Appointment',
    description: 'Browse available slots and book your next appointment.',
    icon: '📅',
  },
  {
    to: '/documents/upload',
    title: 'Upload Documents',
    description: 'Securely upload medical records, referrals, or insurance documents.',
    icon: '📎',
  },
  {
    to: '/documents',
    title: 'My Documents',
    description: 'View the status of your submitted documents.',
    icon: '🗂️',
  },
  {
    to: '/settings',
    title: 'Settings',
    description: 'Manage your notification preferences and account details.',
    icon: '⚙️',
  },
]

export function IntakePage() {
  return (
    <>
      <Header />
      <main style={{ padding: '2rem', maxWidth: '960px', margin: '0 auto' }}>
        <h1 style={{ fontSize: '1.75rem', fontWeight: 700, marginBottom: '0.5rem' }}>
          Patient Portal
        </h1>
        <p style={{ color: 'var(--color-text-secondary)', marginBottom: '2rem' }}>
          Welcome back. What would you like to do today?
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
              <span style={{ fontWeight: 600, fontSize: '1rem' }}>{card.title}</span>
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
