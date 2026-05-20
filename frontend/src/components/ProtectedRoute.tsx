import { useEffect, type ReactNode } from 'react'
import { useNavigate, useLocation } from 'react-router-dom'
import { useAuth } from '../context/AuthContext'

interface ProtectedRouteProps {
  children: ReactNode
}

/**
 * Redirects unauthenticated users to /login?redirect=<original-path>.
 * Returns null while navigating so no protected content flashes (AC-003).
 */
export function ProtectedRoute({ children }: ProtectedRouteProps) {
  const { token } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()

  useEffect(() => {
    if (!token) {
      navigate(
        `/login?redirect=${encodeURIComponent(location.pathname)}`,
        { replace: true },
      )
    }
  }, [token, navigate, location.pathname])

  if (!token) return null

  return <>{children}</>
}
