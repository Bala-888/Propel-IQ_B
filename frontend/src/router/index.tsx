import { createBrowserRouter } from 'react-router-dom'
import { ProtectedRoute } from '../components/ProtectedRoute'
import { LoginPage } from '../pages/LoginPage'
import { IntakePage } from '../pages/IntakePage'
import { QueuePage } from '../pages/QueuePage'
import { AdminPage } from '../pages/AdminPage'

export const router = createBrowserRouter([
  {
    path: '/login',
    element: <LoginPage />,
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
])
