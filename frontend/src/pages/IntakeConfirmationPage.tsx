import { Link } from 'react-router-dom'
import { Header } from '../components/layout/Header'

/**
 * SCR-005 confirmation screen — shown after `POST /intake/manual` returns 201 (AC-002).
 * Informs the patient their intake has been submitted and provides navigation back to the intake hub.
 */
export function IntakeConfirmationPage() {
  return (
    <>
      <Header />
      <main
        style={{
          display:        'flex',
          flexDirection:  'column',
          alignItems:     'center',
          justifyContent: 'center',
          minHeight:      'calc(100vh - 64px)',
          padding:        'var(--space-8)',
          textAlign:      'center',
        }}
      >
        <div
          style={{
            background:   'var(--color-bg-surface)',
            border:       '1px solid var(--color-border)',
            borderRadius: 'var(--radius-md)',
            boxShadow:    'var(--shadow-2)',
            padding:      'var(--space-10) var(--space-8)',
            maxWidth:     480,
            width:        '100%',
          }}
        >
          <div
            aria-hidden="true"
            style={{
              width:        48,
              height:       48,
              borderRadius: 'var(--radius-full)',
              background:   'var(--color-success-surface)',
              display:      'flex',
              alignItems:   'center',
              justifyContent: 'center',
              margin:       '0 auto var(--space-5)',
            }}
          >
            <CheckIcon />
          </div>

          <h1
            style={{
              fontSize:     '20px',
              fontWeight:   700,
              color:        'var(--color-text-primary)',
              marginBottom: 'var(--space-3)',
            }}
          >
            Intake Submitted
          </h1>

          <p
            style={{
              fontSize:     '14px',
              color:        'var(--color-text-secondary)',
              marginBottom: 'var(--space-6)',
              lineHeight:   1.6,
            }}
          >
            Your intake form has been saved. A member of our clinical team will review your information
            before your appointment.
          </p>

          <Link
            to="/intake"
            style={{
              display:      'inline-block',
              background:   'var(--color-primary)',
              color:        'var(--color-text-inverse)',
              fontFamily:   'var(--font-sans)',
              fontSize:     '14px',
              fontWeight:   600,
              padding:      'var(--space-3) var(--space-6)',
              borderRadius: 'var(--radius-sm)',
              textDecoration: 'none',
            }}
          >
            Return to Intake Hub
          </Link>
        </div>
      </main>
    </>
  )
}

function CheckIcon() {
  return (
    <svg
      aria-hidden="true"
      width="24"
      height="24"
      viewBox="0 0 24 24"
      fill="none"
      stroke="var(--color-status-success)"
      strokeWidth="2.5"
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      <polyline points="20 6 9 17 4 12" />
    </svg>
  )
}
