import { useQuery, useQueryClient } from '@tanstack/react-query'
import { UserRound } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router'
import { authApi } from '../../api/endpoints'
import { Button } from '../../components/ui/Button'
import { Card } from '../../components/ui/Card'
import { ConfirmDialog } from '../../components/ui/ConfirmDialog'
import { Input } from '../../components/ui/Input'
import { useToast } from '../../components/ui/toast-context'
import { errorMessage } from '../../lib/errors'
import { queryKeys } from '../../lib/queryKeys'
import { useAppMutation } from '../../lib/useAppMutation'
import { useAuthStatus } from '../../lib/useAuthStatus'

/** Conta no Perfil: e-mail, senha, vínculo com o Google e exclusão da conta. */
export function AccountCard() {
  const { t } = useTranslation('account')
  const me = useAuthStatus().data
  const qc = useQueryClient()
  const navigate = useNavigate()
  const toast = useToast()
  const providers = useQuery({ queryKey: ['providers'], queryFn: authApi.providers, staleTime: Infinity })

  const [current, setCurrent] = useState('')
  const [next, setNext] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [confirming, setConfirming] = useState(false)
  const [deletePassword, setDeletePassword] = useState('')
  const [deleting, setDeleting] = useState(false)

  const change = useAppMutation(
    (input: { currentPassword?: string; newPassword: string }) => authApi.changePassword(input),
    {
      successMessage: t('passwordSaved'),
      onSuccess: () => {
        setCurrent('')
        setNext('')
        void qc.invalidateQueries({ queryKey: queryKeys.auth })
      },
    },
  )

  if (!me) return null

  function submit(e: FormEvent) {
    e.preventDefault()
    if (next.length < 8) {
      setError(t('passwordShort'))
      return
    }
    if (me?.hasPassword && !current) {
      setError(t('passwordCurrentRequired'))
      return
    }
    setError(null)
    change.mutate({ currentPassword: me?.hasPassword ? current : undefined, newPassword: next })
  }

  async function deleteAccount() {
    setDeleting(true)
    try {
      await authApi.deleteAccount(me?.hasPassword ? deletePassword : undefined)
      qc.clear()
      qc.setQueryData(queryKeys.auth, null)
      void navigate('/login', { replace: true })
    } catch (err) {
      toast.error(errorMessage(err))
      setDeleting(false)
    }
  }

  return (
    <Card className="flex flex-col gap-4">
      <div className="flex items-start gap-3">
        <UserRound className="mt-0.5 size-5 shrink-0 text-accent" aria-hidden />
        <div className="min-w-0">
          <p className="font-semibold">{t('title')}</p>
          <p className="truncate text-sm">{me.displayName}</p>
          <p className="truncate text-sm text-muted">
            {me.email}
            {!me.isDemo && (me.emailVerified ? t('verified') : t('unverified'))}
          </p>
          {providers.data?.google && (
            <p className="text-sm text-muted">{t('googleStatus', { status: me.hasGoogle ? t('googleLinked') : t('googleNotLinked') })}</p>
          )}
        </div>
      </div>

      {!me.isDemo && (
        <form onSubmit={submit} className="flex flex-col gap-3" noValidate>
          <p className="text-sm font-medium">{me.hasPassword ? t('changePassword') : t('setPassword')}</p>
          {me.hasPassword && (
            <Input
              label={t('currentPassword')}
              type="password"
              autoComplete="current-password"
              value={current}
              onChange={(e) => setCurrent(e.target.value)}
            />
          )}
          <Input
            label={t('newPassword')}
            type="password"
            autoComplete="new-password"
            value={next}
            onChange={(e) => setNext(e.target.value)}
            error={error}
            hint={t('passwordHint')}
          />
          <Button type="submit" variant="secondary" loading={change.isPending}>
            {me.hasPassword ? t('changePassword') : t('setPassword')}
          </Button>
        </form>
      )}

      <Button variant="danger" onClick={() => setConfirming(true)}>
        {t('deleteAccount')}
      </Button>

      <ConfirmDialog
        open={confirming}
        onClose={() => {
          setConfirming(false)
          setDeletePassword('')
        }}
        onConfirm={() => void deleteAccount()}
        loading={deleting}
        title={t('deleteTitle')}
        description={
          <div className="flex flex-col gap-3">
            <span>{t('deleteDescription')}</span>
            {me.hasPassword && (
              <Input
                label={t('deleteConfirmPassword')}
                type="password"
                autoComplete="current-password"
                value={deletePassword}
                onChange={(e) => setDeletePassword(e.target.value)}
              />
            )}
          </div>
        }
        confirmLabel={t('deleteConfirm')}
        danger
      />
    </Card>
  )
}
