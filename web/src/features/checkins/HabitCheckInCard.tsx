import { Check, Flag, Minus, X } from 'lucide-react'
import { useRef, useState } from 'react'
import { Link } from 'react-router'
import { useTranslation } from 'react-i18next'
import type { CheckInStatus, TodayHabit } from '../../api/types'
import { cn } from '../../lib/cn'
import { formatDuration, streakText } from '../../lib/format'
import { useCheckIn } from './useCheckIn'

const OPTIONS: Array<{ status: CheckInStatus; icon: typeof Check; active: string }> = [
  { status: 'Done', icon: Check, active: 'bg-done text-bg border-done' },
  { status: 'Minimum', icon: Minus, active: 'bg-minimum text-bg border-minimum' },
  { status: 'Missed', icon: X, active: 'bg-missed/15 text-missed border-missed/60' },
]

export function HabitCheckInCard({ habit, date }: { habit: TodayHabit; date: string }) {
  const { t } = useTranslation('checkins')
  const checkIn = useCheckIn()
  const current = habit.checkIn?.status ?? null
  const savedMinutes = habit.checkIn?.minutes ?? null
  // null = acompanha o valor salvo; string = rascunho em edição
  const [draft, setDraft] = useState<string | null>(null)
  const text = draft ?? (savedMinutes ? String(savedMinutes) : '')
  const parsed = text.trim() === '' ? 0 : Number(text)
  const validMinutes = Number.isInteger(parsed) && parsed >= 0 && parsed <= 480
  const changed = validMinutes && parsed !== (savedMinutes ?? 0)
  const statusGroup = useRef<HTMLDivElement>(null)

  function handleTap(status: CheckInStatus) {
    // tocar no status atual desfaz
    const next = current === status ? null : status
    // Minutos digitados vão junto com a troca de status: uma só mutação, sem disputa com o blur.
    const withMinutes = next !== null && changed ? { minutes: parsed } : {}
    setDraft(null)
    checkIn.mutate({ habit, date, status: next, ...withMinutes })
  }

  function saveMinutes() {
    if (!current || !changed) return
    checkIn.mutate({ habit, date, status: current, minutes: parsed }, { onSuccess: () => setDraft(null) })
  }

  function handleBlur(e: React.FocusEvent<HTMLInputElement>) {
    // Indo para um botão de status, o toque dele já leva os minutos.
    if (e.relatedTarget instanceof Node && statusGroup.current?.contains(e.relatedTarget)) return
    saveMinutes()
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
            {t('card.level', { level: habit.level })} · {habit.levelDescription}
          </p>
          <p className="truncate text-xs text-muted/90">{t('card.minimum', { value: habit.minimumDescription })}</p>
          <div className="mt-1.5 flex flex-wrap gap-1.5">
            {habit.streak.current > 0 && (
              <span
                className="rounded-full bg-surface-2 px-2 py-0.5 text-xs font-medium text-text"
                title={t('card.streakTitle', {
                  current: streakText(habit.streak),
                  best: streakText({ ...habit.streak, current: habit.streak.best }),
                })}
              >
                <span aria-hidden>🔥 </span>
                {habit.streak.current}
                <span className="sr-only"> {t('card.streakSr')}</span>
              </span>
            )}
            {habit.weekTarget !== null && (
              <span
                className={cn(
                  'rounded-full px-2 py-0.5 text-xs font-medium',
                  habit.weekCount >= habit.weekTarget ? 'bg-done/15 text-done' : 'bg-surface-2 text-muted',
                )}
              >
                {t('card.weekProgress', { done: habit.weekCount, target: habit.weekTarget })}
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

      <div ref={statusGroup} className="mt-3 grid grid-cols-3 gap-2" role="group" aria-label={t('card.checkInOf', { name: habit.name })}>
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
              {t(`status.${status}`)}
            </button>
          )
        })}
      </div>

      {(current === 'Done' || current === 'Minimum') && (
        <form
          onSubmit={(e) => {
            e.preventDefault()
            saveMinutes()
          }}
          className="mt-2 flex items-center gap-2"
        >
          <label htmlFor={`minutes-${habit.id}`} className="shrink-0 text-sm text-muted">
            {t('card.minutesQuestion')}
          </label>
          <input
            id={`minutes-${habit.id}`}
            type="number"
            inputMode="numeric"
            enterKeyHint="done"
            min={0}
            max={480}
            step={1}
            placeholder={t('card.optional')}
            value={text}
            onChange={(e) => setDraft(e.target.value)}
            onBlur={handleBlur}
            aria-invalid={!validMinutes}
            aria-describedby={`minutes-hint-${habit.id}`}
            className="h-10 min-w-0 flex-1 rounded-xl border border-border bg-surface-2 px-3 text-sm"
          />
          <span id={`minutes-hint-${habit.id}`} className="sr-only">
            {t('card.minutesHint')}
          </span>
          {!changed && savedMinutes ? (
            <span className="shrink-0 text-xs text-muted">{formatDuration(savedMinutes)}</span>
          ) : null}
        </form>
      )}
    </article>
  )
}
