import { useTranslation } from 'react-i18next'
import type { MoodSummary } from '../../api/types'
import { Card } from '../../components/ui/Card'

function pct(v: number) {
  return `${Math.round(v * 100)}%`
}

/** Humor da semana: média e consistência nos dias bons x ruins. */
export function WeekMood({ mood }: { mood: MoodSummary }) {
  const { t } = useTranslation('reviews')
  const consistency = (v: number | null) => (v === null ? t('mood.noData') : pct(v))

  return (
    <section aria-labelledby="week-mood-title" className="flex flex-col gap-3">
      <h2 id="week-mood-title" className="text-sm font-semibold uppercase tracking-wide text-muted">
        {t('mood.title')}
      </h2>
      <Card className="flex flex-col gap-3">
        {mood.days === 0 || mood.average === null ? (
          <p className="text-sm text-muted">{t('mood.empty')}</p>
        ) : (
          <>
            <p className="text-sm">
              {t('mood.average')}{' '}
              <span className="font-semibold tabular-nums" data-testid="mood-average">
                {mood.average.toFixed(1)}
              </span>{' '}
              <span className="text-muted">{t('mood.daysLogged', { count: mood.days })}</span>
            </p>
            <dl className="grid grid-cols-2 gap-3 text-sm">
              <div>
                <dt className="text-muted">{t('mood.goodDays', { count: mood.goodDays })}</dt>
                <dd className="font-semibold tabular-nums">{consistency(mood.goodConsistency)}</dd>
              </div>
              <div>
                <dt className="text-muted">{t('mood.badDays', { count: mood.badDays })}</dt>
                <dd className="font-semibold tabular-nums">{consistency(mood.badConsistency)}</dd>
              </div>
            </dl>
            <p className="text-xs text-muted">{t('mood.consistencyHint')}</p>
          </>
        )}
      </Card>
    </section>
  )
}
