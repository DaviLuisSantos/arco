import type { CheckIn } from '../../api/types'
import { cn } from '../../lib/cn'
import { addDays, dayOfWeek, formatDate, WEEKDAY_INITIALS } from '../../lib/dates'
import { STATUS_LABELS } from '../../lib/format'

const DAYS = 28

const CELL = {
  Done: 'bg-done text-bg',
  Minimum: 'bg-minimum text-bg',
  Missed: 'bg-missed/20 text-missed',
  none: 'bg-surface-2 text-muted',
}

/** Últimos 28 dias (até hoje), em 4 linhas de 7. */
export function CheckInGrid({ checkIns, today }: { checkIns: CheckIn[]; today: string }) {
  const byDate = new Map(checkIns.map((c) => [c.date, c]))
  const start = addDays(today, -(DAYS - 1))
  const days = Array.from({ length: DAYS }, (_, i) => addDays(start, i))

  return (
    <div>
      <div className="mb-1 grid grid-cols-7 gap-1.5 text-center text-xs text-muted" aria-hidden>
        {days.slice(0, 7).map((d) => (
          <span key={d}>{WEEKDAY_INITIALS[dayOfWeek(d)]}</span>
        ))}
      </div>
      <ol className="grid grid-cols-7 gap-1.5">
        {days.map((d) => {
          const c = byDate.get(d)
          const label = `${formatDate(d, { day: 'numeric', month: 'long' })}: ${c ? STATUS_LABELS[c.status] : 'sem check-in'}`
          return (
            <li
              key={d}
              title={label}
              aria-label={label}
              className={cn(
                'grid aspect-square place-items-center rounded-lg text-xs font-semibold',
                CELL[c?.status ?? 'none'],
                d === today && 'ring-2 ring-accent ring-offset-2 ring-offset-surface',
              )}
            >
              {Number(d.slice(8))}
            </li>
          )
        })}
      </ol>
      <div className="mt-3 flex flex-wrap gap-3 text-xs text-muted">
        {(['Done', 'Minimum', 'Missed'] as const).map((s) => (
          <span key={s} className="inline-flex items-center gap-1.5">
            <span className={cn('size-3 rounded', CELL[s])} aria-hidden />
            {STATUS_LABELS[s]}
          </span>
        ))}
      </div>
    </div>
  )
}
