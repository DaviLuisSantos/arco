import { useMutation } from '@tanstack/react-query'
import type { XpResult } from '../api/types'
import { useToast } from '../components/ui/toast-context'
import { useApplyXpResult } from '../features/xp/useApplyXpResult'
import { errorMessage } from './errors'
import { useInvalidateData } from './useInvalidateData'

function isXpResult(value: unknown): value is XpResult {
  return typeof value === 'object' && value !== null && 'xpDelta' in value && 'player' in value
}

interface Options<TData, TVars> {
  onSuccess?: (data: TData, vars: TVars) => void
  successMessage?: string
}

/**
 * useMutation com o comportamento padrão do app: mostra erros em toast,
 * aplica XP/nível quando a resposta traz { xpDelta, player } e revalida os dados.
 */
export function useAppMutation<TData, TVars = void>(
  mutationFn: (vars: TVars) => Promise<TData>,
  { onSuccess, successMessage }: Options<TData, TVars> = {},
) {
  const toast = useToast()
  const applyXp = useApplyXpResult()
  const invalidate = useInvalidateData()

  return useMutation({
    mutationFn,
    onSuccess: (data, vars) => {
      if (isXpResult(data)) applyXp(data)
      if (successMessage) toast.success(successMessage)
      onSuccess?.(data, vars)
    },
    onError: (error) => toast.error(errorMessage(error)),
    onSettled: () => invalidate(),
  })
}
