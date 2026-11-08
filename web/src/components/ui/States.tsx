import { CircleAlert, CloudOff, LoaderCircle } from 'lucide-react'
import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { errorMessage } from '../../lib/errors'
import { useOnline } from '../../lib/useOnline'
import { Button } from './Button'

export function Spinner({ label }: { label?: string }) {
  const { t } = useTranslation('layout')
  // Offline, uma tela sem dados salvos no aparelho fica esperando a conexão.
  if (!useOnline()) {
    return (
      <div role="status" className="flex flex-col items-center gap-3 py-16 text-center">
        <CloudOff className="size-8 text-muted" aria-hidden />
        <p className="max-w-xs text-sm text-muted">
          {t('offlineNoData')}
        </p>
      </div>
    )
  }
  return (
    <div role="status" className="flex items-center justify-center gap-2 py-16 text-muted">
      <LoaderCircle className="size-5 animate-spin" aria-hidden />
      <span className="text-sm">{label ?? t('loading')}</span>
    </div>
  )
}

export function ErrorState({ error, onRetry }: { error: unknown; onRetry?: () => void }) {
  const { t } = useTranslation('layout')
  return (
    <div role="alert" className="flex flex-col items-center gap-3 py-16 text-center">
      <CircleAlert className="size-8 text-danger" aria-hidden />
      <p className="max-w-xs text-sm text-muted">{errorMessage(error)}</p>
      {onRetry && (
        <Button variant="secondary" size="sm" onClick={onRetry}>
          {t('retry')}
        </Button>
      )}
    </div>
  )
}

interface EmptyStateProps {
  icon: ReactNode
  title: string
  description?: string
  action?: ReactNode
}

export function EmptyState({ icon, title, description, action }: EmptyStateProps) {
  return (
    <div className="flex flex-col items-center gap-3 rounded-2xl border border-dashed border-border px-6 py-10 text-center">
      <div className="grid size-12 place-items-center rounded-full bg-surface-2 text-accent">{icon}</div>
      <div>
        <p className="font-semibold">{title}</p>
        {description && <p className="mt-1 text-sm text-muted">{description}</p>}
      </div>
      {action}
    </div>
  )
}
