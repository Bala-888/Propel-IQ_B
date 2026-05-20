import { useAuth } from '../../context/AuthContext'

/**
 * Application header — displays the UPACIP wordmark and, when `patientName`
 * is set in AuthContext, the patient's name in the welcome message (AC-005).
 * patientName is set synchronously by RegistrationForm before navigation so
 * the name is available on the first render of any page that includes <Header />.
 */
export function Header() {
  const { patientName } = useAuth()

  return (
    <header
      style={{
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        padding: 'var(--space-4) var(--space-8)',
        borderBottom: '1px solid var(--color-border)',
        background: 'var(--color-bg-surface)',
      }}
    >
      <span
        style={{
          fontSize: '18px',
          fontWeight: 700,
          color: 'var(--color-primary)',
        }}
      >
        UPACIP
      </span>

      {patientName && (
        <p
          style={{
            fontSize: '14px',
            color: 'var(--color-text-secondary)',
          }}
          aria-live="polite"
        >
          Welcome, <strong style={{ color: 'var(--color-text-primary)' }}>{patientName}</strong>
        </p>
      )}
    </header>
  )
}
