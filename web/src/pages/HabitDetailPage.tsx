import { useQuery } from '@tanstack/react-query'
import { Archive, ArchiveRestore, ArrowLeft, ChevronsUp, Flag, Pencil } from 'lucide-react'
import { useState } from 'react'
import { Link, useParams } from 'react-router'
import { ApiError } from '../api/client'
import { goalsApi, habitsApi, todayApi } from '../api/endpoints'
import { Button } from '../components/ui/Button'
import { Card } from '../components/ui/Card'
import { ConfirmDialog } from '../components/ui/ConfirmDialog'
import { EmptyState, ErrorState, Spinner } from '../components/ui/States'
import { CheckInGrid } from '../features/habits/CheckInGrid'
import { HabitFormDialog } from '../features/habits/HabitFormDialog'
import { LevelTimeline } from '../features/habits/LevelTimeline'
import { LevelUpDialog } from '../features/habits/LevelUpDialog'
import { localToday } from '../lib/dates'
import { frequencyText } from '../lib/format'
import { queryKeys } from '../lib/queryKeys'
import { useAppMutation } from '../lib/useAppMutation'

type Modal = 'edit' | 'levelUp' | 'archive' | null

export function HabitDetailPage() {
  const id = Number(useParams().id)
  const habit = useQuery({ queryKey: queryKeys.habit(id), queryFn: () => habitsApi.get(id), enabled: Number.isInteger(id) })
  const today = useQuery({ queryKey: queryKeys.today, queryFn: todayApi.get, select: (t) => t.date })
  const goals = useQuery({ queryKey: queryKeys.currentGoals, queryFn: () => goalsApi.list() })
  const [modal, setModal] = useState<Modal>(null)
  const close = () => setModal(null)

  const archive = useAppMutation(() => habitsApi.archive(id), { successMessage: 'Hábito arquivado.', onSuccess: close })
  const restore = useAppMutation(() => habitsApi.restore(id), { successMessage: 'Hábito restaurado.' })

  const back = (
    <Link to="/habits" className="mb-4 inline-flex items-center gap-1 text-sm text-muted hover:text-text">
      <ArrowLeft className="size-4" aria-hidden />
      Hábitos
    </Link>
  )

  if (habit.isPending && Number.isInteger(id)) return <Spinner />
  if (!Number.isInteger(id) || (habit.error instanceof ApiError && habit.error.status === 404)) {
    return (
      <div>
        {back}
        <EmptyState icon={<Flag className="size-6" />} title="Hábito não encontrado" description="Ele pode ter sido removido." />
      </div>
    )
  }
  if (habit.isError) return <ErrorState error={habit.error} onRetry={() => void habit.refetch()} />
  if (!habit.data) return null

  const h = habit.data
  const archived = h.archivedAt !== null
  const goal = goals.data?.find((g) => g.id === h.goalId)

  return (
    <div className="flex flex-col gap-5">
      <div>
        {back}
        <header className="flex items-start gap-3">
          <span className="grid size-14 shrink-0 place-items-center rounded-2xl bg-surface-2 text-3xl" aria-hidden>
            {h.emoji || '•'}
          </span>
          <div className="min-w-0 flex-1">
            <h1 className="text-2xl font-extrabold tracking-tight break-words">{h.name}</h1>
            <p className="text-sm text-muted">
              Nível {h.level} · {frequencyText(h)}
              {archived && ' · arquivado'}
            </p>
            {goal && (
              <span className="mt-1 inline-flex items-center gap-1 rounded-full bg-accent/15 px-2 py-0.5 text-xs font-medium text-accent">
                <Flag className="size-3" aria-hidden />
                {goal.title}
              </span>
            )}
          </div>
          <Button variant="ghost" size="sm" onClick={() => setModal('edit')} aria-label="Editar hábito">
            <Pencil className="size-4" aria-hidden />
          </Button>
        </header>
      </div>

      <Card className="flex flex-col gap-3">
        <div>
          <p className="text-xs font-semibold uppercase tracking-wide text-muted">Dose atual</p>
          <p className="text-lg font-semibold">{h.levelDescription}</p>
          <p className="text-sm text-muted">Mínimo: {h.minimumDescription}</p>
        </div>
        {!archived && (
          <Button onClick={() => setModal('levelUp')}>
            <ChevronsUp className="size-4" aria-hidden />
            Subir de nível <span className="font-bold opacity-80">+50 XP</span>
          </Button>
        )}
      </Card>

      <section aria-labelledby="grid-title">
        <h2 id="grid-title" className="mb-2 text-sm font-semibold uppercase tracking-wide text-muted">
          Últimos 28 dias
        </h2>
        <Card>
          <CheckInGrid checkIns={h.recentCheckIns} today={today.data ?? localToday()} />
        </Card>
      </section>

      <section aria-labelledby="levels-title">
        <h2 id="levels-title" className="mb-2 text-sm font-semibold uppercase tracking-wide text-muted">
          Níveis
        </h2>
        <Card>
          <LevelTimeline levels={h.levels} current={h.level} />
        </Card>
      </section>

      {archived ? (
        <Button variant="secondary" loading={restore.isPending} onClick={() => restore.mutate()}>
          <ArchiveRestore className="size-4" aria-hidden />
          Restaurar hábito
        </Button>
      ) : (
        <Button variant="ghost" onClick={() => setModal('archive')}>
          <Archive className="size-4" aria-hidden />
          Arquivar hábito
        </Button>
      )}

      <HabitFormDialog open={modal === 'edit'} onClose={close} habit={h} />
      <LevelUpDialog open={modal === 'levelUp'} onClose={close} habit={h} />
      <ConfirmDialog
        open={modal === 'archive'}
        onClose={close}
        onConfirm={() => archive.mutate()}
        loading={archive.isPending}
        title="Arquivar hábito?"
        description="Ele sai da tela Hoje, mas o histórico e o XP continuam. Você pode restaurar depois."
        confirmLabel="Arquivar"
      />
    </div>
  )
}
