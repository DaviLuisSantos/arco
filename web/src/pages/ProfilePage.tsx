import { useQuery, useQueryClient } from '@tanstack/react-query'
import { ChevronRight, LogOut, Settings, ShieldCheck } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate } from 'react-router'
import { authApi, playerApi } from '../api/endpoints'
import { Button } from '../components/ui/Button'
import { PageHeader } from '../components/ui/PageHeader'
import { useToast } from '../components/ui/toast-context'
import { PlanCard } from '../features/billing/PlanCard'
import { VacationCard } from '../features/pauses/VacationCard'
import { PartnersCard } from '../features/partners/PartnersCard'
import { PublicProfileCard } from '../features/publicProfile/PublicProfileCard'
import { PlayerProgress } from '../features/player/PlayerProgress'
import { errorMessage } from '../lib/errors'
import { queryKeys } from '../lib/queryKeys'
import { useAuthStatus } from '../lib/useAuthStatus'

export function ProfilePage() {
  const { t } = useTranslation('pages', { keyPrefix: 'profile' })
  const { t: ts } = useTranslation('settings')
  const isAdmin = useAuthStatus().data?.isAdmin ?? false
  const player = useQuery({ queryKey: queryKeys.player, queryFn: playerApi.get })

  return (
    <div className="flex flex-col gap-5">
      <PageHeader title={t('title')} />

      {player.data && (
        <Link
          to="/progress?tab=achievements"
          className="block rounded-2xl border border-border bg-surface p-3 transition hover:border-accent/60"
        >
          <PlayerProgress player={player.data} compact />
          <p className="mt-2 text-xs text-muted">{t('openProgress')}</p>
        </Link>
      )}

      <Link
        to="/settings"
        className="flex min-h-11 items-center gap-3 rounded-2xl border border-border bg-surface p-4 transition hover:border-accent/60"
      >
        <Settings className="size-5 text-accent" aria-hidden />
        <span className="flex-1 font-semibold">{ts('openSettings')}</span>
        <ChevronRight className="size-5 text-muted" aria-hidden />
      </Link>
      {isAdmin && (
        <Link
          to="/admin"
          className="flex min-h-11 items-center gap-3 rounded-2xl border border-border bg-surface p-4 transition hover:border-accent/60"
        >
          <ShieldCheck className="size-5 text-accent" aria-hidden />
          <span className="flex-1 font-semibold">{ts('openAdmin')}</span>
          <ChevronRight className="size-5 text-muted" aria-hidden />
        </Link>
      )}
      <PlanCard />
      <VacationCard />
      <PublicProfileCard />
      <PartnersCard />
      <LogoutButton />
    </div>
  )
}

function LogoutButton() {
  const { t } = useTranslation('pages', { keyPrefix: 'profile' })
  const qc = useQueryClient()
  const navigate = useNavigate()
  const toast = useToast()
  const [pending, setPending] = useState(false)

  async function logout() {
    setPending(true)
    try {
      await authApi.logout()
      qc.clear()
      qc.setQueryData(queryKeys.auth, null)
      void navigate('/login', { replace: true })
    } catch (e) {
      toast.error(errorMessage(e))
      setPending(false)
    }
  }

  return (
    <Button variant="danger" loading={pending} onClick={() => void logout()}>
      <LogOut className="size-4" aria-hidden />
      {t('logout')}
    </Button>
  )
}
