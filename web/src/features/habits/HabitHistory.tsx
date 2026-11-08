import { useQuery } from '@tanstack/react-query'
import { ChevronLeft, ChevronRight } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { checkInsApi } from '../../api/endpoints'
import type { CheckInStatus } from '../../api/types'
import { Button } from '../../components/ui/Button'
import { Dialog } from '../../components/ui/Dialog'
import { cn } from '../../lib/cn'
import {
  addDays,
  addMonths,
  dayOfWeek,
  formatDate,
  formatMonth,
  monthEnd,
  monthStart,
  WEEKDAY_INITIALS,
} from '../../lib/dates'
import { queryKeys } from '../../lib/queryKeys'
import { useAppMutation } from '../../lib/useAppMutation'

/** Mesmo limite do servidor (CheckInEndpoints.MaxDaysInPast). */
const MAX_DAYS_IN_PAST = 7

const CELL = {
  Done: 'bg-done text-bg',
  Minimum: 'bg-minimum text-bg',
  Missed: 'bg-missed/20 text-missed',
  none: 'bg-surface-2 text-muted',
  future: 'text-muted/40',
}

const STATUSES: CheckInStatus[] = ['Done', 'Minimum', 'Missed']

/** Segunda a domingo, como a semana do app. */
const WEEK_HEADER = [1, 2, 3, 4, 5, 6, 0].map((d) => WEEKDAY_INITIALS[d])

/** Calendário de check-ins de um hábito, mês a mês, com o resumo do mês. */
export function HabitHistory({
  habitId,
  today,
  firstDay,
  editable = true,
}: {
  habitId: number
  today: string
  firstDay: string
  /** false para hábitos arquivados (o servidor recusa check-in). */
  editable?: boolean
}) {
  const { t } = useTranslation(['habits', 'checkins'])
  const [month, setMonth] = useState(() => monthStart(today))
  const [editing, setEditing] = useState<string | null>(null)
  const earliestEditable = addDays(today, -MAX_DAYS_IN_PAST)
  const save = useAppMutation(
    ({ date, status }: { date: string; status: CheckInStatus | null }) =>
      status ? checkInsApi.upsert({ habitId, date, status }) : checkInsApi.remove(habitId, date),
    { onSuccess: () => setEditing(null) },
  )
  const from = month
  const to = monthEnd(month)
  const checkIns = useQuery({
    queryKey: queryKeys.habitMonth(habitId, month),
    queryFn: () => checkInsApi.list(from, to, habitId),
  })

  const byDate = new Map((checkIns.data ?? []).map((c) => [c.date, c.status]))
  const blanks = (dayOfWeek(from) + 6) % 7
  const days = Array.from({ length: Number(to.slice(8)) }, (_, i) => addDays(from, i))
  const canGoBack = month > monthStart(firstDay)
  const canGoForward = month < monthStart(today)

  return (
    <div className="flex flex-col gap-3">
      <div className="flex items-center justify-between">
        <Button
          variant="ghost"
          size="sm"
          aria-label={t('history.prevMonth')}
          disabled={!canGoBack}
          onClick={() => setMonth(addMonths(month, -1))}
        >
          <ChevronLeft className="size-4" aria-hidden />
        </Button>
        <p className="text-sm font-semibold first-letter:uppercase" aria-live="polite">
          {formatMonth(month)}
        </p>
        <Button
          variant="ghost"
          size="sm"
          aria-label={t('history.nextMonth')}
          disabled={!canGoForward}
          onClick={() => setMonth(addMonths(month, 1))}
        >
          <ChevronRight className="size-4" aria-hidden />
        </Button>
      </div>

      <div>
        <div className="mb-1 grid grid-cols-7 gap-1.5 text-center text-xs text-muted" aria-hidden>
          {WEEK_HEADER.map((initial, i) => (
            <span key={i}>{initial}</span>
          ))}
        </div>
        <ol className={cn('grid grid-cols-7 gap-1.5', checkIns.isPending && 'opacity-50')}>
          {Array.from({ length: blanks }, (_, i) => (
            <li key={`blank-${i}`} aria-hidden />
          ))}
          {days.map((d) => {
            const status = byDate.get(d)
            const future = d > today
            const label = `${formatDate(d, { day: 'numeric', month: 'long' })}: ${status ? t(`checkins:status.${status}`) : t('history.noCheckIn')}`
            const canEdit = editable && !future && d >= earliestEditable && d >= firstDay
            const cls = cn(
              'grid aspect-square w-full place-items-center rounded-lg text-xs font-semibold',
              future ? CELL.future : CELL[status ?? 'none'],
              d === today && 'ring-2 ring-accent ring-offset-2 ring-offset-surface',
              canEdit && 'cursor-pointer hover:brightness-110 focus-visible:outline-2 focus-visible:outline-accent',
            )
            return (
              <li key={d} title={future ? undefined : label} aria-label={canEdit ? undefined : future ? undefined : label}>
                {canEdit ? (
                  <button type="button" className={cls} aria-label={t('history.editDay', { label })} onClick={() => setEditing(d)}>
                    {Number(d.slice(8))}
                  </button>
                ) : (
                  <span className={cls}>{Number(d.slice(8))}</span>
                )}
              </li>
            )
          })}
        </ol>
      </div>

      <div className="flex flex-wrap gap-3 text-xs text-muted">
        {STATUSES.map((s) => (
          <span key={s} className="inline-flex items-center gap-1.5">
            <span className={cn('size-3 rounded', CELL[s])} aria-hidden />
            {t(`checkins:status.${s}`)}{' '}
            <span className="font-semibold text-text">
              {(checkIns.data ?? []).filter((c) => c.status === s).length}
            </span>
          </span>
        ))}
      </div>

      {editable && (
        <p className="text-xs text-muted">{t('history.hint', { days: MAX_DAYS_IN_PAST })}</p>
      )}

      <Dialog
        open={editing !== null}
        onClose={() => setEditing(null)}
        title={editing ? formatDate(editing, { weekday: 'long', day: 'numeric', month: 'long' }) : ''}
        description={t('history.recalc')}
      >
        {editing && (
          <div className="flex flex-col gap-2 pt-1">
            {STATUSES.map((s) => (
              <Button
                key={s}
                variant={byDate.get(editing) === s ? 'primary' : 'secondary'}
                block
                loading={save.isPending && save.variables?.status === s}
                disabled={save.isPending}
                onClick={() => save.mutate({ date: editing, status: s })}
              >
                {t(`checkins:status.${s}`)}
              </Button>
            ))}
            {byDate.has(editing) && (
              <Button
                variant="ghost"
                block
                loading={save.isPending && save.variables?.status === null}
                disabled={save.isPending}
                onClick={() => save.mutate({ date: editing, status: null })}
              >
                {t('history.remove')}
              </Button>
            )}
          </div>
        )}
      </Dialog>
    </div>
  )
}
