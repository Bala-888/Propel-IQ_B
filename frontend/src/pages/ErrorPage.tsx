import { Link, useRouteError, isRouteErrorResponse } from 'react-router-dom'

export function ErrorPage() {
  const error = useRouteError()

  let heading = 'Unexpected Error'
  let message = 'Something went wrong. Please try again.'

  if (isRouteErrorResponse(error)) {
    if (error.status === 404) {
      heading = '404 – Page Not Found'
      message = 'The page you are looking for does not exist.'
    } else {
      heading = `${error.status} – ${error.statusText}`
      message = error.data ?? 'An unexpected error occurred.'
    }
  }

  return (
    <main style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', minHeight: '100vh', gap: '1rem', padding: '2rem', textAlign: 'center' }}>
      <h1>{heading}</h1>
      <p>{message}</p>
      <Link to="/login">Return to Login</Link>
    </main>
  )
}
