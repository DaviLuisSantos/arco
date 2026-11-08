import { useEffect } from 'react'
import { useIsRestoring } from '@tanstack/react-query'
import { Navigate, Outlet, useLocation } from 'react-router'
import { applyUserLocale } from '../../i18n'
import { useAuthStatus } from '../../lib/useAuthStatus'
import { ErrorState, Spinner } from '../ui/States'

/** Quando o app abriu: um `me` mais antigo veio do cache do aparelho, não do servidor. */
const APP_STARTED_AT = Date.now()

/** Decide entre app e login com base em GET /api/auth/me. */
export function RequireAuth() {
  const location = useLocation()
  const auth = useAuthStatus()
  // O `me` vem do cache do aparelho (abre offline); revalida ao abrir o app para refletir plano, onboarding etc.
  // Espera o cache do aparelho terminar de carregar; senão a resposta nova pode ser trocada pela antiga.
  const restoring = useIsRestoring()
  const { refetch } = auth
  useEffect(() => {
    if (!restoring) void refetch()
  }, [refetch, restoring])
  const meLocale = auth.data?.locale
  useEffect(() => {
    if (meLocale) applyUserLocale(meLocale)
  }, [meLocale])

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
  // Só manda para os primeiros passos com a sessão já revalidada (o cache do aparelho pode estar atrasado).
  if (!auth.data.onboarded && location.pathname !== '/welcome') {
    if (auth.dataUpdatedAt < APP_STARTED_AT) {
      return (
        <div className="grid min-h-dvh place-items-center">
          <Spinner />
        </div>
      )
    }
    return <Navigate to="/welcome" replace state={{ from: location.pathname + location.search }} />
  }
  return <Outlet />
}
