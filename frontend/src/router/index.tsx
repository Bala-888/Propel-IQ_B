import { createBrowserRouter } from 'react-router-dom'
import { ProtectedRoute } from '../components/ProtectedRoute'
import { LoginForm } from '../features/auth/LoginForm'
import { IntakePage } from '../pages/IntakePage'
import { QueuePage } from '../pages/QueuePage'
import { AdminPage } from '../pages/AdminPage'
import { RegistrationForm } from '../features/registration/RegistrationForm'
import { UserManagementPage } from '../features/admin/UserManagementPage'
import { WalkInBookingForm } from '../features/walkin/WalkInBookingForm'
import { AiIntakePage } from '../features/intake/AiIntakePage'
import { ManualIntakeForm } from '../features/intake/ManualIntakeForm'
import { IntakeConfirmationPage } from '../pages/IntakeConfirmationPage'
import { SlotCalendar } from '../features/slots/SlotCalendar'
import { BookingConfirmationPage } from '../pages/BookingConfirmationPage'
import { PatientSettingsPage } from '../pages/PatientSettingsPage'

export const router = createBrowserRouter([
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
])
