import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { cyclesApi } from '../../api/endpoints'
import type { Cycle } from '../../api/types'
import { Card } from '../../components/ui/Card'
import { Dialog } from '../../components/ui/Dialog'
import { queryKeys } from '../../lib/queryKeys'
import { Arch } from '../arch/Arch'
import { ArchPanel } from '../arch/ArchPanel'
import { formatFullDate } from '../../lib/dates'
import { CYCLE_PROMPTS } from '../reviews/review-answers'
import { DeleteCycleButton } from './DeleteCycleButton'
import { ReviewAnswersView } from '../reviews/ReviewQuestions'

/** Mini-arco do ciclo encerrado; abre o arco completo em um diálogo. */
function PastArch({ cycle }: { cycle: Cycle }) {
  const [open, setOpen] = useState(false)
  const query = useQuery({ queryKey: queryKeys.cycleArch(cycle.id), queryFn: () => cyclesApi.arch(cycle.id) })

  return (
    <>
      <button
        type="button"
        onClick={() => setOpen(true)}
        aria-label={`Ver o arco de ${cycle.name}`}
        className="mt-3 block w-full rounded-xl border border-border bg-surface-2/50 p-2 hover:border-accent focus-visible:border-accent focus-visible:outline-none"
      >
        {query.data ? (
          <div className="mx-auto max-w-56">
            <Arch arch={query.data} small />
          </div>
        ) : (
          <p className="py-3 text-center text-xs text-muted">
            {query.isLoadingError ? 'Não foi possível carregar o arco. Toque para tentar de novo.' : 'Carregando o arco…'}
          </p>
        )}
      </button>
      <Dialog open={open} onClose={() => setOpen(false)} title={cycle.name} description="Arco do ciclo encerrado.">
        <ArchPanel cycleId={cycle.id} cycle={cycle} />
      </Dialog>
    </>
  )
}

export function PastCycles({ cycles }: { cycles: Cycle[] }) {
  const past = cycles.filter((c) => c.status === 'Completed')
  if (past.length === 0) return null

  return (
    <section aria-labelledby="past-title">
      <h2 id="past-title" className="mb-2 text-sm font-semibold uppercase tracking-wide text-muted">
        Ciclos anteriores
      </h2>
      <ul className="flex flex-col gap-2">
        {past.map((c) => (
          <li key={c.id}>
            <Card>
              <p className="font-semibold">{c.name}</p>
              <p className="text-xs text-muted">
                {formatFullDate(c.startDate)} – {formatFullDate(c.endDate)} · {c.totalDays} dias
              </p>
              <PastArch cycle={c} />
              {c.reviewNotes || c.reviewWins || c.reviewObstacles || c.reviewNext ? (
                <div className="mt-3 flex flex-col gap-3">
                  <ReviewAnswersView
                    prompts={CYCLE_PROMPTS}
                    answers={{ wins: c.reviewWins, obstacles: c.reviewObstacles, next: c.reviewNext }}
                  />
                  {c.reviewNotes && <p className="whitespace-pre-line text-sm">{c.reviewNotes}</p>}
                </div>
              ) : (
                <p className="mt-2 text-sm text-muted italic">Sem revisão.</p>
              )}
              <div className="mt-2 -mb-2 flex justify-end">
                <DeleteCycleButton cycle={c} />
              </div>
            </Card>
          </li>
        ))}
      </ul>
    </section>
  )
}
