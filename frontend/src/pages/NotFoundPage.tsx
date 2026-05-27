import { Link } from 'react-router-dom'

export function NotFoundPage() {
  return (
    <main style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', minHeight: '100vh', gap: '1rem', padding: '2rem', textAlign: 'center' }}>
      <h1>404 – Page Not Found</h1>
      <p>The page you requested could not be found.</p>
      <Link to="/login">Return to Login</Link>
    </main>
  )
}
