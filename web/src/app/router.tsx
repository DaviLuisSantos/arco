import { createBrowserRouter, Navigate } from 'react-router'
import { AppLayout } from '../components/layout/AppLayout'
import { RequireAuth } from '../components/layout/RequireAuth'
import { CyclePage } from '../pages/CyclePage'
import { HabitDetailPage } from '../pages/HabitDetailPage'
import { HabitsPage } from '../pages/HabitsPage'
import { LoginPage } from '../pages/LoginPage'
import { ProfilePage } from '../pages/ProfilePage'
import { TodayPage } from '../pages/TodayPage'

export const router = createBrowserRouter([
  { path: '/login', element: <LoginPage /> },
  {
    element: <RequireAuth />,
    children: [
      {
        element: <AppLayout />,
        children: [
          { index: true, element: <TodayPage /> },
          { path: 'habits', element: <HabitsPage /> },
          { path: 'habits/:id', element: <HabitDetailPage /> },
          { path: 'cycle', element: <CyclePage /> },
          { path: 'profile', element: <ProfilePage /> },
          { path: '*', element: <Navigate to="/" replace /> },
        ],
      },
    ],
  },
])
