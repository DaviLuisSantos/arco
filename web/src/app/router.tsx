import { CircleAlert } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { createBrowserRouter, Navigate, useRouteError } from 'react-router'
import { AppLayout } from '../components/layout/AppLayout'
import { RequireAuth } from '../components/layout/RequireAuth'
import { Button } from '../components/ui/Button'
import { Spinner } from '../components/ui/States'

// Chunk de rota que não existe mais (deploy novo com a aba antiga aberta).
function isChunkError(error: unknown) {
  const message = error instanceof Error ? error.message : String(error)
  return /dynamically imported module|importing a module script|failed to fetch|loading chunk|loading css chunk/i.test(message)
}

function RouteError() {
  const { t } = useTranslation('layout')
  const error = useRouteError()
  const stale = isChunkError(error)
  return (
    <div role="alert" className="mx-auto flex max-w-sm flex-col items-center gap-3 px-4 py-24 text-center">
      <CircleAlert className="size-8 text-danger" aria-hidden />
      <h1 className="text-lg font-bold">{stale ? t('routeError.staleTitle') : t('routeError.title')}</h1>
      <p className="text-sm text-muted">{stale ? t('routeError.staleBody') : t('routeError.body')}</p>
      <Button onClick={() => window.location.reload()}>{t('routeError.reload')}</Button>
    </div>
  )
}

export const router = createBrowserRouter([
  {
    errorElement: <RouteError />,
    HydrateFallback: Spinner,
    children: [
      { path: '/login', lazy: () => import('../pages/LoginPage').then((m) => ({ Component: m.LoginPage })) },
      { path: '/register', lazy: () => import('../pages/RegisterPage').then((m) => ({ Component: m.RegisterPage })) },
      {
        path: '/forgot-password',
        lazy: () => import('../pages/ForgotPasswordPage').then((m) => ({ Component: m.ForgotPasswordPage })),
      },
      {
        path: '/reset-password',
        lazy: () => import('../pages/ResetPasswordPage').then((m) => ({ Component: m.ResetPasswordPage })),
      },
      { path: '/verify-email', lazy: () => import('../pages/VerifyEmailPage').then((m) => ({ Component: m.VerifyEmailPage })) },
      { path: '/invite/:token', lazy: () => import('../pages/AcceptInvitePage').then((m) => ({ Component: m.AcceptInvitePage })) },
      {
        element: <RequireAuth />,
        children: [
          // Primeiros passos fora do layout do app (sem navegação).
          { path: 'welcome', lazy: () => import('../pages/OnboardingPage').then((m) => ({ Component: m.OnboardingPage })) },
          {
            element: <AppLayout />,
            children: [
              {
                // Erro de uma tela mantém a navegação do app visível.
                errorElement: <RouteError />,
                children: [
                  { index: true, lazy: () => import('../pages/TodayPage').then((m) => ({ Component: m.TodayPage })) },
                  { path: 'habits', lazy: () => import('../pages/HabitsPage').then((m) => ({ Component: m.HabitsPage })) },
                  {
                    path: 'habits/:id',
                    lazy: () => import('../pages/HabitDetailPage').then((m) => ({ Component: m.HabitDetailPage })),
                  },
                  { path: 'cycle', lazy: () => import('../pages/CyclePage').then((m) => ({ Component: m.CyclePage })) },
                  { path: 'progress', lazy: () => import('../pages/ProgressPage').then((m) => ({ Component: m.ProgressPage })) },
                  { path: 'settings', lazy: () => import('../pages/SettingsPage').then((m) => ({ Component: m.SettingsPage })) },
                  { path: 'admin', lazy: () => import('../pages/AdminPage').then((m) => ({ Component: m.AdminPage })) },
                  {
                    path: 'partners/:id',
                    lazy: () => import('../pages/PartnerSummaryPage').then((m) => ({ Component: m.PartnerSummaryPage })),
                  },
                  { path: 'profile', lazy: () => import('../pages/ProfilePage').then((m) => ({ Component: m.ProfilePage })) },
                  { path: 'review', lazy: () => import('../pages/ReviewPage').then((m) => ({ Component: m.ReviewPage })) },
                  { path: 'stats', lazy: () => import('../pages/StatsPage').then((m) => ({ Component: m.StatsPage })) },
                  { path: '*', element: <Navigate to="/" replace /> },
                ],
              },
            ],
          },
        ],
      },
    ],
  },
])
