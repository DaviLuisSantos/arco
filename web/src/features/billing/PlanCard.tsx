import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Crown } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { useSearchParams } from 'react-router'
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
  const { data, refetch } = useQuery({ queryKey: ['billing'], queryFn: billingApi.get })

  const redirect = useMutation({
    mutationFn: (kind: 'checkout' | 'portal') => (kind === 'checkout' ? billingApi.checkout() : billingApi.portal()),
    onSuccess: ({ url }) => window.location.assign(url),
    onError: (e) => toast.error(errorMessage(e)),
  })

  const [params, setParams] = useSearchParams()
  const checkout = params.get('checkout')
  const handled = useRef(false)
  const [polling, setPolling] = useState(() => checkout === 'success')
  const queryClient = useQueryClient()
  const refetchBilling = refetch

  useEffect(() => {
    if (!checkout || handled.current) return
    handled.current = true
    setParams(
      (p) => {
        p.delete('checkout')
        return p
      },
      { replace: true },
    )
    if (checkout === 'cancel' || checkout === 'canceled') {
      toast.show({ message: t('checkoutCancel'), variant: 'info' })
      return
    }
    if (checkout !== 'success') return
    toast.success(t('checkoutSuccess'))
    void refetchBilling()
  }, [checkout, setParams, toast, t, refetchBilling])

  // O webhook do Stripe pode atrasar: consulta de novo algumas vezes, parando ao sair da tela ou ao virar Pro.
  const isPro = data?.plan === 'pro'
  useEffect(() => {
    if (!polling || isPro) return
    let tries = 0
    const timer = window.setInterval(() => {
      tries += 1
      void queryClient.invalidateQueries({ queryKey: ['billing'] })
      if (tries >= 5) {
        window.clearInterval(timer)
        setPolling(false)
      }
    }, 2500)
    return () => window.clearInterval(timer)
  }, [polling, isPro, queryClient])

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
      {!pro && (
        <table className="w-full text-left text-xs">
          <caption className="mb-1 text-left text-sm font-semibold">{t('compareTitle')}</caption>
          <thead>
            <tr className="text-muted">
              <th scope="col" className="py-1 font-medium">{t('compareFeature')}</th>
              <th scope="col" className="py-1 font-medium">{t('free')}</th>
              <th scope="col" className="py-1 font-medium">{t('pro')}</th>
            </tr>
          </thead>
          <tbody>
            <tr className="border-t border-border">
              <th scope="row" className="py-1 font-normal">{t('compareHabits')}</th>
              <td>{data.limits.habits === null ? t('compareUnlimited') : data.limits.habits}</td>
              <td>{t('compareUnlimited')}</td>
            </tr>
            <tr className="border-t border-border">
              <th scope="row" className="py-1 font-normal">{t('compareApiKeys')}</th>
              <td>{data.limits.apiKeys === null ? t('compareUnlimited') : data.limits.apiKeys}</td>
              <td>{t('compareMore')}</td>
            </tr>
            <tr className="border-t border-border">
              <th scope="row" className="py-1 font-normal">{t('compareMcp')}</th>
              <td>{data.limits.mcp ? t('compareYes') : t('compareNo')}</td>
              <td>{t('compareYes')}</td>
            </tr>
            <tr className="border-t border-border">
              <th scope="row" className="py-1 font-normal">{t('compareTheme')}</th>
              <td>{data.limits.profileTheme ? t('compareYes') : t('compareNo')}</td>
              <td>{t('compareYes')}</td>
            </tr>
          </tbody>
        </table>
      )}
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
