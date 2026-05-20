/**
 * Displays the active/inactive state of a user account.
 * UXR-105: status is conveyed with both an icon AND a text label — not colour alone
 * (accessibility requirement: colour-blind users must not miss status changes).
 */

interface UserStatusBadgeProps {
  isActive: boolean
}

function CheckCircleIcon() {
  return (
    <svg
      aria-hidden="true"
      focusable="false"
      width="12"
      height="12"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2.5"
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      <path d="M22 11.08V12a10 10 0 1 1-5.93-9.14" />
      <polyline points="22 4 12 14.01 9 11.01" />
    </svg>
  )
}

function XCircleIcon() {
  return (
    <svg
      aria-hidden="true"
      focusable="false"
      width="12"
      height="12"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2.5"
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      <circle cx="12" cy="12" r="10" />
      <line x1="15" y1="9" x2="9" y2="15" />
      <line x1="9" y1="9" x2="15" y2="15" />
    </svg>
  )
}

export function UserStatusBadge({ isActive }: UserStatusBadgeProps) {
  return (
    <span
      className={`user-status-badge ${isActive ? 'user-status-badge--active' : 'user-status-badge--inactive'}`}
    >
      {isActive ? <CheckCircleIcon /> : <XCircleIcon />}
      <span>{isActive ? 'Active' : 'Inactive'}</span>
    </span>
  )
}
