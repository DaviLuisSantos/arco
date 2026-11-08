import { FlaskConical, MailWarning } from 'lucide-react'
import { Link } from 'react-router'
import { authApi } from '../../api/endpoints'
import { Button } from '../../components/ui/Button'
import { useAppMutation } from '../../lib/useAppMutation'
import { useAuthStatus } from '../../lib/useAuthStatus'

const BOX = 'mb-4 flex flex-wrap items-center gap-2 rounded-xl border border-border bg-surface-2 px-3 py-2 text-sm'

/** Avisos no topo do app: conta de demonstração e e-mail por confirmar. */
export function AccountBanners() {
  const me = useAuthStatus().data
  const resend = useAppMutation(() => authApi.resendVerification(), {
    successMessage: 'E-mail de confirmação reenviado.',
  })

  if (!me) return null

  if (me.isDemo) {
    return (
      <div role="status" className={BOX}>
        <FlaskConical className="size-4 shrink-0 text-accent" aria-hidden />
        <span className="min-w-0 flex-1">Conta de demonstração, apagada em 24h.</span>
        <Link to="/login?mode=register" className="font-semibold text-accent underline">
          Criar conta
        </Link>
      </div>
    )
  }

  if (!me.emailVerified) {
    return (
      <div role="status" className={BOX}>
        <MailWarning className="size-4 shrink-0 text-accent" aria-hidden />
        <span className="min-w-0 flex-1">Confirme seu e-mail: enviamos um link para {me.email}.</span>
        <Button variant="secondary" loading={resend.isPending} onClick={() => resend.mutate()}>
          Reenviar
        </Button>
      </div>
    )
  }

  return null
}
