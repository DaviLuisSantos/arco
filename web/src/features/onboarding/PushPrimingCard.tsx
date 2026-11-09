import { useMutation, useQuery } from '@tanstack/react-query'
import { Bell } from 'lucide-react'
import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { achievementsApi, habitsApi } from '../../api/endpoints'
import type { TodayHabit } from '../../api/types'
import { Button } from '../../components/ui/Button'
import { Card } from '../../components/ui/Card'
import { Input } from '../../components/ui/Input'
import { useToast } from '../../components/ui/toast-context'
import { errorMessage } from '../../lib/errors'
import { canPrimePush, enablePush, PushPermissionError } from '../../lib/push'
import { queryKeys } from '../../lib/queryKeys'
import { useInvalidateData } from '../../lib/useInvalidateData'
import { InstallCard } from './InstallCard'

const OFFERED_KEY = 'arco-push-priming-offered'
const DEFAULT_TIME = '08:00'
const TIME_RE = /^([01]\d|2[0-3]):[0-5]\d$/

function readOffered(): boolean {
  try {
    return localStorage.getItem(OFFERED_KEY) === '1'
  } catch {
    return false
  }
}

/**
 * Cartão exibido depois do primeiro check-in. É o único caminho que pede permissão de push fora de
 * Configurações: explica o valor, deixa escolher o horário (hábitos de modelo não têm `reminderTime`)
 * e só então aciona o prompt do navegador. Sem push possível ou já oferecido, cede lugar ao convite
 * de instalação, para nunca haver dois cartões ao mesmo tempo.
 */
export function PushPrimingCard({ habits }: { habits: TodayHabit[] }) {
  const { t } = useTranslation('onboarding', { keyPrefix: 'priming' })
  const { t: tPush } = useTranslation('push')
  const toast = useToast()
  const invalidate = useInvalidateData()
  const achievements = useQuery({ queryKey: queryKeys.achievements, queryFn: achievementsApi.list })
  const [offered, setOffered] = useState(readOffered)
  const [eligible, setEligible] = useState(false)
  const [time, setTime] = useState(DEFAULT_TIME)
  const [timeError, setTimeError] = useState<string | null>(null)

  useEffect(() => {
    let alive = true
    void canPrimePush().then((ok) => alive && setEligible(ok))
    return () => {
      alive = false
    }
  }, [])

  const target = habits.find((h) => h.checkIn !== null && h.reminderTime === null)
  const firstCheckInDone =
    habits.some((h) => h.checkIn !== null) &&
    (achievements.data?.some((a) => a.key === 'checkins-1' && a.unlockedAt !== null) ?? false)

  function markOffered() {
    try {
      localStorage.setItem(OFFERED_KEY, '1')
    } catch {
      // sem storage: some só nesta sessão
    }
    setOffered(true)
  }

  const enable = useMutation({
    mutationFn: async (habit: TodayHabit) => {
      await enablePush()
      await habitsApi.update(habit.id, {
        name: habit.name,
        emoji: habit.emoji,
        goalId: habit.goalId,
        frequencyType: habit.frequencyType,
        frequencyDays: habit.frequencyDays,
        timesPerWeek: habit.timesPerWeek,
        levelDescription: habit.levelDescription,
        minimumDescription: habit.minimumDescription,
        reminderTime: time,
        plannedLevels: habit.plannedLevels.map((l) => ({
          description: l.description,
          minimumDescription: l.minimumDescription,
        })),
        levelUpWeeks: habit.levelUpWeeks,
        levelUpDaysPerWeek: habit.levelUpDaysPerWeek,
      })
    },
    onSuccess: () => toast.success(t('saved', { time })),
    onError: (err) => toast.error(err instanceof PushPermissionError ? tPush('state.denied') : errorMessage(err)),
    onSettled: () => {
      markOffered()
      void invalidate()
    },
  })

  if (!firstCheckInDone) return null
  if (offered || !eligible || !target) return <InstallCard />

  function submit() {
    if (!target) return
    if (!TIME_RE.test(time)) {
      setTimeError(t('timeInvalid'))
      return
    }
    setTimeError(null)
    enable.mutate(target)
  }

  return (
    <Card className="flex flex-col gap-3 p-4!">
      <div className="flex items-start gap-3">
        <span className="grid size-10 shrink-0 place-items-center rounded-xl bg-surface-2 text-accent">
          <Bell className="size-5" aria-hidden />
        </span>
        <div>
          <h2 className="text-sm font-semibold">{t('title')}</h2>
          <p className="mt-1 text-sm text-muted">
            {target.emoji ? `${target.emoji} ` : ''}
            {t('description', { habit: target.name })}
          </p>
        </div>
      </div>
      <Input
        label={t('time')}
        type="time"
        value={time}
        onChange={(e) => setTime(e.target.value)}
        error={timeError}
        className="max-w-40"
      />
      <div className="flex justify-end gap-2">
        <Button variant="ghost" size="sm" onClick={markOffered} disabled={enable.isPending}>
          {t('later')}
        </Button>
        <Button size="sm" onClick={submit} loading={enable.isPending}>
          {t('enable')}
        </Button>
      </div>
    </Card>
  )
}
