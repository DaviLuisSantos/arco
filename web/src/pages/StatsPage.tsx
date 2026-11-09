import { useQuery } from '@tanstack/react-query'
import { ArrowLeft, ChartColumn } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { statsApi } from '../api/endpoints'
import { Card } from '../components/ui/Card'
import { PageHeader } from '../components/ui/PageHeader'
import { Button } from '../components/ui/Button'
import { Skeleton } from '../components/ui/Skeleton'
import { EmptyState, ErrorState, Spinner } from '../components/ui/States'
import { PeriodStats } from '../features/reviews/PeriodStats'
import { WeeklyChart } from '../features/stats/WeeklyChart'
import { queryKeys } from '../lib/queryKeys'
import { useOnline } from '../lib/useOnline'

export function StatsPage({ embedded = false }: { embedded?: boolean }) {
  const { t } = useTranslation('pages', { keyPrefix: 'stats' })
  const { t: tp } = useTranslation('progress')
  const online = useOnline()
  const stats = useQuery({ queryKey: queryKeys.stats, queryFn: statsApi.get })

  return (
    <div className="flex flex-col gap-5">
      {!embedded && (
        <div>
          <Link to="/progress" className="mb-4 inline-flex items-center gap-1 text-sm text-muted hover:text-text">
            <ArrowLeft className="size-4" aria-hidden />
            {tp('back')}
          </Link>
          <PageHeader title={t('title')} />
        </div>
      )}

      {stats.isPending ? (
        online ? (
          <div className="flex flex-col gap-3" role="status" aria-label={tp('title')}>
            <Skeleton className="h-6 w-40" />
            <Skeleton className="h-48 w-full" />
            <Skeleton className="h-24 w-full" />
          </div>
        ) : (
          <Spinner />
        )
      ) : stats.isLoadingError ? (
        <ErrorState error={stats.error} onRetry={() => void stats.refetch()} />
      ) : stats.data.recent.xp === 0 && stats.data.weeks.every((w) => w.done + w.minimum + w.missed === 0) ? (
        <EmptyState
          icon={<ChartColumn className="size-6" />}
          title={tp('statsEmpty.title')}
          description={tp('statsEmpty.description')}
          action={
            <Link to="/">
              <Button>{tp('statsEmpty.action')}</Button>
            </Link>
          }
        />
      ) : (
        <>
          <section aria-labelledby="weeks-title">
            <h2 id="weeks-title" className="mb-2 text-sm font-semibold uppercase tracking-wide text-muted">
              {t('lastWeeks', { count: stats.data.weeks.length })}
            </h2>
            <Card>
              <WeeklyChart weeks={stats.data.weeks} today={stats.data.recent.to} />
            </Card>
            <p className="mt-2 text-xs text-muted">
              {t('consistencyNote')}
            </p>
          </section>

          <section aria-labelledby="recent-title">
            <h2 id="recent-title" className="mb-2 text-sm font-semibold uppercase tracking-wide text-muted">
              {t('last30')}
            </h2>
            <PeriodStats
              xp={stats.data.recent.xp}
              checkIns={stats.data.recent.checkIns}
              habits={stats.data.recent.habits}
              minutes={stats.data.recent.minutes}
              showMinutes
            />
            <Link to="/cycle" className="mt-3 inline-flex items-center gap-1 text-sm text-accent hover:underline">
              {t('viewArch')}
            </Link>
          </section>
        </>
      )}
    </div>
  )
}
