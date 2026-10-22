import { Check, Flag, Minus, X } from 'lucide-react'
import { Link } from 'react-router'
import type { CheckInStatus, TodayHabit } from '../../api/types'
import { cn } from '../../lib/cn'
import { STATUS_LABELS } from '../../lib/format'
import { useCheckIn } from './useCheckIn'

const OPTIONS: Array<{ status: CheckInStatus; icon: typeof Check; active: string }> = [
  { status: 'Done', icon: Check, active: 'bg-done text-bg border-done' },
  { status: 'Minimum', icon: Minus, active: 'bg-minimum text-bg border-minimum' },
  { status: 'Missed', icon: X, active: 'bg-missed/15 text-missed border-missed/60' },
]

export function HabitCheckInCard({ habit, date }: { habit: TodayHabit; date: string }) {
  const checkIn = useCheckIn()
  const current = habit.checkIn?.status ?? null

  function handleTap(status: CheckInStatus) {
    // tocar no status atual desfaz
    checkIn.mutate({ habit, date, status: current === status ? null : status })
  }

  return (
    <article className="rounded-2xl border border-border bg-surface p-4">
      <div className="flex items-start gap-3">
        <span className="grid size-11 shrink-0 place-items-center rounded-xl bg-surface-2 text-2xl" aria-hidden>
          {habit.emoji || '•'}
        </span>
        <div className="min-w-0 flex-1">
          <Link to={`/habits/${habit.id}`} className="line-clamp-2 font-semibold break-words hover:underline">
            {habit.name}
          </Link>
          <p className="truncate text-sm text-muted">
            Nv {habit.level} · {habit.levelDescription}
          </p>
          <p className="truncate text-xs text-muted/90">Mínimo: {habit.minimumDescription}</p>
          <div className="mt-1.5 flex flex-wrap gap-1.5">
            {habit.weekTarget !== null && (
              <span
                className={cn(
                  'rounded-full px-2 py-0.5 text-xs font-medium',
                  habit.weekCount >= habit.weekTarget ? 'bg-done/15 text-done' : 'bg-surface-2 text-muted',
                )}
              >
                {habit.weekCount}/{habit.weekTarget} na semana
              </span>
            )}
            {habit.goalTitle && (
              <span className="inline-flex max-w-full items-center gap-1 truncate rounded-full bg-accent/15 px-2 py-0.5 text-xs font-medium text-accent">
                <Flag className="size-3 shrink-0" aria-hidden />
                <span className="truncate">{habit.goalTitle}</span>
              </span>
            )}
          </div>
        </div>
      </div>

      <div className="mt-3 grid grid-cols-3 gap-2" role="group" aria-label={`Check-in de ${habit.name}`}>
        {OPTIONS.map(({ status, icon: Icon, active }) => {
          const selected = current === status
          return (
            <button
              key={status}
              type="button"
              aria-pressed={selected}
              // Pausado (offline, ou esperando a fila): deixa trocar de novo.
              disabled={checkIn.isPending && !checkIn.isPaused}
              onClick={() => handleTap(status)}
              className={cn(
                'flex h-12 min-w-0 items-center justify-center gap-1.5 rounded-xl border px-1 text-sm font-semibold transition active:scale-95',
                'disabled:cursor-wait',
                selected ? active : 'border-border bg-surface-2 text-muted hover:text-text',
              )}
            >
              <Icon className="size-4 shrink-0" aria-hidden />
              {STATUS_LABELS[status]}
            </button>
          )
        })}
      </div>
    </article>
  )
}
