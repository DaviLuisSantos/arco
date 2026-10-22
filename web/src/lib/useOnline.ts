import { onlineManager } from '@tanstack/react-query'
import { useSyncExternalStore } from 'react'

/** Conexão do aparelho, pela mesma fonte que o TanStack Query usa para pausar requisições. */
export function useOnline(): boolean {
  return useSyncExternalStore(
    (onChange) => onlineManager.subscribe(onChange),
    () => onlineManager.isOnline(),
  )
}
