import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useEffect, useRef } from 'react'
import { useTranslation } from 'react-i18next'
import type { CheckInStatus, Today, TodayHabit, XpResult } from '../../api/types'
import { useToast } from '../../components/ui/toast-context'
import { errorMessage } from '../../lib/errors'
import { expectedCheckInXp } from '../../lib/format'
import { queryKeys } from '../../lib/queryKeys'
import { useApplyXpResult } from '../xp/useApplyXpResult'
import { CHECK_IN_KEY, type CheckInVars } from './checkInMutation'

const counts = (status: CheckInStatus | null | undefined) => (status && status !== 'Missed' ? 1 : 0)

function optimistic(habit: TodayHabit, { date, status, minutes }: CheckInVars): TodayHabit {
  const delta = counts(status) - counts(habit.checkIn?.status)
  // Estimativa até o servidor responder: marcar hoje soma 1 dia na sequência, desfazer tira.
  const current = habit.streak.unit === 'Days' ? Math.max(0, habit.streak.current + delta) : habit.streak.current
  return {
    ...habit,
    weekCount: habit.weekCount + delta,
    streak: { ...habit.streak, current, best: Math.max(habit.streak.best, current) },
    checkIn: status
      ? {
          habitId: habit.id,
          date,
          status,
          xp: expectedCheckInXp(status, habit.level),
          minutes: minutes === undefined ? (habit.checkIn?.minutes ?? null) : minutes > 0 ? minutes : null,
        }
      : null,
  }
}

const UNDO_MS = 6000

const VIBRATION: Record<CheckInStatus, number | number[]> = { Done: 18, Minimum: 12, Missed: [10, 40, 10] }

/** Vibração curta onde houver suporte (Android; iOS e desktop ignoram). */
function haptic(status: CheckInStatus | null) {
  if (!status || typeof navigator === 'undefined' || typeof navigator.vibrate !== 'function') return
  try {
    navigator.vibrate(VIBRATION[status])
  } catch {
    // alguns navegadores bloqueiam sem gesto do usuário; não importa
  }
}

/** Hábitos que contam no dia: os previstos para hoje e os que já têm check-in. */
const dueOf = (habits: TodayHabit[]) => habits.filter((h) => h.isDueToday || h.checkIn !== null)

/**
 * Check-in com atualização otimista na tela Hoje (rollback em caso de erro).
 * Funciona offline: veja os padrões em checkInMutation.ts.
 */
export function useCheckIn() {
  const qc = useQueryClient()
  const toast = useToast()
  const applyXp = useApplyXpResult()
  const { t } = useTranslation('checkins')
  const mutateRef = useRef<((vars: CheckInVars) => void) | null>(null)
  // O "Desfazer" é um check-in como outro qualquer, mas não ganha um novo "Desfazer".
  const silentRef = useRef(false)

  const mutation = useMutation<XpResult, Error, CheckInVars, { previous: Today | undefined }>({
    mutationKey: CHECK_IN_KEY,
    onMutate: async (vars) => {
      const silent = silentRef.current
      silentRef.current = false
      // Feedback no toque (e não no onSuccess): offline a mutação pausa e o onSuccess só roda na volta.
      haptic(vars.status)
      await qc.cancelQueries({ queryKey: queryKeys.today })
      const previous = qc.getQueryData<Today>(queryKeys.today)
      qc.setQueryData<Today>(queryKeys.today, (d) =>
        d ? { ...d, habits: d.habits.map((h) => (h.id === vars.habit.id ? optimistic(h, vars) : h)) } : d,
      )

      // Só trocas de status (não o salvar minutos) ganham toast com "Desfazer".
      if (!silent && vars.minutes === undefined) {
        const before = previous?.habits.find((h) => h.id === vars.habit.id)?.checkIn ?? null
        const after = qc.getQueryData<Today>(queryKeys.today)
        const dueAfter = after ? dueOf(after.habits) : []
        const dueBefore = previous ? dueOf(previous.habits) : []
        const wasComplete = dueBefore.length > 0 && dueBefore.every((h) => h.checkIn !== null)
        // Celebra só na transição para "dia completo", sem modal: um toast (que já entra com animate-pop).
        const dayComplete =
          vars.status !== null && !wasComplete && dueAfter.length > 0 && dueAfter.every((h) => h.checkIn !== null)
        const name = vars.habit.name
        const restore: CheckInVars = {
          habit: vars.habit,
          date: vars.date,
          status: before?.status ?? null,
          ...(before ? { minutes: before.minutes ?? 0 } : {}),
        }
        toast.show({
          message: dayComplete
            ? t('feedback.dayComplete')
            : vars.status === null
              ? t('feedback.removed', { name })
              : t(`feedback.${vars.status}`, { name }),
          variant: dayComplete ? 'achievement' : vars.status === 'Missed' ? 'info' : 'success',
          duration: UNDO_MS,
          action: {
            label: t('feedback.undo'),
            onClick: () => {
              silentRef.current = true
              mutateRef.current?.(restore)
            },
          },
        })
      }
      return { previous }
    },
    onError: (error, _vars, ctx) => {
      if (ctx?.previous) qc.setQueryData(queryKeys.today, ctx.previous)
      toast.error(errorMessage(error))
    },
    onSuccess: (result) => applyXp(result),
  })

  const { mutate } = mutation
  useEffect(() => {
    mutateRef.current = (vars) => mutate(vars)
    return () => {
      mutateRef.current = null
    }
  }, [mutate])

  return mutation
}
