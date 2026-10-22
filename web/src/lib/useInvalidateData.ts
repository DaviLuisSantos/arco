import { useQueryClient } from '@tanstack/react-query'
import { useCallback } from 'react'

/**
 * App de um usuário só, com poucos dados: depois de uma mutação, invalidar tudo
 * (menos a sessão) é simples e mantém as telas coerentes entre si.
 */
export function useInvalidateData() {
  const qc = useQueryClient()
  return useCallback(
    () => qc.invalidateQueries({ predicate: (q) => q.queryKey[0] !== 'auth' }),
    [qc],
  )
}
