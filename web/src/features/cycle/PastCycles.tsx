import type { Cycle } from '../../api/types'
import { Card } from '../../components/ui/Card'
import { formatFullDate } from '../../lib/dates'

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
              {c.reviewNotes ? (
                <p className="mt-2 whitespace-pre-line text-sm">{c.reviewNotes}</p>
              ) : (
                <p className="mt-2 text-sm text-muted italic">Sem revisão.</p>
              )}
            </Card>
          </li>
        ))}
      </ul>
    </section>
  )
}
