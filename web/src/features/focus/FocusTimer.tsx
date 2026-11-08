import { Check, RotateCw, Timer, X } from 'lucide-react'
import { useState } from 'react'
import { Trans, useTranslation } from 'react-i18next'
import { checkInsApi } from '../../api/endpoints'
import { Button } from '../../components/ui/Button'
import { ConfirmDialog } from '../../components/ui/ConfirmDialog'
import { formatDuration } from '../../lib/format'
import { habitColor } from '../../lib/habitColor'
import { useAppMutation } from '../../lib/useAppMutation'
import { useFocusTimer } from './useFocusTimer'

const PRESETS = [15, 25, 45, 60]

/** Tijolo que enche de baixo para cima conforme o tempo passa (fill 0–1). */
function Brick({ color, fill, size, label }: { color: string; fill: number; size: number; label?: string }) {
  const f = Math.min(1, Math.max(0, fill))
  const h = 28
  return (
    <svg
      width={size}
      height={(size * 40) / 64}
      viewBox="0 0 64 40"
      role={label ? 'img' : undefined}
      aria-label={label}
      aria-hidden={label ? undefined : true}
    >
      <rect x="2" y="6" width="60" height="28" rx="4" fill="none" stroke={color} strokeWidth="2" opacity="0.5" />
      <rect x="4" y={6 + 2 + (h - 4) * (1 - f)} width="56" height={(h - 4) * f} rx="2" fill={color} />
    </svg>
  )
}

function clock(totalSeconds: number): string {
  const h = Math.floor(totalSeconds / 3600)
  const m = Math.floor((totalSeconds % 3600) / 60)
  const s = totalSeconds % 60
  const pad = (n: number) => String(n).padStart(2, '0')
  return h > 0 ? `${h}:${pad(m)}:${pad(s)}` : `${pad(m)}:${pad(s)}`
}

interface Props {
  habitId: number
  emoji: string | null
  name: string
}

export function FocusTimer({ habitId, emoji, name }: Props) {
  const { t } = useTranslation(['focus', 'common'])
  const timer = useFocusTimer(habitId)
  const [confirmCancel, setConfirmCancel] = useState(false)
  const color = habitColor(habitId)

  const markDone = useAppMutation(
    (date: string) => checkInsApi.upsert({ habitId, date, status: 'Done' }),
    { successMessage: t('markedDone'), onSuccess: timer.dismissSaved },
  )

  const { running, pending, saved } = timer

  if (running) {
    const shown = timer.remaining ?? timer.elapsed
    const target = running.targetMinutes
    const fill = target ? timer.elapsed / (target * 60) : (timer.elapsed % 3600) / 3600
    return (
      <div className="flex flex-col items-center gap-3 text-center">
        <Brick color={color} fill={fill} size={96} label={t('brickFilling', { name })} />
        <p
          className="text-4xl font-extrabold tabular-nums tracking-tight"
          role="timer"
          aria-label={timer.remaining !== null ? t('remaining') : t('elapsed')}
        >
          {clock(shown)}
        </p>
        <p className="text-sm text-muted">
          {running.targetMinutes !== null
            ? t('focusOf', { duration: formatDuration(running.targetMinutes) })
            : t('freeTimerHint')}
        </p>
        <div className="flex w-full gap-2">
          <Button variant="ghost" block onClick={() => setConfirmCancel(true)}>
            <X className="size-4" aria-hidden />
            {t('discard')}
          </Button>
          <Button block loading={timer.saving} onClick={timer.finish}>
            <Check className="size-4" aria-hidden />
            {t('finishNow')}
          </Button>
        </div>
        <ConfirmDialog
          open={confirmCancel}
          onClose={() => setConfirmCancel(false)}
          onConfirm={() => {
            setConfirmCancel(false)
            timer.cancel()
          }}
          title={t('discardTitle')}
          description={t('discardDescription')}
          confirmLabel={t('discard')}
          danger
        />
      </div>
    )
  }

  if (pending) {
    return (
      <div className="flex flex-col gap-3">
        <p className="text-sm">
          <Trans
            ns="focus"
            i18nKey="pendingText"
            values={{ duration: formatDuration(pending.minutes) }}
            components={{ b: <span className="font-semibold" /> }}
          />
        </p>
        <div className="flex gap-2">
          <Button variant="ghost" block disabled={timer.saving} onClick={timer.discardPending}>
            {t('discard')}
          </Button>
          <Button block loading={timer.saving} onClick={timer.retry}>
            <RotateCw className="size-4" aria-hidden />
            {t('common:retry')}
          </Button>
        </div>
      </div>
    )
  }

  return (
    <div className="flex flex-col gap-3">
      {saved && (
        <div className="flex items-center gap-3 rounded-xl bg-accent/10 p-3">
          <Brick color={color} fill={1} size={48} />
          <div className="min-w-0 flex-1">
            <p className="text-sm font-semibold">
              {emoji ? `${emoji} ` : ''}
              {t('savedFocus', { duration: formatDuration(saved.minutes) })}
            </p>
            <p className="text-xs text-muted">{t('newBrick')}</p>
          </div>
          <div className="flex shrink-0 gap-1">
            <Button size="sm" loading={markDone.isPending} onClick={() => markDone.mutate(saved.date)}>
              {t('markDone')}
            </Button>
            <Button size="sm" variant="ghost" onClick={timer.dismissSaved} aria-label={t('dismiss')}>
              <X className="size-4" aria-hidden />
            </Button>
          </div>
        </div>
      )}
      <div className="grid grid-cols-4 gap-2" role="group" aria-label={t('durationGroup')}>
        {PRESETS.map((m) => (
          <Button key={m} variant="secondary" size="sm" onClick={() => timer.start(m)}>
            {t('presetMinutes', { count: m })}
          </Button>
        ))}
      </div>
      <Button variant="ghost" size="sm" onClick={() => timer.start(null)}>
        <Timer className="size-4" aria-hidden />
        {t('freeTimer')}
      </Button>
    </div>
  )
}
