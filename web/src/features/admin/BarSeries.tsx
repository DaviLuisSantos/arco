import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { formatDate } from '../../lib/dates'

interface Point {
  date: string
  count: number
}

const W = 600
const H = 140
const PAD_B = 18

/** Barras de uma série única (30 dias), cor de acento, com tooltip por barra e tabela alternativa. */
export function BarSeries({ title, points }: { title: string; points: Point[] }) {
  const { t } = useTranslation('admin')
  const [hover, setHover] = useState<number | null>(null)
  const max = Math.max(1, ...points.map((p) => p.count))
  const slot = W / Math.max(1, points.length)
  const bw = Math.max(2, slot - 3)
  const plotH = H - PAD_B
  const active = hover !== null ? points[hover] : null

  return (
    <section className="rounded-2xl border border-border bg-surface p-4">
      <div className="mb-2 flex items-baseline justify-between gap-2">
        <h2 className="text-sm font-semibold">{title}</h2>
        <span className="text-xs text-muted" aria-live="polite">
          {active ? t('tooltip', { date: formatDate(active.date), count: active.count }) : `max ${max}`}
        </span>
      </div>
      {points.length === 0 ? (
        <p className="text-sm text-muted">{t('noData')}</p>
      ) : (
        <svg viewBox={`0 0 ${W} ${H}`} className="w-full" role="img" aria-label={title} onMouseLeave={() => setHover(null)}>
          <line x1={0} x2={W} y1={plotH} y2={plotH} stroke="var(--border)" strokeWidth={1} />
          <line x1={0} x2={W} y1={0} y2={0} stroke="var(--border)" strokeWidth={1} strokeDasharray="2 4" />
          {points.map((p, i) => {
            const h = (p.count / max) * (plotH - 4)
            const x = i * slot + (slot - bw) / 2
            return (
              <g key={p.date} onMouseEnter={() => setHover(i)} onTouchStart={() => setHover(i)}>
                <rect x={i * slot} y={0} width={slot} height={H} fill="transparent" />
                {p.count > 0 && (
                  <rect
                    x={x}
                    y={plotH - h}
                    width={bw}
                    height={h}
                    rx={2}
                    fill="var(--accent)"
                    opacity={hover === null || hover === i ? 1 : 0.55}
                  />
                )}
              </g>
            )
          })}
          <text x={0} y={H - 3} fontSize={10} fill="var(--muted)">
            {formatDate(points[0].date)}
          </text>
          <text x={W} y={H - 3} fontSize={10} fill="var(--muted)" textAnchor="end">
            {formatDate(points[points.length - 1].date)}
          </text>
        </svg>
      )}
      <details className="mt-2 text-xs text-muted">
        <summary className="cursor-pointer">{t('table')}</summary>
        <table className="mt-2 w-full text-left">
          <thead>
            <tr>
              <th className="py-1 font-semibold">{t('date')}</th>
              <th className="py-1 font-semibold">{t('count')}</th>
            </tr>
          </thead>
          <tbody>
            {points.map((p) => (
              <tr key={p.date}>
                <td className="py-0.5">{formatDate(p.date)}</td>
                <td className="py-0.5 tabular-nums">{p.count}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </details>
    </section>
  )
}
