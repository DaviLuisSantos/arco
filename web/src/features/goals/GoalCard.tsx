import { Check, CircleCheck, Link2, Trash2 } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { goalsApi } from '../../api/endpoints'
import type { Goal, Milestone } from '../../api/types'
import { Button } from '../../components/ui/Button'
import { Card } from '../../components/ui/Card'
import { ConfirmDialog } from '../../components/ui/ConfirmDialog'
import { Input } from '../../components/ui/Input'
import { ProgressBar } from '../../components/ui/ProgressBar'
import { cn } from '../../lib/cn'
import { diffDays, formatDate, formatInstant } from '../../lib/dates'
import { formatNumber, goalProgressText, parseDecimal } from '../../lib/format'
import { useAppMutation } from '../../lib/useAppMutation'

export function GoalCard({ goal, today }: { goal: Goal; today: string }) {
  const { t } = useTranslation(['goals', 'common'])
  const [confirm, setConfirm] = useState<'delete' | 'complete' | null>(null)
  const done = goal.completedAt !== null
  const daysLeft = diffDays(today, goal.deadline)

  const remove = useAppMutation(() => goalsApi.remove(goal.id), {
    successMessage: t('card.deleted'),
    onSuccess: () => setConfirm(null),
  })
  const complete = useAppMutation(() => goalsApi.complete(goal.id), {
    successMessage: t('card.completed'),
    onSuccess: () => setConfirm(null),
  })

  return (
    <Card className="flex flex-col gap-3">
      <div className="flex items-start gap-2">
        <div className="min-w-0 flex-1">
          <h3 className="font-bold break-words">{goal.title}</h3>
          {goal.description && <p className="text-sm text-muted">{goal.description}</p>}
        </div>
        {done ? (
          <span className="inline-flex shrink-0 items-center gap-1 rounded-full bg-done/15 px-2 py-0.5 text-xs font-semibold text-done">
            <CircleCheck className="size-3.5" aria-hidden />
            {t('card.done')}
          </span>
        ) : (
          <span className="shrink-0 rounded-full bg-surface-2 px-2 py-0.5 text-xs text-muted">
            {t(`type.${goal.type}`)}
          </span>
        )}
      </div>

      <div>
        <ProgressBar value={goal.progress} tone={done ? 'done' : 'accent'} label={t('card.progressOf', { title: goal.title })} />
        <div className="mt-1.5 flex flex-wrap justify-between gap-x-3 text-xs text-muted">
          <span>
            {goalProgressText(goal)} · {Math.round(goal.progress * 100)}%
          </span>
          <span>
            {done && goal.completedAt
              ? t('card.completedOn', { date: formatInstant(goal.completedAt, { day: 'numeric', month: 'short' }) })
              : `${t('card.deadline', { date: formatDate(goal.deadline) })}${daysLeft >= 0 ? ` · ${t('card.daysLeft', { count: daysLeft })}` : ` · ${t('card.overdue')}`}`}
          </span>
        </div>
        {goal.habitIds.length > 0 && (
          <p className="mt-1 inline-flex items-center gap-1 text-xs text-muted">
            <Link2 className="size-3" aria-hidden />
            {t('card.linkedHabits', { count: goal.habitIds.length })}
          </p>
        )}
      </div>

      {goal.type === 'Numeric' ? (
        <NumericDetails goal={goal} />
      ) : (
        <MilestoneList goal={goal} />
      )}

      <div className="flex gap-2 border-t border-border pt-3">
        {!done && (
          <Button variant="secondary" size="sm" onClick={() => setConfirm('complete')}>
            <Check className="size-4" aria-hidden />
            {t('card.complete')}
          </Button>
        )}
        <Button variant="ghost" size="sm" className="ml-auto" onClick={() => setConfirm('delete')}>
          <Trash2 className="size-4" aria-hidden />
          {t('common:delete')}
        </Button>
      </div>

      <ConfirmDialog
        open={confirm === 'complete'}
        onClose={() => setConfirm(null)}
        onConfirm={() => complete.mutate()}
        loading={complete.isPending}
        title={t('card.completeTitle')}
        description={
          <>
            {t('card.completeDescription', { title: goal.title })} <span className="font-semibold text-xp">+150 XP</span>
          </>
        }
        confirmLabel={t('card.complete')}
      />
      <ConfirmDialog
        open={confirm === 'delete'}
        onClose={() => setConfirm(null)}
        onConfirm={() => remove.mutate()}
        loading={remove.isPending}
        title={t('card.deleteTitle')}
        description={t('card.deleteDescription')}
        confirmLabel={t('common:delete')}
        danger
      />
    </Card>
  )
}

