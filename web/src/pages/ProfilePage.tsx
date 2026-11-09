import { useQuery, useQueryClient } from '@tanstack/react-query'
import { ChevronRight, LogOut, Settings, ShieldCheck, Sparkles } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate } from 'react-router'
import { authApi, playerApi } from '../api/endpoints'
import type { XpEvent } from '../api/types'
import { Button } from '../components/ui/Button'
import { Card } from '../components/ui/Card'
import { PageHeader } from '../components/ui/PageHeader'
import { EmptyState, ErrorState, Spinner } from '../components/ui/States'
import { useToast } from '../components/ui/toast-context'
import { AchievementsSection } from '../features/achievements/AchievementsSection'
import { PlanCard } from '../features/billing/PlanCard'
import { PartnersCard } from '../features/partners/PartnersCard'
import { PublicProfileCard } from '../features/publicProfile/PublicProfileCard'
import { PlayerProgress } from '../features/player/PlayerProgress'
import { formatInstant } from '../lib/dates'
import { errorMessage } from '../lib/errors'
import { formatNumber, formatXp, XP_SOURCE_LABELS } from '../lib/format'
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

      {player.isPending ? (
        <Spinner />
      ) : player.isLoadingError ? (
        <ErrorState error={player.error} onRetry={() => void player.refetch()} />
      ) : (
        <>
          <Card className="flex flex-col gap-4">
            <PlayerProgress player={player.data} />
            <p className="text-sm text-muted">
              {t('total')} <span className="font-semibold text-text">{formatNumber(player.data.totalXp)} XP</span>
            </p>
          </Card>
        </>
      )}

      <AchievementsSection />
      {player.data && <RecentEvents events={player.data.recentEvents} />}

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
      <PublicProfileCard />
      <PartnersCard />
      <LogoutButton />
    </div>
  )
}

function RecentEvents({ events }: { events: XpEvent[] }) {
  const { t } = useTranslation('pages', { keyPrefix: 'profile' })
  return (
    <section aria-labelledby="events-title">
      <h2 id="events-title" className="mb-2 text-sm font-semibold uppercase tracking-wide text-muted">
        {t('recentXp')}
      </h2>
      {events.length === 0 ? (
        <EmptyState
          icon={<Sparkles className="size-6" />}
          title={t('noXpTitle')}
          description={t('noXpDescription')}
        />
      ) : (
        <Card className="p-0">
          <ul className="divide-y divide-border">
            {events.map((e, i) => (
              <li key={`${e.date}-${i}`} className="flex items-center gap-3 px-4 py-3">
                <div className="min-w-0 flex-1">
                  <p className="truncate text-sm font-medium">{XP_SOURCE_LABELS[e.source] ?? e.source}</p>
                  <p className="truncate text-xs text-muted">
                    {e.description} · {formatInstant(e.date, { day: 'numeric', month: 'short' })}
                  </p>
                </div>
                <span className={e.amount >= 0 ? 'font-bold text-xp' : 'font-bold text-muted'}>{formatXp(e.amount)}</span>
              </li>
            ))}
          </ul>
        </Card>
      )}
    </section>
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
