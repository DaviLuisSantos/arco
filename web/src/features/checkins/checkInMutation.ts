import type { QueryClient } from '@tanstack/react-query'
import { ApiError } from '../../api/client'
import { checkInsApi } from '../../api/endpoints'
import type { CheckInStatus, TodayHabit, XpResult } from '../../api/types'

export const CHECK_IN_KEY = ['checkin'] as const

export interface CheckInVars {
  habit: TodayHabit
  date: string
  /** null desfaz o check-in (DELETE). */
  status: CheckInStatus | null
  /** Minutos manuais do dia. Ausente = não mexe; 0 = remove. Serializável na fila offline. */
  minutes?: number
}

/**
 * Padrões do check-in, registrados no QueryClient (e não no hook) para valerem também
 * para check-ins feitos offline e retomados depois de reabrir o app:
 * - networkMode 'online': sem rede, a mutação pausa e espera a conexão voltar;
 * - scope: os check-ins vão um de cada vez, na ordem em que foram feitos;
 * - o servidor recalcula o XP oficial e as telas são revalidadas no fim da fila.
 */
export function registerCheckInDefaults(qc: QueryClient) {
  qc.setMutationDefaults(CHECK_IN_KEY, {
    mutationFn: ({ habit, date, status, minutes }: CheckInVars): Promise<XpResult> =>
      status
        ? checkInsApi.upsert({ habitId: habit.id, date, status, ...(minutes !== undefined && { minutes }) })
        : checkInsApi.remove(habit.id, date),
    networkMode: 'online',
    scope: { id: 'checkin' },
    // Rede instável com o aparelho "online": tenta mais algumas vezes antes de desistir.
    retry: (failureCount, error) => error instanceof ApiError && error.code === 'network' && failureCount < 3,
    onSettled: () => {
      // Só revalida quando for o último check-in da fila, para não sobrescrever
      // o estado otimista dos que ainda vão ser enviados.
      if (qc.isMutating({ mutationKey: CHECK_IN_KEY }) === 1) {
        void qc.invalidateQueries({ predicate: (q) => q.queryKey[0] !== 'auth' })
      }
    },
  })
}
