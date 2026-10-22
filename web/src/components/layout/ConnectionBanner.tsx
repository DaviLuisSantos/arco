import { useIsMutating, useQueryClient } from '@tanstack/react-query'
import { CloudOff, RefreshCw } from 'lucide-react'
import { useSyncExternalStore } from 'react'
import { CHECK_IN_KEY } from '../../features/checkins/checkInMutation'
import { useOnline } from '../../lib/useOnline'

const time = new Intl.DateTimeFormat('pt-BR', { hour: '2-digit', minute: '2-digit' })
const dayAndTime = new Intl.DateTimeFormat('pt-BR', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' })

/** Momento da resposta mais antiga entre as telas abertas (0 = nenhuma). */
function useOldestShownData(): number {
  const cache = useQueryClient().getQueryCache()
  return useSyncExternalStore(
    (onChange) => cache.subscribe(onChange),
    () => {
      let oldest = 0
      for (const q of cache.getAll()) {
        if (q.queryKey[0] === 'auth' || q.getObserversCount() === 0 || !q.state.dataUpdatedAt) continue
        if (!oldest || q.state.dataUpdatedAt < oldest) oldest = q.state.dataUpdatedAt
      }
      return oldest
    },
  )
}

function formatShown(at: number): string {
  const d = new Date(at)
  return d.toDateString() === new Date().toDateString() ? time.format(d) : dayAndTime.format(d).replace(',', '')
}

/** Aviso global de "Offline" (com a idade dos dados na tela) e de "Sincronizando…". */
export function ConnectionBanner() {
  const online = useOnline()
  const queued = useIsMutating({ mutationKey: CHECK_IN_KEY })
  const shownAt = useOldestShownData()

  if (online && queued === 0) return null

  if (online) {
    return (
      <div role="status" className="mb-4 flex items-center gap-2 rounded-xl bg-surface-2 px-3 py-2 text-sm text-muted">
        <RefreshCw className="size-4 shrink-0 animate-spin" aria-hidden />
        Sincronizando…
      </div>
    )
  }

  return (
    <div role="status" className="mb-4 flex items-start gap-2 rounded-xl border border-border bg-surface-2 px-3 py-2 text-sm">
      <CloudOff className="mt-0.5 size-4 shrink-0 text-muted" aria-hidden />
      <div className="min-w-0">
        <p className="font-medium">
          Offline{shownAt ? ` — dados de ${formatShown(shownAt)}` : ''}
          {queued > 0 && <span className="text-muted"> · {queued} check-in{queued > 1 ? 's' : ''} na fila</span>}
        </p>
        <p className="text-muted">Os check-ins são enviados quando a conexão voltar. O resto precisa de internet.</p>
      </div>
    </div>
  )
}
