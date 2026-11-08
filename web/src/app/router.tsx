import { createBrowserRouter, Navigate } from 'react-router'
import { AppLayout } from '../components/layout/AppLayout'
import { RequireAuth } from '../components/layout/RequireAuth'
import { AcceptInvitePage } from '../pages/AcceptInvitePage'
import { AdminPage } from '../pages/AdminPage'
import { OnboardingPage } from '../pages/OnboardingPage'
import { PartnerSummaryPage } from '../pages/PartnerSummaryPage'
import { CyclePage } from '../pages/CyclePage'
import { HabitDetailPage } from '../pages/HabitDetailPage'
import { HabitsPage } from '../pages/HabitsPage'
import { ForgotPasswordPage } from '../pages/ForgotPasswordPage'
import { LoginPage } from '../pages/LoginPage'
import { ProfilePage } from '../pages/ProfilePage'
import { RegisterPage } from '../pages/RegisterPage'
import { ResetPasswordPage } from '../pages/ResetPasswordPage'
import { ReviewPage } from '../pages/ReviewPage'
import { StatsPage } from '../pages/StatsPage'
import { TodayPage } from '../pages/TodayPage'
import { VerifyEmailPage } from '../pages/VerifyEmailPage'

export const router = createBrowserRouter([
  { path: '/login', element: <LoginPage /> },
  { path: '/register', element: <RegisterPage /> },
  { path: '/forgot-password', element: <ForgotPasswordPage /> },
  { path: '/reset-password', element: <ResetPasswordPage /> },
  { path: '/verify-email', element: <VerifyEmailPage /> },
  { path: '/invite/:token', element: <AcceptInvitePage /> },
  {
    element: <RequireAuth />,
    children: [
      // Primeiros passos fora do layout do app (sem navegação).
      { path: 'welcome', element: <OnboardingPage /> },
      {
        element: <AppLayout />,
        children: [
          { index: true, element: <TodayPage /> },
          { path: 'habits', element: <HabitsPage /> },
          { path: 'habits/:id', element: <HabitDetailPage /> },
          { path: 'cycle', element: <CyclePage /> },
          { path: 'admin', element: <AdminPage /> },
          { path: 'partners/:id', element: <PartnerSummaryPage /> },
          { path: 'profile', element: <ProfilePage /> },
          { path: 'review', element: <ReviewPage /> },
          { path: 'stats', element: <StatsPage /> },
          { path: '*', element: <Navigate to="/" replace /> },
        ],
      },
    ],
  },
])
