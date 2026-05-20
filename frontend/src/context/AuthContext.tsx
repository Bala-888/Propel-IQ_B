import { createContext, useContext, useState, type ReactNode } from 'react'
import { decodeJwtSub } from '../utils/jwt'

interface AuthContextValue {
  token: string | null
  setToken: (t: string | null) => void
  /** Patient's full name stored after successful registration (AC-005). */
  patientName: string | null
  setPatientName: (name: string | null) => void
  /** JWT access token stored in memory — never in localStorage (OWASP A02; task_003/AC-002..AC-004). */
  accessToken: string | null
  /** Decoded role from the JWT access token. */
  role: string | null
  /** Decoded user ID (`sub` claim) from the JWT access token. Used for AC-004 own-account guard. */
  userId: string | null
  /** Stores accessToken + role in memory on successful login; call with nulls on logout. */
  setAuth: (accessToken: string | null, role: string | null) => void
}

export const AuthContext = createContext<AuthContextValue | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [token, setToken] = useState<string | null>(null)
  const [patientName, setPatientName] = useState<string | null>(null)
  const [accessToken, setAccessToken] = useState<string | null>(null)
  const [role, setRole] = useState<string | null>(null)
  const [userId, setUserId] = useState<string | null>(null)

  function setAuth(at: string | null, r: string | null) {
    setAccessToken(at)
    setRole(r)
    setUserId(at ? decodeJwtSub(at) : null)
  }

  return (
    <AuthContext.Provider value={{ token, setToken, patientName, setPatientName, accessToken, role, userId, setAuth }}>
      {children}
    </AuthContext.Provider>
  )
}

/** Typed accessor — throws if called outside <AuthProvider> */
export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used within <AuthProvider>')
  return ctx
}
