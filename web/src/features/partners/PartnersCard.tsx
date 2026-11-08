import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Copy, Trash2, UserPlus } from 'lucide-react'
import { useState, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { partnersApi } from '../../api/endpoints'
import type { PartnerInvite } from '../../api/types'
import { Button } from '../../components/ui/Button'
import { Card } from '../../components/ui/Card'
import { ConfirmDialog } from '../../components/ui/ConfirmDialog'
import { useToast } from '../../components/ui/toast-context'
import { formatInstant } from '../../lib/dates'
import { errorMessage } from '../../lib/errors'
import { partnersKey } from './keys'

export function PartnersCard() {
  const { t } = useTranslation('partners')
  const toast = useToast()
  const qc = useQueryClient()
  const [invite, setInvite] = useState<PartnerInvite | null>(null)
  const [removeId, setRemoveId] = useState<number | null>(null)
  const query = useQuery({ queryKey: partnersKey, queryFn: partnersApi.list })

  const refresh = () => qc.invalidateQueries({ queryKey: partnersKey })
  const create = useMutation({
    mutationFn: partnersApi.invite,
    onSuccess: (data) => {
      setInvite(data)
      void refresh()
    },
    onError: (e) => toast.error(errorMessage(e)),
  })
  const remove = useMutation({
    mutationFn: partnersApi.remove,
    onSuccess: () => {
      setRemoveId(null)
      void refresh()
    },
    onError: (e) => toast.error(errorMessage(e)),
  })

  async function copy(link: string) {
    try {
      await navigator.clipboard.writeText(link)
      toast.success(t('linkCopied'))
    } catch {
      toast.error(link)
    }
  }

  const data = query.data
  const none = data && !data.sharingWith.length && !data.sharedWithMe.length && !data.pendingInvites.length

  return (
    <Card className="space-y-4" data-testid="partners-card">
      <div>
        <h2 className="font-semibold">{t('title')}</h2>
        <p className="mt-1 text-sm text-muted">{t('intro')}</p>
      </div>

      <Button variant="secondary" loading={create.isPending} onClick={() => create.mutate()}>
        <UserPlus className="size-4" aria-hidden /> {t('invite')}
      </Button>

      {invite && (
        <div className="space-y-2 rounded-xl bg-surface-2 p-3">
          <p className="break-all text-sm">{invite.link}</p>
          <div className="flex items-center justify-between gap-2">
            <span className="text-xs text-muted">{t('inviteExpires', { date: formatInstant(invite.expiresAt) })}</span>
            <Button size="sm" onClick={() => void copy(invite.link)}>
              <Copy className="size-4" aria-hidden /> {t('copyLink')}
            </Button>
          </div>
        </div>
      )}

      {none && <p className="text-sm text-muted">{t('empty')}</p>}

      {data && data.sharingWith.length > 0 && (
        <Group title={t('sharingWith')}>
          {data.sharingWith.map((p) => (
            <Row key={p.id} name={p.displayName} sub={t('since', { date: formatInstant(p.since) })}>
              <Button variant="ghost" size="sm" aria-label={t('remove')} onClick={() => setRemoveId(p.id)}>
                <Trash2 className="size-4" aria-hidden />
              </Button>
            </Row>
          ))}
        </Group>
      )}

      {data && data.sharedWithMe.length > 0 && (
        <Group title={t('sharedWithMe')}>
          {data.sharedWithMe.map((p) => (
            <Row key={p.id} name={p.displayName} sub={t('since', { date: formatInstant(p.since) })}>
              <Link
                to={`/partners/${p.id}`}
                className="rounded-lg px-3 py-1.5 text-sm font-semibold text-accent hover:bg-surface-2"
              >
                {t('view')}
              </Link>
              <Button variant="ghost" size="sm" aria-label={t('remove')} onClick={() => setRemoveId(p.id)}>
                <Trash2 className="size-4" aria-hidden />
              </Button>
            </Row>
          ))}
        </Group>
      )}

      {data && data.pendingInvites.length > 0 && (
        <Group title={t('pending')}>
          {data.pendingInvites.map((p) => (
            <Row
              key={p.id}
              name={t('pendingItem', { created: formatInstant(p.createdAt), expires: formatInstant(p.expiresAt) })}
            >
              <Button variant="ghost" size="sm" aria-label={t('revokeInvite')} onClick={() => setRemoveId(p.id)}>
                <Trash2 className="size-4" aria-hidden />
              </Button>
            </Row>
          ))}
        </Group>
      )}

      <ConfirmDialog
        open={removeId !== null}
        onClose={() => setRemoveId(null)}
        onConfirm={() => removeId !== null && remove.mutate(removeId)}
        title={t('removeTitle')}
        description={t('removeDescription')}
        confirmLabel={t('remove')}
        danger
        loading={remove.isPending}
      />
    </Card>
  )
}

function Group({ title, children }: { title: string; children: ReactNode }) {
  return (
    <div>
      <h3 className="mb-1 text-xs font-semibold uppercase tracking-wide text-muted">{title}</h3>
      <ul className="divide-y divide-border">{children}</ul>
    </div>
  )
}

function Row({ name, sub, children }: { name: string; sub?: string; children?: ReactNode }) {
  return (
    <li className="flex items-center gap-2 py-2">
      <div className="min-w-0 flex-1">
        <p className="truncate text-sm font-semibold">{name}</p>
        {sub && <p className="text-xs text-muted">{sub}</p>}
      </div>
      {children}
    </li>
  )
}
