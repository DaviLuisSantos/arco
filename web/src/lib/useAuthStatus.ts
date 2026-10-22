import { useQuery } from '@tanstack/react-query'
import { authApi } from '../api/endpoints'
import { queryKeys } from './queryKeys'

/** Sessão atual: true/false a partir de GET /api/auth/me. */
export function useAuthStatus() {
  return useQuery({ queryKey: queryKeys.auth, queryFn: authApi.me, staleTime: Infinity })
}
