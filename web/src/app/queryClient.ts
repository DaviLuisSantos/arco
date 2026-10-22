import { createAsyncStoragePersister } from '@tanstack/query-async-storage-persister'
import { QueryClient } from '@tanstack/react-query'
import type { PersistQueryClientOptions } from '@tanstack/react-query-persist-client'
import { ApiError } from '../api/client'
import { registerCheckInDefaults } from '../features/checkins/checkInMutation'
import { idbStorage } from '../lib/idbStorage'

/** Quanto tempo os dados salvos no aparelho valem (a API aceita check-in de até 7 dias atrás). */
const MAX_AGE = 7 * 24 * 60 * 60 * 1000

export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      // Mantém em memória tudo que está salvo no aparelho, para não sumir do cache persistido.
      gcTime: MAX_AGE,
      // Não insiste em erros do cliente (4xx); tenta de novo em falhas de rede/servidor.
      retry: (failureCount, error) => {
        if (error instanceof ApiError && error.status >= 400 && error.status < 500) return false
        return failureCount < 2
      },
    },
    // Só o check-in espera a conexão voltar; as demais ações falham na hora com aviso.
    mutations: { retry: false, networkMode: 'always' },
  },
})

registerCheckInDefaults(queryClient)

// Cache do app no IndexedDB: as telas abrem offline com a última resposta, e os
// check-ins pausados (sem rede) sobrevivem a fechar o app. O service worker não cacheia /api.
export const persistOptions: Omit<PersistQueryClientOptions, 'queryClient'> = {
  persister: createAsyncStoragePersister({ storage: idbStorage, key: 'arco-query-cache', throttleTime: 1000 }),
  maxAge: MAX_AGE,
  // Trocar quando o formato das respostas mudar de um jeito incompatível.
  buster: 'v2',
}
