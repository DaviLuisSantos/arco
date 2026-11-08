import { useMutation, useQuery } from '@tanstack/react-query'
import { Crown } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { billingApi } from '../../api/endpoints'
import { Button } from '../../components/ui/Button'
import { Card } from '../../components/ui/Card'
import { useToast } from '../../components/ui/toast-context'
import { errorMessage } from '../../lib/errors'
import { useOnline } from '../../lib/useOnline'

function formatDate(iso: string, locale: string): string {
  return new Intl.DateTimeFormat(locale, { dateStyle: 'long' }).format(new Date(iso))
}

/** Plano atual, uso x limite e acesso ao checkout e ao portal do Stripe. */
export function PlanCard() {
  const { t, i18n } = useTranslation('billing')
  const language = i18n.language
  const toast = useToast()
  const online = useOnline()
  const { data } = useQuery({ queryKey: ['billing'], queryFn: billingApi.get })

  const redirect = useMutation({
    mutationFn: (kind: 'checkout' | 'portal') => (kind === 'checkout' ? billingApi.checkout() : billingApi.portal()),
    onSuccess: ({ url }) => window.location.assign(url),
    onError: (e) => toast.error(errorMessage(e)),
  })

  if (!data) return null
  const pro = data.plan === 'pro'
  const usage = (used: number, limit: number | null) =>
    limit === null ? `${used} (${t('unlimited')})` : t('usageOf', { used, limit })

  return (
    <Card id="plano" className="flex flex-col gap-3">
      <div className="flex items-start gap-3">
        <Crown className="mt-0.5 size-5 shrink-0 text-accent" aria-hidden />
        <div className="min-w-0">
          <p className="font-semibold">{t('title')}</p>
          <p className="text-sm text-muted">{t('currentPlan', { plan: pro ? t('pro') : t('free') })}</p>
          {pro && data.renewsAt && (
            <p className="text-sm text-muted">
              {t(data.cancelAtPeriodEnd ? 'cancelsAt' : 'renewsAt', { date: formatDate(data.renewsAt, language) })}
            </p>
          )}
        </div>
      </div>
      <dl className="flex flex-col gap-1 text-sm">
        <div className="flex justify-between gap-3">
          <dt className="text-muted">{t('usageHabits')}</dt>
          <dd>{usage(data.usage.habits, data.limits.habits)}</dd>
        </div>
        <div className="flex justify-between gap-3">
          <dt className="text-muted">{t('usageApiKeys')}</dt>
          <dd>{usage(data.usage.apiKeys, data.limits.apiKeys)}</dd>
        </div>
        <div className="flex justify-between gap-3">
          <dt className="text-muted">{t('mcp')}</dt>
          <dd>{data.limits.mcp ? t('mcpYes') : t('mcpNo')}</dd>
        </div>
      </dl>
      {data.stripeEnabled && (
        <>
          {!pro ? (
            <Button loading={redirect.isPending} disabled={!online} onClick={() => redirect.mutate('checkout')}>
              {t('subscribe')}
            </Button>
          ) : (
            <Button
              variant="secondary"
              loading={redirect.isPending}
              disabled={!online}
              onClick={() => redirect.mutate('portal')}
            >
              {t('manage')}
            </Button>
          )}
          <p className="text-xs text-muted">{t('testMode')}</p>
        </>
      )}
    </Card>
  )
}
