import './app/sentry'
import { PersistQueryClientProvider } from '@tanstack/react-query-persist-client'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { RouterProvider } from 'react-router'
import { setUnauthorizedHandler } from './api/client'
import { persistOptions, queryClient } from './app/queryClient'
import { router } from './app/router'
import { ToastProvider } from './components/ui/Toast'
import { XpFeedbackProvider } from './features/xp/XpFeedbackProvider'
import './i18n'
import './index.css'
import { initPwaInstall } from './lib/pwaInstall'
import { queryKeys } from './lib/queryKeys'

// O convite de instalação do PWA dispara cedo: captura já no boot.
initPwaInstall()

// 401 em qualquer rota protegida: sessão acabou → limpa o cache e volta ao login.
setUnauthorizedHandler(() => {
  queryClient.setQueryData(queryKeys.auth, null)
  queryClient.removeQueries({ predicate: (q) => q.queryKey[0] !== 'auth' })
  if (window.location.pathname !== '/login') void router.navigate('/login', { replace: true })
})

// Chunk de rota antigo após um deploy: recarrega uma vez (com trava contra laço) para pegar o build novo.
window.addEventListener('vite:preloadError', (event) => {
  const key = 'arco-chunk-reload'
  try {
    const last = Number(sessionStorage.getItem(key) ?? 0)
    if (Date.now() - last < 30_000) return // já recarregou há pouco: deixa o errorElement mostrar
    sessionStorage.setItem(key, String(Date.now()))
  } catch {
    return
  }
  event.preventDefault()
  window.location.reload()
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
