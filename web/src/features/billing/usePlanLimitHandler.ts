import { useTranslation } from 'react-i18next'
import { useToast } from '../../components/ui/toast-context'
import { isPlanLimit } from './isPlanLimit'

/**
 * Hook para os `onError` das mutações: se o erro for `plan_limit`, mostra o aviso e devolve true
 * (quem chama não precisa mostrar a mensagem genérica); senão devolve false.
 */
export function usePlanLimitHandler(): (err: unknown) => boolean {
  const toast = useToast()
  const { t } = useTranslation('billing')
  return (err) => {
    if (!isPlanLimit(err)) return false
    toast.error(t('limitMessage'))
    return true
  }
}