function NumericDetails({ goal }: { goal: Goal }) {
  const { t } = useTranslation('goals')
  const [value, setValue] = useState('')
  const [note, setNote] = useState('')
  const [error, setError] = useState<string | null>(null)
  const unit = goal.unit ? ` ${goal.unit}` : ''
  const stats: Array<{ key: string; label: string; value: number | null }> = [
    { key: 'start', label: t('card.start'), value: goal.startValue },
    { key: 'current', label: t('card.current'), value: goal.currentValue ?? goal.startValue },
    { key: 'target', label: t('card.target'), value: goal.targetValue },
  ]
  const progress = useAppMutation((v: number) => goalsApi.progress(goal.id, { value: v, note: note.trim() || null }), {
    successMessage: t('card.progressSaved'),
    onSuccess: () => {
      setValue('')
      setNote('')
    },
  })

  function handleSubmit(e: FormEvent) {
    e.preventDefault()
    const parsed = parseDecimal(value)
    if (parsed === null) {
      setError(t('card.numberRequired'))
      return
    }
    setError(null)
    progress.mutate(parsed)
  }

  return (
    <div className="flex flex-col gap-3">
      <dl className="grid grid-cols-3 gap-2 text-center">
        {stats.map(({ key, label, value: v }) => (
          <div key={key} className="rounded-xl bg-surface-2 px-2 py-2">
            <dt className="text-xs text-muted">{label}</dt>
            <dd className="font-semibold">{v === null ? '—' : `${formatNumber(v)}${unit}`}</dd>
          </div>
        ))}
      </dl>
      {goal.completedAt === null && (
        <form onSubmit={handleSubmit} className="flex flex-col gap-2" noValidate>
          <div className="flex items-start gap-2">
            <div className="min-w-0 flex-1">
              <Input
                aria-label={t('card.newValue')}
                inputMode="decimal"
                value={value}
                onChange={(e) => setValue(e.target.value)}
                placeholder={`${t('card.newValue')}${unit}`}
                error={error}
              />
            </div>
            <Button type="submit" loading={progress.isPending} className="shrink-0">
              {t('card.register')}
            </Button>
          </div>
          {value && (
            <Input aria-label={t('card.note')} value={note} onChange={(e) => setNote(e.target.value)} placeholder={t('card.note')} />
          )}
        </form>
      )}
    </div>
  )
}

function MilestoneList({ goal }: { goal: Goal }) {
  const toggle = useAppMutation((m: Milestone) => goalsApi.toggleMilestone(goal.id, m.id))
  const ordered = [...goal.milestones].sort((a, b) => a.order - b.order)

  return (
    <ul className="flex flex-col gap-1">
      {ordered.map((m) => {
        const checked = m.doneAt !== null
        const busy = toggle.isPending && toggle.variables?.id === m.id
        return (
          <li key={m.id}>
            <label
              className={cn(
                'flex min-h-11 cursor-pointer items-center gap-3 rounded-xl px-2 py-2 hover:bg-surface-2',
                busy && 'opacity-60',
              )}
            >
              <input
                type="checkbox"
                checked={checked}
                disabled={toggle.isPending}
                onChange={() => toggle.mutate(m)}
                className="size-5 shrink-0 accent-[var(--done)]"
              />
              <span className={cn('flex-1 text-sm', checked && 'text-muted line-through')}>{m.title}</span>
              {!checked && <span className="text-xs text-xp">+25 XP</span>}
            </label>
          </li>
        )
      })}
    </ul>
  )
}
