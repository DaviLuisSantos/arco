import { useQuery } from '@tanstack/react-query'
import { Flag, Plus } from 'lucide-react'
import { useState } from 'react'
import { cyclesApi, goalsApi, todayApi } from '../api/endpoints'
import type { Cycle } from '../api/types'
import { Button } from '../components/ui/Button'
import { Card } from '../components/ui/Card'
import { PageHeader } from '../components/ui/PageHeader'
import { ProgressBar } from '../components/ui/ProgressBar'
import { EmptyState, ErrorState, Spinner } from '../components/ui/States'
import { CompleteCycleDialog } from '../features/cycle/CompleteCycleDialog'
import { CreateCycleForm } from '../features/cycle/CreateCycleForm'
import { PastCycles } from '../features/cycle/PastCycles'
import { CreateGoalDialog } from '../features/goals/CreateGoalDialog'
import { GoalCard } from '../features/goals/GoalCard'
import { formatFullDate, localToday } from '../lib/dates'
import { queryKeys } from '../lib/queryKeys'

/** Recomendação de UI (não é regra do backend). */
const RECOMMENDED_MAX_GOALS = 3

export function CyclePage() {
  const current = useQuery({ queryKey: queryKeys.currentCycle, queryFn: cyclesApi.current })
  const cycles = useQuery({ queryKey: queryKeys.cycles, queryFn: cyclesApi.list })
  const today = useQuery({ queryKey: queryKeys.today, queryFn: todayApi.get, select: (t) => t.date })
  const todayDate = today.data ?? localToday()

  if (current.isPending) return <Spinner />
  if (current.isError) return <ErrorState error={current.error} onRetry={() => void current.refetch()} />

  return (
    <div className="flex flex-col gap-5">
      {current.data ? (
        <ActiveCycle cycle={current.data} today={todayDate} />
      ) : (
        <>
          <PageHeader title="Ciclo" subtitle="Nenhum ciclo ativo" />
          {today.isPending ? <Spinner /> : <CreateCycleForm today={todayDate} />}
        </>
      )}
      {cycles.data && <PastCycles cycles={cycles.data} />}
    </div>
  )
}

function ActiveCycle({ cycle, today }: { cycle: Cycle; today: string }) {
  const goals = useQuery({ queryKey: queryKeys.goals(cycle.id), queryFn: () => goalsApi.list(cycle.id) })
  const [modal, setModal] = useState<'goal' | 'complete' | null>(null)
  const close = () => setModal(null)
  const goalCount = goals.data?.length ?? 0

  return (
    <>
      <PageHeader
        title={cycle.name}
        subtitle={`${formatFullDate(cycle.startDate)} – ${formatFullDate(cycle.endDate)}`}
      />

      <Card>
        <div className="mb-2 flex items-baseline justify-between text-sm">
          <span className="font-semibold">
            Dia {cycle.dayNumber} de {cycle.totalDays}
          </span>
          <span className="text-muted">{Math.round((cycle.dayNumber / Math.max(1, cycle.totalDays)) * 100)}%</span>
        </div>
        <ProgressBar value={cycle.dayNumber / Math.max(1, cycle.totalDays)} label="Progresso do ciclo" />
      </Card>

      <section aria-labelledby="cycle-goals-title" className="flex flex-col gap-3">
        <div className="flex items-center justify-between">
          <h2 id="cycle-goals-title" className="text-sm font-semibold uppercase tracking-wide text-muted">
            Objetivos ({goalCount}/{RECOMMENDED_MAX_GOALS})
          </h2>
          {goalCount > 0 && (
            <Button size="sm" variant="secondary" onClick={() => setModal('goal')}>
              <Plus className="size-4" aria-hidden />
              Objetivo
            </Button>
          )}
        </div>
        {goalCount >= RECOMMENDED_MAX_GOALS && (
          <p className="text-xs text-muted">Até 3 objetivos é o recomendado para manter o foco.</p>
        )}

        {goals.isPending ? (
          <Spinner />
        ) : goals.isError ? (
          <ErrorState error={goals.error} onRetry={() => void goals.refetch()} />
        ) : goalCount === 0 ? (
          <EmptyState
            icon={<Flag className="size-6" />}
            title="Nenhum objetivo neste ciclo"
            description="Defina de 1 a 3 objetivos com prazo. Seus hábitos passam a empurrar algo maior."
            action={
              <Button onClick={() => setModal('goal')}>
                <Plus className="size-4" aria-hidden />
                Criar objetivo
              </Button>
            }
          />
        ) : (
          goals.data.map((g) => <GoalCard key={g.id} goal={g} today={today} />)
        )}
      </section>

      <Button variant="secondary" size="lg" onClick={() => setModal('complete')}>
        Encerrar ciclo
      </Button>

      <CreateGoalDialog cycle={cycle} open={modal === 'goal'} onClose={close} />
      <CompleteCycleDialog cycle={cycle} open={modal === 'complete'} onClose={close} />
    </>
  )
}
