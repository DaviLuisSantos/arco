import type { HabitLevel } from '../../api/types'
import { cn } from '../../lib/cn'
import { formatInstant } from '../../lib/dates'

export function LevelTimeline({ levels, current }: { levels: HabitLevel[]; current: number }) {
  const ordered = [...levels].sort((a, b) => b.level - a.level)
  return (
    <ol className="relative ml-3 border-l-2 border-border">
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
              Nível {l.level}
              {isCurrent && ' · atual'}
            </p>
            <p className="text-sm">{l.description}</p>
            <p className="text-xs text-muted">
              Mínimo: {l.minimumDescription} · desde {formatInstant(l.reachedAt)}
            </p>
          </li>
        )
      })}
    </ol>
  )
}
