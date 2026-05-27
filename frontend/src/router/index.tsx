import { createBrowserRouter, Navigate } from 'react-router-dom'
import { ProtectedRoute } from '../components/ProtectedRoute'
import { ErrorPage } from '../pages/ErrorPage'
import { NotFoundPage } from '../pages/NotFoundPage'
import { LoginForm } from '../features/auth/LoginForm'
import { IntakePage } from '../pages/IntakePage'
import { QueuePage } from '../pages/QueuePage'
import { AdminPage } from '../pages/AdminPage'
import { AdminKpiDashboardPage } from '../pages/AdminKpiDashboardPage'
import { RegistrationForm } from '../features/registration/RegistrationForm'
import { UserManagementPage } from '../features/admin/UserManagementPage'
import { WalkInBookingForm } from '../features/walkin/WalkInBookingForm'
import { WalkinBookingPage } from '../pages/WalkinBookingPage'
import { AiIntakePage } from '../features/intake/AiIntakePage'
import { ManualIntakeForm } from '../features/intake/ManualIntakeForm'
import { IntakeConfirmationPage } from '../pages/IntakeConfirmationPage'
import { SlotCalendar } from '../features/slots/SlotCalendar'
import { BookingConfirmationPage } from '../pages/BookingConfirmationPage'
import { PatientSettingsPage } from '../pages/PatientSettingsPage'
import { DocumentUploadPage }   from '../pages/DocumentUploadPage'
import { DocumentStatusPage }   from '../pages/DocumentStatusPage'
import { PatientSearchPage }    from '../pages/PatientSearchPage'
import { PatientViewPage }      from '../pages/PatientViewPage'
import { MedicalCodePage }      from '../pages/MedicalCodePage'

export const router = createBrowserRouter([
  {
    path: '/',
    errorElement: <ErrorPage />,
    children: [
      { index: true, element: <Navigate to="/login" replace /> },
  {
    path: '/login',
    element: <LoginForm />,
  },
  // Public registration route — no ProtectedRoute wrapper (AC-004, AC-005)
  {
    path: '/register',
    element: <RegistrationForm />,
  },
  {
    path: '/intake',
    element: <ProtectedRoute><IntakePage /></ProtectedRoute>,
  },
  {
    path: '/queue',
    element: <ProtectedRoute><QueuePage /></ProtectedRoute>,
  },
  {
    path: '/admin',
    element: <ProtectedRoute><AdminPage /></ProtectedRoute>,
  },
  // SCR-016 — Admin KPI dashboard (us_034/task_002)
  {
    path: '/admin/kpi',
    element: <ProtectedRoute><AdminKpiDashboardPage /></ProtectedRoute>,
  },
  // SCR-017 — User management screen (us_011/task_002)
  {
    path: '/admin/users',
    element: <ProtectedRoute><UserManagementPage /></ProtectedRoute>,
  },
  // SCR-012 — Walk-in booking form (us_012/task_002)
  {
    path: '/walkin',
    element: <ProtectedRoute><WalkInBookingForm /></ProtectedRoute>,
  },
  // SCR-012 — New walk-in booking (us_030/task_002): Staff/Admin only; role guard inside page
  {
    path: '/walkin/new',
    element: <ProtectedRoute><WalkinBookingPage /></ProtectedRoute>,
  },
  // SCR-004 — AI conversational intake (us_016-I/task_002)
  {
    path: '/intake/ai',
    element: <ProtectedRoute><AiIntakePage /></ProtectedRoute>,
  },
  // SCR-005 — Manual intake form (us_017/task_002)
  {
    path: '/intake/manual',
    element: <ProtectedRoute><ManualIntakeForm /></ProtectedRoute>,
  },
  // SCR-005 confirmation screen — shown after POST /intake/manual succeeds (us_017/AC-002)
  {
    path: '/intake/confirmation',
    element: <ProtectedRoute><IntakeConfirmationPage /></ProtectedRoute>,
  },
  // SCR-006 — Appointment slot calendar (us_019/task_002)
  {
    path: '/slots',
    element: <ProtectedRoute><SlotCalendar /></ProtectedRoute>,
  },
  // Booking confirmation screen — shown after POST /bookings returns 201 (us_020/AC-001)
  {
    path: '/booking/confirmation',
    element: <ProtectedRoute><BookingConfirmationPage /></ProtectedRoute>,
  },
  // SCR-008 — Patient profile & settings: Notifications section (us_029/AC-001, AC-002)
  {
    path: '/settings',
    element: <ProtectedRoute><PatientSettingsPage /></ProtectedRoute>,
  },
  // SCR-009 — Document upload: Patient-only; role guard inside page (us_035/AC-001, AC-002, AC-004, AC-005)
  {
    path: '/documents/upload',
    element: <ProtectedRoute><DocumentUploadPage /></ProtectedRoute>,
  },
  // SCR-010 — Document processing status: Patient-only; polls GET /documents/{id}/status (us_039/AC-001)
  {
    path: '/documents',
    element: <ProtectedRoute><DocumentStatusPage /></ProtectedRoute>,
  },
  // SCR-013 — Patient search: Staff/Admin/Clinician only; role guard inside page (us_040/task_002; AC-001, AC-004)
  {
    path: '/patients/search',
    element: <ProtectedRoute><PatientSearchPage /></ProtectedRoute>,
  },
  // SCR-014 — 360° Patient view: Staff/Admin/Clinician only; role guard inside page (us_040/task_002; AC-002, AC-004)
  {
    path: '/patients/:id/view',
    element: <ProtectedRoute><PatientViewPage /></ProtectedRoute>,
  },
  // SCR-015 — Medical Code Review: Clinician/Admin only; role guard inside page (us_043/task_002; AC-001, AC-005)
  {
    path: '/patients/:id/codes',
    element: <ProtectedRoute><MedicalCodePage /></ProtectedRoute>,
  },
  { path: '*', element: <NotFoundPage /> },
  ],
  },
])
