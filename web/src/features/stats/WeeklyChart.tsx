import { useState } from 'react'
import type { WeekStats } from '../../api/types'
import { Segmented } from '../../components/ui/Segmented'
import { cn } from '../../lib/cn'
import { formatDate } from '../../lib/dates'
import { formatPercent, formatXp } from '../../lib/format'

type Metric = 'rate' | 'xp'

const METRICS = {
  rate: { bar: 'bg-accent', label: 'Consistência' },
  xp: { bar: 'bg-xp', label: 'XP' },
}

/**
 * Barras das últimas semanas, uma medida por vez (consistência ou XP — nunca duas escalas juntas).
 * Tocar (ou passar o mouse) numa barra mostra os números daquela semana embaixo.
 */
export function WeeklyChart({ weeks, today }: { weeks: WeekStats[]; today: string }) {
  const [metric, setMetric] = useState<Metric>('rate')
  const [selected, setSelected] = useState(weeks.length - 1)
  const values = weeks.map((w) => (metric === 'rate' ? w.rate : w.xp))
  const max = metric === 'rate' ? 1 : Math.max(1, ...values)
  const week = weeks[selected]
  const partial = week && week.weekEnd > today

  return (
    <div className="flex flex-col gap-3">
      <Segmented
        label="Medida do gráfico"
        value={metric}
        onChange={setMetric}
        options={[
          { value: 'rate', label: 'Consistência' },
          { value: 'xp', label: 'XP' },
        ]}
      />

      <div className="relative pt-5">
        <span className="absolute top-0 right-0 text-[11px] leading-4 text-muted">
          {metric === 'rate' ? '100%' : formatXp(max)}
        </span>
        <div className="absolute inset-x-0 top-5 border-t border-dashed border-border" aria-hidden />
        <div
          className="flex h-40 items-end gap-0.5 border-b border-border"
          role="group"
          aria-label={`${METRICS[metric].label} nas últimas ${weeks.length} semanas`}
        >
          {weeks.map((w, i) => {
            const value = values[i]
            const empty = metric === 'rate' && w.expected === 0
            const label = `Semana de ${formatDate(w.weekStart)}: ${empty ? 'sem hábitos' : metric === 'rate' ? formatPercent(value) : formatXp(value)}`
            return (
              <button
                key={w.weekStart}
                type="button"
                aria-label={label}
                aria-pressed={i === selected}
                onClick={() => setSelected(i)}
                onMouseEnter={() => setSelected(i)}
                className="flex h-full min-w-0 flex-1 items-end justify-center"
              >
                {!empty && value > 0 && (
                  <span
                    className={cn(
                      'block w-full max-w-6 rounded-t-[4px] transition-[height,opacity]',
                      METRICS[metric].bar,
                      i === selected ? 'opacity-100' : 'opacity-60',
                      w.weekEnd > today && 'bg-[repeating-linear-gradient(135deg,transparent_0_3px,rgb(0_0_0/0.25)_3px_5px)]',
                    )}
                    style={{ height: `max(2px, ${(value / max) * 100}%)` }}
                  />
                )}
              </button>
            )
          })}
        </div>
        <div className="mt-1 flex gap-0.5 text-[10px] text-muted" aria-hidden>
          {weeks.map((w, i) => (
            <span key={w.weekStart} className="min-w-0 flex-1 text-center">
              {i === weeks.length - 1 ? 'atual' : i % 3 === 0 ? formatDate(w.weekStart, { day: 'numeric', month: 'numeric' }) : ''}
            </span>
          ))}
        </div>
      </div>

      {week && (
        <p className="rounded-xl bg-surface-2 px-3 py-2 text-sm" aria-live="polite">
          <span className="text-muted">
            {formatDate(week.weekStart)} a {formatDate(week.weekEnd)}
            {partial && ' (até hoje)'}:
          </span>{' '}
          {week.expected === 0 ? (
            <span className="text-muted">sem hábitos nesta semana</span>
          ) : (
            <>
              <span className="font-semibold">{formatPercent(week.rate)}</span>{' '}
              <span className="text-muted">
                ({week.done + week.minimum} de {week.expected})
              </span>
            </>
          )}
          {' · '}
          <span className="font-semibold">{formatXp(week.xp)}</span>
        </p>
      )}
    </div>
  )
}
