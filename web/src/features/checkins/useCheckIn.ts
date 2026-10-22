import { useMutation, useQueryClient } from '@tanstack/react-query'
import { checkInsApi } from '../../api/endpoints'
import type { CheckInStatus, Today, TodayHabit, XpResult } from '../../api/types'
import { useToast } from '../../components/ui/toast-context'
import { errorMessage } from '../../lib/errors'
import { expectedCheckInXp } from '../../lib/format'
import { queryKeys } from '../../lib/queryKeys'
import { useInvalidateData } from '../../lib/useInvalidateData'
import { useApplyXpResult } from '../xp/useApplyXpResult'

const MUTATION_KEY = ['checkin'] as const

export interface CheckInVars {
  habit: TodayHabit
  date: string
  /** null desfaz o check-in (DELETE). */
  status: CheckInStatus | null
}

const counts = (status: CheckInStatus | null | undefined) => (status && status !== 'Missed' ? 1 : 0)

function optimistic(habit: TodayHabit, { date, status }: CheckInVars): TodayHabit {
  return {
    ...habit,
    weekCount: habit.weekCount - counts(habit.checkIn?.status) + counts(status),
    checkIn: status ? { habitId: habit.id, date, status, xp: expectedCheckInXp(status, habit.level) } : null,
  }
}

/** Check-in com atualização otimista na tela Hoje (rollback em caso de erro). */
export function useCheckIn() {
  const qc = useQueryClient()
  const toast = useToast()
  const applyXp = useApplyXpResult()
  const invalidate = useInvalidateData()

  return useMutation({
    mutationKey: MUTATION_KEY,
    mutationFn: ({ habit, date, status }: CheckInVars): Promise<XpResult> =>
      status ? checkInsApi.upsert({ habitId: habit.id, date, status }) : checkInsApi.remove(habit.id, date),
    onMutate: async (vars) => {
      await qc.cancelQueries({ queryKey: queryKeys.today })
      const previous = qc.getQueryData<Today>(queryKeys.today)
      qc.setQueryData<Today>(queryKeys.today, (d) =>
        d ? { ...d, habits: d.habits.map((h) => (h.id === vars.habit.id ? optimistic(h, vars) : h)) } : d,
      )
      return { previous }
    },
    onError: (error, _vars, ctx) => {
      if (ctx?.previous) qc.setQueryData(queryKeys.today, ctx.previous)
      toast.error(errorMessage(error))
    },
    onSuccess: (result) => applyXp(result),
    onSettled: () => {
      // Só revalida quando for o último check-in em andamento, para não sobrescrever
      // o estado otimista de outros toques ainda pendentes.
      if (qc.isMutating({ mutationKey: MUTATION_KEY }) === 1) void invalidate()
    },
  })
}
