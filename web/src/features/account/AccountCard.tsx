import { useQuery, useQueryClient } from '@tanstack/react-query'
import { UserRound } from 'lucide-react'
import { useState, type FormEvent } from 'react'
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
      successMessage: 'Senha salva.',
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
      setError('A senha precisa ter pelo menos 8 caracteres.')
      return
    }
    if (me?.hasPassword && !current) {
      setError('Informe a senha atual.')
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
          <p className="font-semibold">Conta</p>
          <p className="truncate text-sm">{me.displayName}</p>
          <p className="truncate text-sm text-muted">
            {me.email}
            {!me.isDemo && (me.emailVerified ? ' · verificado' : ' · não verificado')}
          </p>
          {providers.data?.google && (
            <p className="text-sm text-muted">Google: {me.hasGoogle ? 'vinculado' : 'não vinculado'}</p>
          )}
        </div>
      </div>

      {!me.isDemo && (
        <form onSubmit={submit} className="flex flex-col gap-3" noValidate>
          <p className="text-sm font-medium">{me.hasPassword ? 'Trocar senha' : 'Definir senha'}</p>
          {me.hasPassword && (
            <Input
              label="Senha atual"
              type="password"
              autoComplete="current-password"
              value={current}
              onChange={(e) => setCurrent(e.target.value)}
            />
          )}
          <Input
            label="Nova senha"
            type="password"
            autoComplete="new-password"
            value={next}
            onChange={(e) => setNext(e.target.value)}
            error={error}
            hint="Mínimo de 8 caracteres."
          />
          <Button type="submit" variant="secondary" loading={change.isPending}>
            {me.hasPassword ? 'Trocar senha' : 'Definir senha'}
          </Button>
        </form>
      )}

      <Button variant="danger" onClick={() => setConfirming(true)}>
        Excluir conta
      </Button>

      <ConfirmDialog
        open={confirming}
        onClose={() => {
          setConfirming(false)
          setDeletePassword('')
        }}
        onConfirm={() => void deleteAccount()}
        loading={deleting}
        title="Excluir sua conta?"
        description={
          <div className="flex flex-col gap-3">
            <span>Apaga a conta e todos os seus dados, de forma definitiva. Exporte os dados antes se quiser guardá-los.</span>
            {me.hasPassword && (
              <Input
                label="Confirme com sua senha"
                type="password"
                autoComplete="current-password"
                value={deletePassword}
                onChange={(e) => setDeletePassword(e.target.value)}
              />
            )}
          </div>
        }
        confirmLabel="Excluir tudo"
        danger
      />
    </Card>
  )
}
