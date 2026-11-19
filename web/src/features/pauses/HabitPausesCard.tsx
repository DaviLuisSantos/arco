import { Pause } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { pausesApi } from '../../api/endpoints'
import type { HabitPause } from '../../api/types'
import { Button } from '../../components/ui/Button'
import { Card } from '../../components/ui/Card'
import { formatFullDate } from '../../lib/dates'
import { useAppMutation } from '../../lib/useAppMutation'
import { PauseHabitDialog } from './PauseHabitDialog'

interface HabitPausesCardProps {
  habitId: number
  pauses: HabitPause[]
  today: string
}

export function HabitPausesCard({ habitId, pauses, today }: HabitPausesCardProps) {
  const { t } = useTranslation('pauses', { keyPrefix: 'card' })
  const [open, setOpen] = useState(false)
  const resume = useAppMutation(() => pausesApi.resume(habitId), { successMessage: t('resumed') })
  const remove = useAppMutation((pauseId: number) => pausesApi.remove(habitId, pauseId), { successMessage: t('removed') })

  return (
    <section aria-labelledby="pauses-title">
      <h2 id="pauses-title" className="mb-2 text-sm font-semibold uppercase tracking-wide text-muted">
        {t('title')}
      </h2>
      <Card className="flex flex-col gap-3">
        {pauses.length === 0 ? (
          <p className="text-sm text-muted">{t('empty')}</p>
        ) : (
          <ul className="flex flex-col gap-2">
            {pauses.map((p) => {
              const current = p.startDate <= today && (p.endDate === null || p.endDate >= today)
              const start = formatFullDate(p.startDate)
              return (
                <li key={p.id} className="flex items-center gap-3 text-sm">
                  <div className="min-w-0 flex-1">
                    <p className="font-medium">
                      {p.endDate ? t('range', { start, end: formatFullDate(p.endDate) }) : t('openEnded', { start })}
                    </p>
                    <p className="text-xs text-muted">
                      {current ? t('current') : t('future')}
                      {p.reason && ` · ${p.reason}`}
                    </p>
                  </div>
                  {current ? (
                    <Button variant="secondary" size="sm" loading={resume.isPending} onClick={() => resume.mutate()}>
                      {t('resume')}
                    </Button>
                  ) : (
                    <Button
                      variant="ghost"
                      size="sm"
                      loading={remove.isPending && remove.variables === p.id}
                      aria-label={t('removeAria', { start })}
                      onClick={() => remove.mutate(p.id)}
                    >
                      {t('remove')}
                    </Button>
                  )}
                </li>
              )
            })}
          </ul>
        )}
        <Button variant="secondary" onClick={() => setOpen(true)}>
          <Pause className="size-4" aria-hidden />
          {t('pauseButton')}
        </Button>
      </Card>
      <PauseHabitDialog open={open} onClose={() => setOpen(false)} habitId={habitId} today={today} />
    </section>
  )
}
