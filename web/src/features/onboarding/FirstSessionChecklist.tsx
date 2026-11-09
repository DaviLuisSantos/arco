import { useQuery } from '@tanstack/react-query'
import { Check, X } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { achievementsApi } from '../../api/endpoints'
import type { TodayHabit } from '../../api/types'
import { Card } from '../../components/ui/Card'
import { cn } from '../../lib/cn'
import { queryKeys } from '../../lib/queryKeys'

const DISMISSED_KEY = 'arco-first-session-dismissed'

function readDismissed(): boolean {
  try {
    return localStorage.getItem(DISMISSED_KEY) === '1'
  } catch {
    return false
  }
}

/**
 * Checklist da primeira sessão: aparece com pelo menos um hábito e some sozinho assim que o primeiro
 * check-in da conta existe (conquista `checkins-1`), ou quando a pessoa dispensa.
 */
export function FirstSessionChecklist({ habits }: { habits: TodayHabit[] }) {
  const { t } = useTranslation('onboarding', { keyPrefix: 'checklist' })
  const achievements = useQuery({ queryKey: queryKeys.achievements, queryFn: achievementsApi.list })
  const [dismissed, setDismissed] = useState(readDismissed)

  if (dismissed || habits.length === 0 || !achievements.data) return null
  if (achievements.data.some((a) => a.key === 'checkins-1' && a.unlockedAt !== null)) return null

  const steps = [
    { id: 'habit', done: true },
    { id: 'checkIn', done: habits.some((h) => h.checkIn !== null) },
    { id: 'reminder', done: habits.some((h) => h.reminderTime !== null) },
  ] as const
  const doneCount = steps.filter((s) => s.done).length

  function dismiss() {
    try {
      localStorage.setItem(DISMISSED_KEY, '1')
    } catch {
      // sem storage: some só nesta sessão
    }
    setDismissed(true)
  }

  return (
    <Card className="p-4!">
      <div className="flex items-start justify-between gap-3">
        <div>
          <h2 className="text-sm font-semibold">{t('title')}</h2>
          <p className="text-xs text-muted">{t('progress', { done: doneCount, total: steps.length })}</p>
        </div>
        <button
          type="button"
          onClick={dismiss}
          aria-label={t('dismiss')}
          className="-m-2 grid size-11 place-items-center rounded-xl text-muted hover:text-text"
        >
          <X className="size-4" aria-hidden />
        </button>
      </div>
      <ol className="mt-3 flex flex-col gap-2">
        {steps.map((s) => (
          <li key={s.id} className="flex items-center gap-3 text-sm">
            <span
              aria-hidden
              className={cn(
                'grid size-5 shrink-0 place-items-center rounded-full border',
                s.done ? 'border-transparent bg-accent-strong text-on-accent' : 'border-border',
              )}
            >
              {s.done && <Check className="size-3" />}
            </span>
            <span className={cn(s.done && 'text-muted line-through')}>{t(s.id)}</span>
          </li>
        ))}
      </ol>
    </Card>
  )
}
