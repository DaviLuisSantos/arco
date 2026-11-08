import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { adminApi } from '../api/endpoints'
import { PageHeader } from '../components/ui/PageHeader'
import { ErrorState, Spinner } from '../components/ui/States'
import { BarSeries } from '../features/admin/BarSeries'
import { formatDate } from '../lib/dates'

const pct = (v: number) => `${Math.round(v * 100)}%`

export function AdminPage() {
  const { t } = useTranslation('admin')
  const query = useQuery({ queryKey: ['admin', 'metrics'], queryFn: adminApi.metrics })

  if (query.isPending) return <Spinner />
  if (query.isError) return <ErrorState error={query.error} onRetry={() => void query.refetch()} />
  const m = query.data
  const stats = [
    ['total', m.users.total],
    ['verified', m.users.verified],
    ['demo', m.users.demo],
    ['pro', m.users.pro],
  ] as const
  const active = [
    ['d1', m.activeUsers.d1],
    ['d7', m.activeUsers.d7],
    ['d30', m.activeUsers.d30],
  ] as const

  return (
    <div className="space-y-5">
      <PageHeader title={t('title')} />
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        {stats.map(([k, v]) => (
          <div key={k} className="rounded-2xl border border-border bg-surface p-4">
            <p className="text-xs text-muted">{t(`users.${k}`)}</p>
            <p className="mt-1 text-2xl font-extrabold tabular-nums">{v}</p>
          </div>
        ))}
      </div>

      <BarSeries title={t('signups')} points={m.signupsByDay} />
      <BarSeries title={t('checkIns')} points={m.checkInsByDay} />

      <section className="rounded-2xl border border-border bg-surface p-4">
        <h2 className="text-sm font-semibold">{t('active')}</h2>
        <p className="text-xs text-muted">{t('activeHint')}</p>
        <div className="mt-3 grid grid-cols-3 gap-3">
          {active.map(([k, v]) => (
            <div key={k}>
              <p className="text-xs text-muted">{t(k)}</p>
              <p className="text-2xl font-extrabold tabular-nums">{v}</p>
            </div>
          ))}
        </div>
      </section>

      <section className="rounded-2xl border border-border bg-surface p-4">
        <h2 className="mb-2 text-sm font-semibold">{t('retention')}</h2>
        {m.retention.length === 0 ? (
          <p className="text-sm text-muted">{t('noData')}</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left text-sm tabular-nums">
              <thead className="text-xs text-muted">
                <tr>
                  <th className="py-1 pr-3 font-semibold">{t('cohort')}</th>
                  <th className="py-1 pr-3 font-semibold">{t('size')}</th>
                  <th className="py-1 pr-3 font-semibold">{t('week1')}</th>
                  <th className="py-1 pr-3 font-semibold">{t('week2')}</th>
                  <th className="py-1 font-semibold">{t('week4')}</th>
                </tr>
              </thead>
              <tbody>
                {m.retention.map((r) => (
                  <tr key={r.cohortWeek} className="border-t border-border">
                    <td className="py-1.5 pr-3">{formatDate(r.cohortWeek)}</td>
                    <td className="py-1.5 pr-3">{r.size}</td>
                    {[r.week1, r.week2, r.week4].map((v, i) => (
                      <td key={i} className="py-1.5 pr-3">
                        <span
                          className="rounded px-1.5 py-0.5"
                          style={{ background: `color-mix(in srgb, var(--accent) ${Math.round(v * 60)}%, transparent)` }}
                        >
                          {pct(v)}
                        </span>
                      </td>
                    ))}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </div>
  )
}
