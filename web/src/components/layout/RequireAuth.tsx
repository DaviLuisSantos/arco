import { Navigate, Outlet, useLocation } from 'react-router'
import { useAuthStatus } from '../../lib/useAuthStatus'
import { ErrorState, Spinner } from '../ui/States'

/** Decide entre app e login com base em GET /api/auth/me. */
export function RequireAuth() {
  const location = useLocation()
  const auth = useAuthStatus()

  if (auth.isPending) {
    return (
      <div className="grid min-h-dvh place-items-center">
        <Spinner />
      </div>
    )
  }
  if (auth.isLoadingError) {
    return (
      <div className="grid min-h-dvh place-items-center px-4">
        <ErrorState error={auth.error} onRetry={() => void auth.refetch()} />
      </div>
    )
  }
  if (!auth.data) {
    // Na raiz, quem não tem sessão vê a landing (o servidor já faz isso; aqui cobre o shell vindo do service worker).
    if (location.pathname === '/') {
      window.location.replace('/landing/')
      return null
    }
    return <Navigate to="/login" replace state={{ from: location.pathname }} />
  }
  return <Outlet />
}
