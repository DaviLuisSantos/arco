import { useMutation, useQuery } from '@tanstack/react-query'
import { Check, Copy, KeyRound, Plus, Trash2 } from 'lucide-react'
import { useId, useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { apiKeysApi } from '../../api/endpoints'
import type { ApiKey, CreatedApiKey } from '../../api/types'
import { Button } from '../../components/ui/Button'
import { Card } from '../../components/ui/Card'
import { ConfirmDialog } from '../../components/ui/ConfirmDialog'
import { Dialog } from '../../components/ui/Dialog'
import { Input } from '../../components/ui/Input'
import { formatInstant } from '../../lib/dates'
import { useAppMutation } from '../../lib/useAppMutation'
import { useInvalidateData } from '../../lib/useInvalidateData'
import { errorMessage } from '../../lib/errors'
import { useToast } from '../../components/ui/toast-context'
import { usePlanLimitHandler } from '../billing'

const KEY = ['api-keys'] as const

/** Chaves para usar a API sem o app (scripts, automações, assistentes). */
export function ApiKeysCard() {
  const { t } = useTranslation('apikeys')
  const keys = useQuery({ queryKey: KEY, queryFn: apiKeysApi.list })
  const [creating, setCreating] = useState(false)
  const [revoking, setRevoking] = useState<ApiKey | null>(null)
  const revoke = useAppMutation((id: number) => apiKeysApi.revoke(id), {
    successMessage: t('revoked'),
    onSuccess: () => setRevoking(null),
  })

  return (
    <Card className="flex flex-col gap-3">
      <div className="flex items-start gap-3">
        <KeyRound className="mt-0.5 size-5 shrink-0 text-accent" aria-hidden />
        <div className="min-w-0">
          <p className="font-semibold">{t('title')}</p>
          <p className="text-sm text-muted">
            {t('descBefore')}{' '}
            <code className="rounded bg-surface-2 px-1 text-xs">Authorization: Bearer &lt;{t('keyPlaceholder')}&gt;</code>. {t('descAfter')}
          </p>
        </div>
      </div>

      {keys.data && keys.data.length > 0 && (
        <ul className="divide-y divide-border rounded-xl border border-border">
          {keys.data.map((k) => (
            <li key={k.id} className="flex items-center gap-3 px-3 py-2.5">
              <div className="min-w-0 flex-1">
                <p className="truncate text-sm font-medium">{k.name}</p>
                <p className="truncate text-xs text-muted">
                  <code>{k.prefix}…</code> · {t('created', { date: formatInstant(k.createdAt) })} ·{' '}
                  {k.lastUsedAt ? t('lastUsed', { date: formatInstant(k.lastUsedAt) }) : t('neverUsed')}
                </p>
              </div>
              <Button variant="ghost" size="sm" aria-label={t('revokeAria', { name: k.name })} onClick={() => setRevoking(k)}>
                <Trash2 className="size-4" aria-hidden />
              </Button>
            </li>
          ))}
        </ul>
      )}

      <Button variant="secondary" onClick={() => setCreating(true)}>
        <Plus className="size-4" aria-hidden />
        {t('newKey')}
      </Button>

      <CreateKeyDialog open={creating} onClose={() => setCreating(false)} />
      <ConfirmDialog
        open={revoking !== null}
        onClose={() => setRevoking(null)}
        onConfirm={() => revoking && revoke.mutate(revoking.id)}
        loading={revoke.isPending}
        title={t('revokeTitle', { name: revoking?.name ?? '' })}
        description={t('revokeDescription')}
        confirmLabel={t('revoke')}
        danger
      />
    </Card>
  )
}

function CreateKeyDialog({ open, onClose }: { open: boolean; onClose: () => void }) {
  const { t } = useTranslation('apikeys')
  const onPlanLimit = usePlanLimitHandler()
  const toast = useToast()
  const invalidate = useInvalidateData()
  const formId = useId()
  const [created, setCreated] = useState<CreatedApiKey | null>(null)
  const create = useMutation({
    mutationFn: (name: string) => apiKeysApi.create(name),
    onSuccess: setCreated,
    onError: (err) => {
      if (onPlanLimit(err)) close()
      else toast.error(errorMessage(err))
    },
    onSettled: () => invalidate(),
  })

  function close() {
    setCreated(null)
    onClose()
  }

  return (
    <Dialog
      open={open}
      onClose={close}
      title={created ? t('createdTitle') : t('newTitle')}
      description={
        created
          ? t('createdDescription')
          : t('newDescription')
      }
      footer={
        created ? (
          <Button block onClick={close}>
            {t('done')}
          </Button>
        ) : (
          <>
            <Button variant="secondary" block onClick={close}>
              {t('cancel')}
            </Button>
            <Button type="submit" form={formId} block loading={create.isPending}>
              {t('create')}
            </Button>
          </>
        )
      }
    >
      {created ? <ShowKey apiKey={created.key} /> : <NameForm formId={formId} onSubmit={(name) => create.mutate(name)} />}
    </Dialog>
  )
}

function NameForm({ formId, onSubmit }: { formId: string; onSubmit: (name: string) => void }) {
  const { t } = useTranslation('apikeys')
  const [name, setName] = useState('')
  const [error, setError] = useState<string>()

  function handleSubmit(e: FormEvent) {
    e.preventDefault()
    if (!name.trim()) return setError(t('nameRequired'))
    onSubmit(name.trim())
  }

  return (
    <form id={formId} onSubmit={handleSubmit} noValidate>
      <Input
        label={t('nameLabel')}
        value={name}
        onChange={(e) => setName(e.target.value)}
        placeholder={t('namePlaceholder')}
        maxLength={60}
        autoFocus
        error={error}
      />
    </form>
  )
}

function ShowKey({ apiKey }: { apiKey: string }) {
  const { t } = useTranslation('apikeys')
  const [copied, setCopied] = useState(false)

  async function copy() {
    try {
      await navigator.clipboard.writeText(apiKey)
      setCopied(true)
    } catch {
      // Sem acesso à área de transferência: o texto continua selecionável.
    }
  }

  return (
    <div className="flex flex-col gap-3">
      <div className="flex items-center gap-2">
        <code className="min-w-0 flex-1 rounded-xl border border-border bg-surface-2 px-3 py-2.5 text-xs break-all select-all">
          {apiKey}
        </code>
        <Button variant="secondary" size="sm" aria-label={t('copyAria')} onClick={() => void copy()}>
          {copied ? <Check className="size-4 text-done" aria-hidden /> : <Copy className="size-4" aria-hidden />}
        </Button>
      </div>
      <p className="text-xs text-muted">{t('example')}</p>
      <code className="rounded-xl bg-surface-2 px-3 py-2 text-xs break-all">
        curl -H &quot;Authorization: Bearer {apiKey.slice(0, 12)}…&quot; {window.location.origin}/api/today
      </code>
    </div>
  )
}
