import { QueryClientProvider } from '@tanstack/react-query'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { RouterProvider } from 'react-router'
import { setUnauthorizedHandler } from './api/client'
import { queryClient } from './app/queryClient'
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
    <QueryClientProvider client={queryClient}>
      <ToastProvider>
        <XpFeedbackProvider>
          <RouterProvider router={router} />
        </XpFeedbackProvider>
      </ToastProvider>
    </QueryClientProvider>
  </StrictMode>,
)
