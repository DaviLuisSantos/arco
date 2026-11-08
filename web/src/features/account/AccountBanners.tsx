import { FlaskConical, MailWarning } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { authApi } from '../../api/endpoints'
import { Button } from '../../components/ui/Button'
import { useAppMutation } from '../../lib/useAppMutation'
import { useAuthStatus } from '../../lib/useAuthStatus'

const BOX = 'mb-4 flex flex-wrap items-center gap-2 rounded-xl border border-border bg-surface-2 px-3 py-2 text-sm'

/** Avisos no topo do app: conta de demonstração e e-mail por confirmar. */
export function AccountBanners() {
  const { t } = useTranslation('account')
  const me = useAuthStatus().data
  const resend = useAppMutation(() => authApi.resendVerification(), {
    successMessage: t('resent'),
  })

  if (!me) return null

  if (me.isDemo) {
    return (
      <div role="status" className={BOX}>
        <FlaskConical className="size-4 shrink-0 text-accent" aria-hidden />
        <span className="min-w-0 flex-1">{t('demoBanner')}</span>
        <Link to="/login?mode=register" className="font-semibold text-accent underline">
          {t('createAccount')}
        </Link>
      </div>
    )
  }

  if (!me.emailVerified) {
    return (
      <div role="status" className={BOX}>
        <MailWarning className="size-4 shrink-0 text-accent" aria-hidden />
        <span className="min-w-0 flex-1">{t('verifyEmail', { email: me.email })}</span>
        <Button variant="secondary" loading={resend.isPending} onClick={() => resend.mutate()}>
          {t('resend')}
        </Button>
      </div>
    )
  }

  return null
}
