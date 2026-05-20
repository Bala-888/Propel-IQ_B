import { useCallback, useState } from 'react'
import { RouterProvider } from 'react-router-dom'
import { AuthProvider, useAuth } from './context/AuthContext'
import { router } from './router'
import { useInactivityTimer } from './hooks/useInactivityTimer'
import { SessionTimeoutModal } from './features/session/SessionTimeoutModal'

const WARNING_AT_MS = 14 * 60 * 1000
const TIMEOUT_AT_MS = 15 * 60 * 1000

/**
 * Mounts the inactivity timer and renders the session timeout modal.
 * Only rendered when a JWT access token is in context — unauthenticated users
 * visiting /login or /register are not affected (us_010/AC-001).
 *
 * Uses router.navigate() instead of useNavigate() because this component lives
 * inside <AuthProvider> but outside <RouterProvider>; the imported router instance
 * exposes .navigate() directly without requiring the React Router context.
 */
function AppSessionManager() {
  const { setAuth } = useAuth()
  const [showModal, setShowModal] = useState(false)

  const handleTimeout = useCallback(() => {
    setAuth(null, null)
    sessionStorage.clear()
    void router.navigate('/login', {
      state: { message: 'Your session has expired. Please sign in again.' },
    })
  }, [setAuth])

  const { reset } = useInactivityTimer({
    warningAtMs: WARNING_AT_MS,
    timeoutAtMs: TIMEOUT_AT_MS,
    onWarning: () => setShowModal(true),
    onTimeout: handleTimeout,
    // Close the modal on this tab if another tab extended the session (Edge: multiple tabs).
    onExternalExtend: () => setShowModal(false),
  })

  return (
    <SessionTimeoutModal
      isOpen={showModal}
      onExtend={() => { setShowModal(false); reset() }}
      onLogout={handleTimeout}
    />
  )
}

/**
 * Gate: renders AppSessionManager only while an access token is present.
 * When accessToken is null the component tree is unmounted, stopping all
 * event listeners and clearing both inactivity timers (AC-001 — timer scope).
 */
function AppSessionGate() {
  const { accessToken } = useAuth()
  return accessToken ? <AppSessionManager /> : null
}

function App() {
  return (
    <AuthProvider>
      {/*
        AppSessionGate is inside AuthProvider (can call useAuth) but outside
        RouterProvider (router context unavailable). This is intentional:
        the session manager must read auth state and imperatively navigate.
      */}
      <AppSessionGate />
      <RouterProvider router={router} />
    </AuthProvider>
  )
}

export default App
