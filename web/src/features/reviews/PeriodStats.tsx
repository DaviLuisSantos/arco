import { ChevronsUp } from 'lucide-react'
import { Link } from 'react-router'
import type { CheckInTotals, HabitPeriodStats } from '../../api/types'
import { Card } from '../../components/ui/Card'
import { ProgressBar } from '../../components/ui/ProgressBar'
import { formatDuration, formatPercent, formatXp } from '../../lib/format'

interface PeriodStatsProps {
  xp: number
  checkIns: CheckInTotals
  habits: HabitPeriodStats[]
  /** Total de foco no período; quando informado, mostra horas total e por hábito. */
  minutes?: number
  showMinutes?: boolean
}

/** Números de um período (semana ou ciclo): XP, totais de check-in e consistência por hábito. */
export function PeriodStats({ xp, checkIns, habits, minutes, showMinutes }: PeriodStatsProps) {
  return (
    <div className="flex flex-col gap-3">
      <div className="grid grid-cols-4 gap-2 text-center">
        <Stat label="XP" value={formatXp(xp)} className="text-xp" />
        <Stat label="Feito" value={checkIns.done} className="text-done" />
        <Stat label="Mínimo" value={checkIns.minimum} className="text-minimum" />
        <Stat label="Não fiz" value={checkIns.missed} className="text-missed" />
      </div>

      {showMinutes && (
        <p className="text-sm text-muted">
          Foco no período: <span className="font-semibold text-text">{formatDuration(minutes ?? 0)}</span>
        </p>
      )}

      {habits.length === 0 ? (
        <p className="text-sm text-muted">Nenhum hábito neste período.</p>
      ) : (
        <Card className="p-0">
          <ul className="divide-y divide-border">
            {habits.map((h) => (
              <li key={h.habitId} className="flex flex-col gap-1.5 px-4 py-3">
                <div className="flex items-baseline justify-between gap-2 text-sm">
                  <Link to={`/habits/${h.habitId}`} className="min-w-0 truncate font-medium hover:underline">
                    {h.emoji ? `${h.emoji} ` : ''}
                    {h.name}
                  </Link>
                  <span className="shrink-0 text-muted">
                    {h.done + h.minimum}/{h.expected} · {formatPercent(h.rate)}
                    {showMinutes && ` · ${formatDuration(h.minutes ?? 0)}`}
                  </span>
                </div>
                <ProgressBar value={h.rate} size="sm" tone={h.rate >= 0.8 ? 'done' : 'accent'} label={h.name} />
                {(h.levelsGained > 0 || h.levelUpSuggested) && (
                  <p className="flex items-center gap-1 text-xs text-accent">
                    <ChevronsUp className="size-3.5" aria-hidden />
                    {h.levelsGained > 0
                      ? `Subiu ${h.levelsGained} nível${h.levelsGained > 1 ? 'is' : ''} · agora no ${h.level}`
                      : `Pronto para o nível ${h.level + 1}`}
                  </p>
                )}
              </li>
            ))}
          </ul>
        </Card>
      )}
    </div>
  )
}

function Stat({ label, value, className }: { label: string; value: number | string; className?: string }) {
  return (
    <div className="rounded-xl bg-surface-2 px-1 py-2">
      <p className={`text-base font-bold ${className ?? ''}`}>{value}</p>
      <p className="text-[11px] text-muted">{label}</p>
    </div>
  )
}
