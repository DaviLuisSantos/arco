import { ChevronRight, Plus } from 'lucide-react'
import { Link } from 'react-router'
import type { Cycle } from '../../api/types'
import { ProgressBar } from '../../components/ui/ProgressBar'

export function CycleStrip({ cycle }: { cycle: Cycle | null }) {
  if (!cycle) {
    return (
      <Link
        to="/cycle"
        className="flex items-center gap-3 rounded-2xl border border-dashed border-accent/60 bg-accent/5 p-4 transition hover:bg-accent/10"
      >
        <span className="grid size-10 place-items-center rounded-xl bg-accent/15 text-accent">
          <Plus className="size-5" aria-hidden />
        </span>
        <span className="flex-1">
          <span className="block font-semibold">Comece um ciclo de 6 semanas</span>
          <span className="block text-sm text-muted">Defina até 3 objetivos e dê direção aos seus hábitos.</span>
        </span>
        <ChevronRight className="size-5 text-muted" aria-hidden />
      </Link>
    )
  }

  return (
    <Link to="/cycle" className="block rounded-2xl border border-border bg-surface p-4 transition hover:border-accent/60">
      <div className="mb-2 flex items-end justify-between gap-3">
        <div className="min-w-0">
          <p className="truncate font-semibold">{cycle.name}</p>
          <p className="text-sm text-muted">
            Dia {cycle.dayNumber} de {cycle.totalDays}
          </p>
        </div>
        <span className="shrink-0 text-sm text-muted">
          {Math.max(0, cycle.totalDays - cycle.dayNumber)} dias restantes
        </span>
      </div>
      <ProgressBar value={cycle.dayNumber / Math.max(1, cycle.totalDays)} size="sm" label="Progresso do ciclo" />
    </Link>
  )
}
