import { useQuery } from '@tanstack/react-query'
import { Check, X } from 'lucide-react'
import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { achievementsApi } from '../../api/endpoints'
import type { TodayHabit } from '../../api/types'
import { Card } from '../../components/ui/Card'
import { cn } from '../../lib/cn'
import { queryKeys } from '../../lib/queryKeys'
import { PushPrimingCard } from './PushPrimingCard'

const DISMISSED_KEY = 'arco-first-session-dismissed'
const SEEN_KEY = 'arco-first-session-seen'

function readFlag(key: string): boolean {
  try {
    return localStorage.getItem(key) === '1'
  } catch {
    return false
  }
}

function writeFlag(key: string) {
  try {
    localStorage.setItem(key, '1')
  } catch {
    // sem storage: vale só nesta sessão
  }
}

function useChecklist(habits: TodayHabit[]) {
  const achievements = useQuery({ queryKey: queryKeys.achievements, queryFn: achievementsApi.list })
  const [dismissed, setDismissed] = useState(() => readFlag(DISMISSED_KEY))

  const loaded = achievements.data !== undefined
  const unlocked = achievements.data?.some((a) => a.key === 'checkins-1' && a.unlockedAt !== null) ?? false
  const steps = [
    { id: 'habit', done: true },
    { id: 'checkIn', done: habits.some((h) => h.checkIn?.status === 'Done' || h.checkIn?.status === 'Minimum') },
    { id: 'reminder', done: habits.some((h) => h.reminderTime !== null) },
  ] as const
  const complete = steps.every((s) => s.done)
  // Conta antiga: já tem o 1º check-in e nunca viu o checklist neste dispositivo.
  const legacy = unlocked && !readFlag(SEEN_KEY)
  const visible = loaded && !dismissed && !complete && !legacy && habits.length > 0

  useEffect(() => {
    if (visible) writeFlag(SEEN_KEY)
  }, [visible])

  function dismiss() {
    writeFlag(DISMISSED_KEY)
    setDismissed(true)
  }

  return { visible, steps, dismiss }
}

/** Um único slot de onboarding em Hoje: o checklist tem prioridade sobre priming/instalação. */
export function OnboardingSlot({ habits }: { habits: TodayHabit[] }) {
  const checklist = useChecklist(habits)
  if (checklist.visible) return <FirstSessionChecklist steps={checklist.steps} onDismiss={checklist.dismiss} />
  return <PushPrimingCard habits={habits} />
}

type Step = { id: 'habit' | 'checkIn' | 'reminder'; done: boolean }

/**
 * Checklist da primeira sessão: mostra o progresso até os 3 passos serem concluídos ou a pessoa dispensar.
 */
function FirstSessionChecklist({ steps, onDismiss }: { steps: readonly Step[]; onDismiss: () => void }) {
  const { t } = useTranslation('onboarding', { keyPrefix: 'checklist' })
  const doneCount = steps.filter((s) => s.done).length

  return (
    <Card className="p-4!">
      <div className="flex items-start justify-between gap-3">
        <div>
          <h2 className="text-sm font-semibold">{t('title')}</h2>
          <p className="text-xs text-muted">{t('progress', { done: doneCount, total: steps.length })}</p>
        </div>
        <button
          type="button"
          onClick={onDismiss}
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
