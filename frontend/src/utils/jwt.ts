/**
 * Decodes the `role` claim from a JWT access token without a third-party library.
 * Splits on `.`, base64url-decodes the payload segment with `atob`, and JSON.parses.
 * Read-only — does NOT validate the signature. Used only for client-side route decisions
 * after the server has already authenticated the request.
 * Decision[2026-05-20]: short-form "role" claim emitted by TokenService (not ClaimTypes.Role URL)
 * so this function can read payload.role directly (AC-002, AC-003, AC-004; bundle-size hygiene).
 */
export function decodeJwtRole(token: string): string {
  // JWT structure: <header>.<payload>.<signature>  (RFC 7519 §3.1)
  const segments = token.split('.')
  if (segments.length !== 3) return ''

  // base64url → base64: replace URL-safe chars; pad to multiple of 4
  const b64 = segments[1].replace(/-/g, '+').replace(/_/g, '/')
  const padded = b64 + '='.repeat((4 - (b64.length % 4)) % 4)

  const payload = JSON.parse(atob(padded)) as Record<string, unknown>
  return typeof payload['role'] === 'string' ? payload['role'] : ''
}

/**
 * Decodes the `sub` claim from a JWT access token without a third-party library.
 * The `sub` claim is the user's integer ID serialised as a string by TokenService.
 * Read-only — does NOT validate the signature. Used only for AC-004 own-account guard
 * in UserManagementPage (us_011/task_002).
 */
export function decodeJwtSub(token: string): string | null {
  const segments = token.split('.')
  if (segments.length !== 3) return null
  try {
    const b64 = segments[1].replace(/-/g, '+').replace(/_/g, '/')
    const padded = b64 + '='.repeat((4 - (b64.length % 4)) % 4)
    const payload = JSON.parse(atob(padded)) as Record<string, unknown>
    const sub = payload['sub']
    if (sub == null) return null
    return typeof sub === 'string' ? sub : String(sub)
  } catch {
    return null
  }
}

/**
 * Decodes the `exp` claim from a JWT access token and returns it in milliseconds.
 * Returns null if the token is malformed or has no exp claim.
 * Read-only — does NOT validate the signature.
 */
export function decodeJwtExp(token: string): number | null {
  const segments = token.split('.')
  if (segments.length !== 3) return null
  try {
    const b64 = segments[1].replace(/-/g, '+').replace(/_/g, '/')
    const padded = b64 + '='.repeat((4 - (b64.length % 4)) % 4)
    const payload = JSON.parse(atob(padded)) as Record<string, unknown>
    const exp = payload['exp']
    if (typeof exp !== 'number') return null
    return exp * 1000 // JWT exp is in seconds; convert to ms
  } catch {
    return null
  }
}
