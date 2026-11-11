import { useQuery } from '@tanstack/react-query'
import { Flag, Plus } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { cyclesApi, goalsApi, todayApi } from '../api/endpoints'
import type { Cycle } from '../api/types'
import { Button } from '../components/ui/Button'
import { Card } from '../components/ui/Card'
import { PageHeader } from '../components/ui/PageHeader'
import { Skeleton } from '../components/ui/Skeleton'
import { EmptyState, ErrorState, Spinner } from '../components/ui/States'
import { CompleteCycleDialog } from '../features/cycle/CompleteCycleDialog'
import { DeleteCycleButton } from '../features/cycle/DeleteCycleButton'
import { CreateCycleForm } from '../features/cycle/CreateCycleForm'
import { PastCycles } from '../features/cycle/PastCycles'
import { ArchPanel } from '../features/arch/ArchPanel'
import { CreateGoalDialog } from '../features/goals/CreateGoalDialog'
import { GoalCard } from '../features/goals/GoalCard'
import { diffDays, formatFullDate, localToday } from '../lib/dates'
import { queryKeys } from '../lib/queryKeys'

/** Recomendação de UI (não é regra do backend). */
const RECOMMENDED_MAX_GOALS = 3

export function CyclePage() {
  const { t } = useTranslation('pages', { keyPrefix: 'cycle' })
  const { t: tp } = useTranslation('progress')
  const current = useQuery({ queryKey: queryKeys.currentCycle, queryFn: cyclesApi.current })
  const cycles = useQuery({ queryKey: queryKeys.cycles, queryFn: cyclesApi.list })
  const today = useQuery({ queryKey: queryKeys.today, queryFn: todayApi.get, select: (t) => t.date })
  const todayDate = today.data ?? localToday()

  if (current.isPending) return <Spinner />
  if (current.isLoadingError) return <ErrorState error={current.error} onRetry={() => void current.refetch()} />

  return (
    <div className="flex flex-col gap-5">
      {current.data ? (
        <ActiveCycle cycle={current.data} today={todayDate} />
      ) : (
        <>
          <PageHeader title={t('title')} subtitle={t('none')} />
          {today.isPending ? <Spinner /> : <CreateCycleForm today={todayDate} />}
        </>
      )}
      {cycles.isLoadingError && (
        <div role="alert" className="flex items-center justify-between gap-2 rounded-2xl border border-danger/40 bg-danger/10 p-3 text-sm">
          <span>{tp('secondaryError', { what: tp('what.cycles') })}</span>
          <Button size="sm" variant="secondary" onClick={() => void cycles.refetch()}>
            {tp('retry')}
          </Button>
        </div>
      )}
      {cycles.data && <PastCycles cycles={cycles.data} />}
    </div>
  )
}

function ActiveCycle({ cycle, today }: { cycle: Cycle; today: string }) {
  const { t } = useTranslation('pages', { keyPrefix: 'cycle' })
  const goals = useQuery({ queryKey: queryKeys.goals(cycle.id), queryFn: () => goalsApi.list(cycle.id) })
  const [modal, setModal] = useState<'goal' | 'complete' | null>(null)
  const close = () => setModal(null)
  const goalCount = goals.data?.length ?? 0
  const over = diffDays(cycle.endDate, today) >= 0

  return (
    <>
      <PageHeader
        title={cycle.name}
        subtitle={`${formatFullDate(cycle.startDate)} – ${formatFullDate(cycle.endDate)} · ${t('dayOf', { day: cycle.dayNumber, total: cycle.totalDays })}`}
      />

      <Card>
        <h2 id="cycle-arch-title" className="mb-3 text-sm font-semibold uppercase tracking-wide text-muted">
          {t('arch')}
        </h2>
        <ArchPanel cycleId={cycle.id} cycle={cycle} />
      </Card>

      {over && (
        <Card className="flex flex-col gap-3 border-accent/60 bg-accent/10">
          <div>
            <p className="font-semibold">
              {diffDays(cycle.endDate, today) === 0 ? t('lastDay') : t('ended')}
            </p>
            <p className="mt-1 text-sm text-muted">
              {t('endHint')} <span className="font-semibold text-xp">+100 XP</span>
            </p>
          </div>
          <Button onClick={() => setModal('complete')}>{t('doReview')}</Button>
        </Card>
      )}

      <section aria-labelledby="cycle-goals-title" className="flex flex-col gap-3">
        <div className="flex items-center justify-between">
          <h2 id="cycle-goals-title" className="text-sm font-semibold uppercase tracking-wide text-muted">
            {t('goals', { count: goalCount, max: RECOMMENDED_MAX_GOALS })}
          </h2>
          {goalCount > 0 && (
            <Button size="sm" variant="secondary" onClick={() => setModal('goal')}>
              <Plus className="size-4" aria-hidden />
              {t('goalBtn')}
            </Button>
          )}
        </div>
        {goalCount >= RECOMMENDED_MAX_GOALS && (
          <p className="text-xs text-muted">{t('goalsRecommended')}</p>
        )}

        {goals.isPending ? (
          <Skeleton className="h-24 w-full" />
        ) : goals.isLoadingError ? (
          <ErrorState error={goals.error} onRetry={() => void goals.refetch()} />
        ) : goalCount === 0 ? (
          <EmptyState
            icon={<Flag className="size-6" />}
            title={t('noGoalsTitle')}
            description={t('noGoalsDescription')}
            action={
              <Button onClick={() => setModal('goal')}>
                <Plus className="size-4" aria-hidden />
                {t('createGoal')}
              </Button>
            }
          />
        ) : (
          goals.data.map((g) => <GoalCard key={g.id} goal={g} today={today} />)
        )}
      </section>

      {!over && (
        <Button variant="secondary" onClick={() => setModal('complete')}>
          {t('endCycle')}
        </Button>
      )}
      <div className="flex justify-center">
        <DeleteCycleButton cycle={cycle} />
      </div>

      <CreateGoalDialog cycle={cycle} open={modal === 'goal'} onClose={close} />
      <CompleteCycleDialog cycle={cycle} open={modal === 'complete'} onClose={close} />
    </>
  )
}
