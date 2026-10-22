import { PersistQueryClientProvider } from '@tanstack/react-query-persist-client'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { RouterProvider } from 'react-router'
import { setUnauthorizedHandler } from './api/client'
import { persistOptions, queryClient } from './app/queryClient'
import { router } from './app/router'
import { ToastProvider } from './components/ui/Toast'
import { XpFeedbackProvider } from './features/xp/XpFeedbackProvider'
import './index.css'
import { queryKeys } from './lib/queryKeys'

// 401 em qualquer rota protegida: sessão acabou → limpa o cache e volta ao login.
setUnauthorizedHandler(() => {
  queryClient.setQueryData(queryKeys.auth, false)
  queryClient.removeQueries({ predicate: (q) => q.queryKey[0] !== 'auth' })
  if (window.location.pathname !== '/login') void router.navigate('/login', { replace: true })
})

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <PersistQueryClientProvider
      client={queryClient}
      persistOptions={persistOptions}
      // Check-ins feitos offline antes de fechar o app: envia assim que der.
      onSuccess={() => void queryClient.resumePausedMutations()}
    >
      <ToastProvider>
        <XpFeedbackProvider>
          <RouterProvider router={router} />
        </XpFeedbackProvider>
      </ToastProvider>
    </PersistQueryClientProvider>
  </StrictMode>,
)
