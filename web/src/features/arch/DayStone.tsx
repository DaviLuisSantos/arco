import { useQuery } from '@tanstack/react-query'
import { focusApi } from '../../api/endpoints'
import { Card } from '../../components/ui/Card'
import { habitColor } from '../../lib/habitColor'
import { formatDuration } from '../../lib/format'
import { queryKeys } from '../../lib/queryKeys'

const BRICK_PX_PER_MIN = 0.6
const BRICK_MIN_PX = 14
const BRICK_MAX_PX = 56

/** Pedra do dia: um tijolo por sessão de foco de hoje, empilhados por hábito. */
export function DayStone({ date }: { date: string }) {
  const q = useQuery({ queryKey: queryKeys.focus(date, date), queryFn: () => focusApi.list(date, date) })

  if (q.isPending || q.isLoadingError) return null
  const { sessions, totalMinutes, byHabit } = q.data

  const ordered = byHabit.flatMap((h) =>
    sessions.filter((s) => s.habitId === h.habitId).map((s) => ({ ...s, name: h.name })),
  )

  return (
    <Card>
      <section aria-labelledby="day-stone-title" className="flex items-center gap-4">
        <div
          className="flex h-28 w-20 shrink-0 flex-col-reverse justify-start gap-0.5 overflow-hidden rounded-t-[2.5rem] rounded-b-lg border border-border bg-surface-2 p-1.5"
          role="img"
          aria-label={
            ordered.length === 0
              ? 'Pedra do dia vazia'
              : `Pedra do dia com ${ordered.length} tijolo${ordered.length > 1 ? 's' : ''}`
          }
        >
          {ordered.map((s) => (
            <div
              key={s.id}
              title={`${s.name}: ${formatDuration(s.minutes)}`}
              className="w-full shrink-0 rounded-sm"
              style={{
                background: habitColor(s.habitId),
                height: Math.min(BRICK_MAX_PX, Math.max(BRICK_MIN_PX, s.minutes * BRICK_PX_PER_MIN)),
              }}
            />
          ))}
        </div>
        <div className="min-w-0 flex-1">
          <h2 id="day-stone-title" className="text-sm font-semibold uppercase tracking-wide text-muted">
            Pedra do dia
          </h2>
          {ordered.length === 0 ? (
            <p className="mt-1 text-sm text-muted">
              Nenhum foco hoje ainda. Inicie o timer em um hábito para assentar o primeiro tijolo.
            </p>
          ) : (
            <>
              <p className="text-2xl font-extrabold tabular-nums">{formatDuration(totalMinutes)}</p>
              <ul className="mt-1 flex flex-col gap-0.5 text-sm">
                {byHabit.map((h) => (
                  <li key={h.habitId} className="flex items-center gap-2">
                    <span
                      className="size-2.5 shrink-0 rounded-sm"
                      style={{ background: habitColor(h.habitId) }}
                      aria-hidden
                    />
                    <span className="truncate">
                      {h.emoji ? `${h.emoji} ` : ''}
                      {h.name}
                    </span>
                    <span className="ml-auto text-muted tabular-nums">{formatDuration(h.minutes)}</span>
                  </li>
                ))}
              </ul>
            </>
          )}
        </div>
      </section>
    </Card>
  )
}
