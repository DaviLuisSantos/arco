import type { Cycle } from '../../api/types'
import { Card } from '../../components/ui/Card'
import { formatFullDate } from '../../lib/dates'
import { CYCLE_PROMPTS } from '../reviews/review-answers'
import { DeleteCycleButton } from './DeleteCycleButton'
import { ReviewAnswersView } from '../reviews/ReviewQuestions'

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
