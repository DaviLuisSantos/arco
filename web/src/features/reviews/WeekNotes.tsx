import { useTranslation } from 'react-i18next'
import type { WeekNote } from '../../api/types'
import { Card } from '../../components/ui/Card'
import { formatDate } from '../../lib/dates'

/** Notas dos check-ins da semana, agrupadas por dia. */
export function WeekNotes({ notes }: { notes: WeekNote[] }) {
  const { t } = useTranslation('reviews')
  const { t: tc } = useTranslation('checkins')
  if (notes.length === 0) return null

  const byDate = new Map<string, WeekNote[]>()
  for (const n of notes) byDate.set(n.date, [...(byDate.get(n.date) ?? []), n])

  return (
    <section aria-labelledby="week-notes-title" className="flex flex-col gap-3">
      <h2 id="week-notes-title" className="text-sm font-semibold uppercase tracking-wide text-muted">
        {t('notes.title')}
      </h2>
      <Card className="flex flex-col gap-4">
        {[...byDate.entries()].map(([date, items]) => (
          <div key={date} className="flex flex-col gap-1.5">
            <p className="text-xs font-semibold text-muted">{formatDate(date)}</p>
            <ul className="flex flex-col gap-2">
              {items.map((n) => (
                <li key={`${n.habitId}-${n.date}`} className="text-sm">
                  <span className="font-medium">{n.habitName}</span>{' '}
                  <span className="text-muted">· {tc(`status.${n.status}`)}</span>
                  <p className="mt-0.5 whitespace-pre-line break-words">{n.note}</p>
                </li>
              ))}
            </ul>
          </div>
        ))}
      </Card>
    </section>
  )
}
