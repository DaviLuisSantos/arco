import { useQuery } from '@tanstack/react-query'
import { ArrowLeft, Flame } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router'
import { partnersApi } from '../api/endpoints'
import { Card } from '../components/ui/Card'
import { PageHeader } from '../components/ui/PageHeader'
import { ProgressBar } from '../components/ui/ProgressBar'
import { ErrorState, Spinner } from '../components/ui/States'
import { formatDate } from '../lib/dates'

export function PartnerSummaryPage() {
  const { t } = useTranslation('partners')
  const id = Number(useParams().id)
  const query = useQuery({
    queryKey: ['partners', id, 'summary'],
    queryFn: () => partnersApi.summary(id),
    enabled: Number.isFinite(id),
  })

  if (query.isPending) return <Spinner />
  if (query.isError) return <ErrorState error={query.error} onRetry={() => void query.refetch()} />
  const { owner, weeks } = query.data

  return (
    <div className="space-y-5">
      <Link to="/profile" className="inline-flex items-center gap-1 text-sm text-muted hover:text-text">
        <ArrowLeft className="size-4" aria-hidden /> {t('summary.back')}
      </Link>
      <PageHeader
        title={t('summary.title', { name: owner.displayName })}
        subtitle={t('summary.level', { level: owner.level })}
      />
      {weeks.map((w, i) => (
        <section key={w.start} aria-label={i === 0 ? t('summary.thisWeek') : t('summary.lastWeek')}>
          <h2 className="mb-2 text-sm font-semibold uppercase tracking-wide text-muted">
            {i === 0 ? t('summary.thisWeek') : t('summary.lastWeek')} · {formatDate(w.start)} – {formatDate(w.end)}
          </h2>
          <Card className="space-y-4">
            <p className="text-sm text-muted">{t('summary.totals', { ...w.totals })}</p>
            {w.habits.length === 0 && <p className="text-sm text-muted">{t('summary.noHabits')}</p>}
            <ul className="space-y-3">
              {w.habits.map((h) => (
                <li key={h.name} className="space-y-1.5">
                  <div className="flex items-center justify-between gap-2">
                    <span className="truncate font-semibold">{h.name}</span>
                    <span className="flex shrink-0 items-center gap-2 text-sm text-muted">
                      {h.streak > 0 && (
                        <span className="inline-flex items-center gap-1" title={t('summary.streak', { count: h.streak })}>
                          <Flame className="size-4 text-xp" aria-hidden />
                          {h.streak}
                        </span>
                      )}
                      {Math.round(h.rate * 100)}%
                    </span>
                  </div>
                  <ProgressBar value={h.rate} label={h.name} tone="done" size="sm" />
                  <p className="text-xs text-muted">
                    {t('summary.habitLine', { done: h.done, minimum: h.minimum, expected: h.expected })}
                  </p>
                </li>
              ))}
            </ul>
          </Card>
        </section>
      ))}
    </div>
  )
}
