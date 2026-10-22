import { useMutation, useQueryClient } from '@tanstack/react-query'
import type { CheckInStatus, Today, TodayHabit, XpResult } from '../../api/types'
import { useToast } from '../../components/ui/toast-context'
import { errorMessage } from '../../lib/errors'
import { expectedCheckInXp } from '../../lib/format'
import { queryKeys } from '../../lib/queryKeys'
import { useApplyXpResult } from '../xp/useApplyXpResult'
import { CHECK_IN_KEY, type CheckInVars } from './checkInMutation'

const counts = (status: CheckInStatus | null | undefined) => (status && status !== 'Missed' ? 1 : 0)

function optimistic(habit: TodayHabit, { date, status }: CheckInVars): TodayHabit {
  return {
    ...habit,
    weekCount: habit.weekCount - counts(habit.checkIn?.status) + counts(status),
    checkIn: status ? { habitId: habit.id, date, status, xp: expectedCheckInXp(status, habit.level) } : null,
  }
}

/**
 * Check-in com atualização otimista na tela Hoje (rollback em caso de erro).
 * Funciona offline: veja os padrões em checkInMutation.ts.
 */
export function useCheckIn() {
  const qc = useQueryClient()
  const toast = useToast()
  const applyXp = useApplyXpResult()

  return useMutation<XpResult, Error, CheckInVars, { previous: Today | undefined }>({
    mutationKey: CHECK_IN_KEY,
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
  })
}
