import type { CreateWalkInResponse } from '../../api/walkInApi'
import './walkin.css'

interface Props {
  booking: CreateWalkInResponse
  /** Called when staff clicks "Add another walk-in" */
  onAddAnother: () => void
}

function CheckCircleIcon() {
  return (
    <svg
      aria-hidden="true"
      width="24"
      height="24"
      viewBox="0 0 24 24"
      fill="none"
      xmlns="http://www.w3.org/2000/svg"
    >
      <circle cx="12" cy="12" r="9" stroke="currentColor" strokeWidth="1.75" />
      <path
        d="M8 12.5l2.5 2.5 5.5-5.5"
        stroke="currentColor"
        strokeWidth="1.75"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  )
}

function AlertTriangleIcon() {
  return (
    <svg
      aria-hidden="true"
      width="18"
      height="18"
      viewBox="0 0 20 20"
      fill="none"
      xmlns="http://www.w3.org/2000/svg"
      className="walkin-confirmation__email-warning-icon"
    >
      <path
        d="M10 3L17.794 17H2.206L10 3Z"
        stroke="currentColor"
        strokeWidth="1.5"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
      <path d="M10 9v3.5" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
      <circle cx="10" cy="14.5" r="0.75" fill="currentColor" />
    </svg>
  )
}

/**
 * WalkInConfirmation
 *
 * Shown after a walk-in booking is successfully created.
 *
 * When `booking.credentialsEmailFailed` is true, a persistent <div role="alert"> banner is
 * rendered instructing staff to deliver credentials manually.
 * The banner does NOT use setTimeout or any auto-dismiss mechanism (AC-003 Edge).
 */
export function WalkInConfirmation({ booking, onAddAnother }: Props) {
  return (
    <div className="walkin-confirmation">
      <div className="walkin-confirmation__card">
        <div className="walkin-confirmation__icon">
          <CheckCircleIcon />
        </div>

        <h1 className="walkin-confirmation__title">Walk-in booking created</h1>
        <p className="walkin-confirmation__subtitle">
          The patient has been added to the queue.
        </p>

        {/* Booking details */}
        <dl className="walkin-confirmation__details">
          <div className="walkin-confirmation__detail-row">
            <dt className="walkin-confirmation__detail-label">Booking ID</dt>
            <dd className="walkin-confirmation__detail-value">#{booking.bookingId}</dd>
          </div>
          {booking.userId != null && (
            <div className="walkin-confirmation__detail-row">
              <dt className="walkin-confirmation__detail-label">Patient account</dt>
              <dd className="walkin-confirmation__detail-value">Created (ID {booking.userId})</dd>
            </div>
          )}
        </dl>

        {/*
          Credentials-email-failed persistent warning banner (AC-003 Edge).
          Uses <div role="alert"> — rendered immediately in JSX, never auto-dismissed.
        */}
        {booking.credentialsEmailFailed && (
          <div
            role="alert"
            aria-live="assertive"
            className="walkin-confirmation__email-warning"
          >
            <AlertTriangleIcon />
            <p className="walkin-confirmation__email-warning-text">
              <strong>Credentials email could not be delivered.</strong> Please provide the
              patient's temporary password manually before they leave.
            </p>
          </div>
        )}

        <div className="walkin-confirmation__actions">
          <a href="/queue" className="btn btn-secondary">
            View queue
          </a>
          <button type="button" className="btn btn-primary" onClick={onAddAnother}>
            Add another walk-in
          </button>
        </div>
      </div>
    </div>
  )
}
