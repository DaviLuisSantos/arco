import { useQuery } from '@tanstack/react-query'
import { ArrowLeft } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { statsApi } from '../api/endpoints'
import { Card } from '../components/ui/Card'
import { PageHeader } from '../components/ui/PageHeader'
import { ErrorState, Spinner } from '../components/ui/States'
import { PeriodStats } from '../features/reviews/PeriodStats'
import { WeeklyChart } from '../features/stats/WeeklyChart'
import { queryKeys } from '../lib/queryKeys'

export function StatsPage() {
  const { t } = useTranslation('pages', { keyPrefix: 'stats' })
  const stats = useQuery({ queryKey: queryKeys.stats, queryFn: statsApi.get })

  return (
    <div className="flex flex-col gap-5">
      <div>
        <Link to="/profile" className="mb-4 inline-flex items-center gap-1 text-sm text-muted hover:text-text">
          <ArrowLeft className="size-4" aria-hidden />
          {t('back')}
        </Link>
        <PageHeader title={t('title')} />
      </div>

      {stats.isPending ? (
        <Spinner />
      ) : stats.isLoadingError ? (
        <ErrorState error={stats.error} onRetry={() => void stats.refetch()} />
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
