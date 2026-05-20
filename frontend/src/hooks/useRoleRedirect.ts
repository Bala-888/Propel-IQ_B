/** Role-to-path mapping (AC-004) */
const ROLE_PATHS: Record<string, string> = {
  Patient: '/intake',
  Staff: '/queue',
  Admin: '/admin',
}

/**
 * Returns the designated destination path for a given role.
 * Falls back to '/login' with a console.warn for unrecognised roles — never throws.
 */
export function useRoleRedirect(role: string): string {
  const path = ROLE_PATHS[role]
  if (path !== undefined) {
    return path
  }
  console.warn(`Unknown role: ${role}`)
  return '/login'
}
