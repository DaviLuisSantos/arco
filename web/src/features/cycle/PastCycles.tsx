import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
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
  const { t } = useTranslation('cycle')
  const [open, setOpen] = useState(false)
  const query = useQuery({ queryKey: queryKeys.cycleArch(cycle.id), queryFn: () => cyclesApi.arch(cycle.id) })

  return (
    <>
      <button
        type="button"
        onClick={() => setOpen(true)}
        aria-label={t('past.viewArch', { name: cycle.name })}
        className="mt-3 block w-full rounded-xl border border-border bg-surface-2/50 p-2 hover:border-accent focus-visible:border-accent focus-visible:outline-none"
      >
        {query.data ? (
          <span className="mx-auto block max-w-56">
            <Arch arch={query.data} small />
          </span>
        ) : (
          <span className="block py-3 text-center text-xs text-muted">
            {query.isLoadingError ? t('past.archError') : t('past.archLoading')}
          </span>
        )}
      </button>
      <Dialog open={open} onClose={() => setOpen(false)} title={cycle.name} description={t('past.archDescription')}>
        <ArchPanel cycleId={cycle.id} cycle={cycle} />
      </Dialog>
    </>
  )
}

export function PastCycles({ cycles }: { cycles: Cycle[] }) {
  const { t } = useTranslation('cycle')
  const past = cycles.filter((c) => c.status === 'Completed')
  if (past.length === 0) return null

  return (
    <section aria-labelledby="past-title">
      <h2 id="past-title" className="mb-2 text-sm font-semibold uppercase tracking-wide text-muted">
        {t('past.title')}
      </h2>
      <ul className="flex flex-col gap-2">
        {past.map((c) => (
          <li key={c.id}>
            <Card>
              <h3 className="font-semibold">{c.name}</h3>
              <p className="text-xs text-muted">
                {formatFullDate(c.startDate)} – {formatFullDate(c.endDate)} · {t('past.days', { count: c.totalDays })}
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
                <p className="mt-2 text-sm text-muted italic">{t('past.noReview')}</p>
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
