import { useTranslation } from 'react-i18next'
import type { HabitLevel, HabitPlannedLevel } from '../../api/types'
import { cn } from '../../lib/cn'
import { formatInstant } from '../../lib/dates'

export function LevelTimeline({
  levels,
  planned = [],
  current,
}: {
  levels: HabitLevel[]
  planned?: HabitPlannedLevel[]
  current: number
}) {
  const { t } = useTranslation('habits')
  const ordered = [...levels].sort((a, b) => b.level - a.level)
  return (
    <ol className="relative ml-3 border-l-2 border-border">
      {[...planned].reverse().map((l) => (
        <li key={`planned-${l.level}`} className="relative pb-4 pl-5">
          <span
            className="absolute top-1 -left-[9px] size-4 rounded-full border-2 border-dashed border-border bg-surface"
            aria-hidden
          />
          <p className="text-sm font-semibold text-muted">{t('timeline.planned', { level: l.level })}</p>
          <p className="text-sm text-muted">{l.description}</p>
          <p className="text-xs text-muted">{t('timeline.minimum', { value: l.minimumDescription })}</p>
        </li>
      ))}
      {ordered.map((l) => {
        const isCurrent = l.level === current
        return (
          <li key={l.level} className="relative pb-4 pl-5 last:pb-0">
            <span
              className={cn(
                'absolute top-1 -left-[9px] size-4 rounded-full border-2',
                isCurrent ? 'border-accent bg-accent' : 'border-border bg-surface',
              )}
              aria-hidden
            />
            <p className={cn('text-sm font-semibold', isCurrent ? 'text-accent' : 'text-text')}>
              {t('timeline.level', { level: l.level })}
              {isCurrent && ` · ${t('timeline.current')}`}
            </p>
            <p className="text-sm">{l.description}</p>
            <p className="text-xs text-muted">
              {t('timeline.minimumSince', { value: l.minimumDescription, date: formatInstant(l.reachedAt) })}
            </p>
          </li>
        )
      })}
    </ol>
  )
}
