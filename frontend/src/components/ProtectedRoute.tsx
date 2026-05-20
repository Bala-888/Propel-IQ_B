import { useEffect, type ReactNode } from 'react'
import { useNavigate, useLocation } from 'react-router-dom'
import { useAuth } from '../context/AuthContext'

interface ProtectedRouteProps {
  children: ReactNode
}

/**
 * Redirects unauthenticated users to /login after storing the originally requested path
 * in sessionStorage under 'redirectAfterLogin'. LoginForm reads and clears this key on
 * successful login to restore the intended destination (AC-002).
 * Returns null while navigating so no protected content flashes.
 */
export function ProtectedRoute({ children }: ProtectedRouteProps) {
  const { accessToken } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()

  useEffect(() => {
    if (!accessToken) {
      // Store full path + search so LoginForm can restore it after successful auth (AC-002)
      sessionStorage.setItem('redirectAfterLogin', location.pathname + location.search)
      navigate('/login', { replace: true })
    }
  }, [accessToken, navigate, location.pathname, location.search])

  if (!accessToken) return null

  return <>{children}</>
}
