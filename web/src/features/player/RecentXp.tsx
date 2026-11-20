import { Sparkles } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import type { XpEvent } from '../../api/types'
import { Card } from '../../components/ui/Card'
import { EmptyState } from '../../components/ui/States'
import { formatInstant } from '../../lib/dates'
import { formatXp, XP_SOURCE_LABELS } from '../../lib/format'

/** Últimos lançamentos do livro-razão de XP. */
export function RecentXp({ events }: { events: XpEvent[] }) {
  const { t } = useTranslation('pages', { keyPrefix: 'profile' })
  return (
    <section aria-labelledby="events-title">
      <h2 id="events-title" className="mb-2 text-sm font-semibold uppercase tracking-wide text-muted">
        {t('recentXp')}
      </h2>
      {events.length === 0 ? (
        <EmptyState icon={<Sparkles className="size-6" />} title={t('noXpTitle')} description={t('noXpDescription')} />
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
