import { createBrowserRouter } from 'react-router-dom'
import { ProtectedRoute } from '../components/ProtectedRoute'
import { LoginForm } from '../features/auth/LoginForm'
import { IntakePage } from '../pages/IntakePage'
import { QueuePage } from '../pages/QueuePage'
import { AdminPage } from '../pages/AdminPage'
import { RegistrationForm } from '../features/registration/RegistrationForm'
import { UserManagementPage } from '../features/admin/UserManagementPage'
import { WalkInBookingForm } from '../features/walkin/WalkInBookingForm'

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
])
