import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import type { CheckInStatus, TodayHabit } from '../../api/types'
import { cn } from '../../lib/cn'
import { addQuantity, formatQuantity, parseQuantity, parseTotal } from '../../lib/quantity'

const BADGE: Record<CheckInStatus | 'None', string> = {
  Done: 'bg-done/15 text-done',
  Minimum: 'bg-minimum/20 text-text',
  Missed: 'bg-missed/15 text-missed',
  Skipped: 'bg-surface-2 text-text',
  None: 'bg-surface-2 text-muted',
}

type Mode = 'add' | 'set' | null

/**
 * Total do dia de um hábito quantitativo: "3 / 8 copos" com barra, "+1", "+ valor" e "Corrigir total".
 * O status vem do total (o selo só mostra); quem grava é `onSet`, sempre com o total absoluto.
 */
export function QuantityControls({
  habit,
  onSet,
  disabled,
}: {
  habit: TodayHabit
  onSet: (total: number) => void
  disabled: boolean
}) {
  const { t } = useTranslation('checkins')
  const [mode, setMode] = useState<Mode>(null)
  const [text, setText] = useState('')
  const [invalid, setInvalid] = useState(false)

  const unit = habit.unit ?? ''
  const target = habit.target ?? 0
  const minimum = habit.minimumTarget ?? 0
  const total = habit.quantityToday
  const status = habit.checkIn?.status ?? null
  const ratio = target > 0 ? Math.min(1, total / target) : 0
  const fillColor = total >= target ? 'bg-done' : total >= minimum ? 'bg-minimum' : 'bg-accent'
  const showTick = minimum > 0 && minimum < target
  const inputId = `qty-${habit.id}`

  function open(next: Exclude<Mode, null>) {
    if (mode === next) {
      setMode(null)
      return
    }
    setMode(next)
    setInvalid(false)
    setText(next === 'set' ? formatQuantity(total) : '')
  }

  function submit(e: FormEvent) {
    e.preventDefault()
    if (mode === 'add') {
      const value = parseQuantity(text)
      if (value === null) return setInvalid(true)
      onSet(addQuantity(total, value))
    } else {
      const value = parseTotal(text)
      if (value === null) return setInvalid(true)
      onSet(value)
    }
    setMode(null)
  }

  const actionButton =
    'flex h-12 min-w-0 items-center justify-center rounded-xl border border-border bg-surface-2 px-1 text-sm font-semibold ' +
    'text-text transition active:scale-95 disabled:cursor-wait disabled:opacity-60'

  return (
    <div role="group" aria-label={t('qty.groupLabel', { name: habit.name })} className="mt-3 flex flex-col gap-2">
      <div className="flex items-center justify-between gap-2">
        <p className="min-w-0 text-lg font-semibold tabular-nums break-words" data-testid="qty-progress">
          {t('qty.progress', { total: formatQuantity(total), target: formatQuantity(target), unit })}
        </p>
        <span className={cn('shrink-0 rounded-full px-2 py-0.5 text-xs font-semibold', BADGE[status ?? 'None'])}>
          {status ? t(`status.${status}`) : t('qty.pending')}
        </span>
      </div>

      <div
        role="progressbar"
        aria-label={t('qty.barLabel', { name: habit.name })}
        aria-valuemin={0}
        aria-valuemax={target}
        aria-valuenow={Math.min(total, target)}
        aria-valuetext={t('qty.progress', { total: formatQuantity(total), target: formatQuantity(target), unit })}
        className="relative h-2.5 overflow-hidden rounded-full bg-surface-2"
      >
        <div className={cn('h-full rounded-full transition-[width]', fillColor)} style={{ width: `${ratio * 100}%` }} />
        {showTick && (
          <span
            aria-hidden
            className="absolute inset-y-0 w-0.5 bg-text/50"
            style={{ left: `${(minimum / target) * 100}%` }}
          />
        )}
      </div>
      {showTick && (
        <p className="text-xs text-muted">{t('qty.minimumMark', { value: `${formatQuantity(minimum)} ${unit}` })}</p>
      )}

      <div className="grid grid-cols-3 gap-1.5">
        <button
          type="button"
          disabled={disabled}
          onClick={() => onSet(addQuantity(total, 1))}
          className={actionButton}
        >
          {t('qty.addOne')}
        </button>
        <button
          type="button"
          aria-expanded={mode === 'add'}
          onClick={() => open('add')}
          className={cn(actionButton, mode === 'add' && 'border-accent')}
        >
          {t('qty.addValue')}
        </button>
        <button
          type="button"
          aria-expanded={mode === 'set'}
          onClick={() => open('set')}
          className={cn(actionButton, mode === 'set' && 'border-accent')}
        >
          {t('qty.correct')}
        </button>
      </div>

      {mode && (
        <form onSubmit={submit} className="flex items-start gap-2">
          <div className="flex min-w-0 flex-1 flex-col gap-1">
            <label htmlFor={inputId} className="text-sm text-muted">
              {mode === 'add' ? t('qty.addLabel') : t('qty.totalLabel')}
            </label>
            <input
              id={inputId}
              type="text"
              inputMode="decimal"
              enterKeyHint="done"
              autoFocus
              maxLength={9}
              value={text}
              onChange={(e) => {
                setText(e.target.value)
                setInvalid(false)
              }}
              onKeyDown={(e) => e.key === 'Escape' && setMode(null)}
              aria-invalid={invalid || undefined}
              aria-describedby={invalid ? `${inputId}-msg` : undefined}
              className="h-11 min-w-0 rounded-xl border border-border bg-surface-2 px-3 text-base"
            />
            {invalid && (
              <p id={`${inputId}-msg`} role="alert" className="text-xs text-danger">
                {t('qty.invalid')}
              </p>
            )}
          </div>
          <button
            type="submit"
            disabled={disabled}
            className="mt-6 h-11 shrink-0 rounded-xl bg-accent-strong px-4 text-sm font-semibold text-on-accent disabled:opacity-60"
          >
            {mode === 'add' ? t('qty.add') : t('qty.save')}
          </button>
        </form>
      )}
    </div>
  )
}
