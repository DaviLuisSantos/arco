import { useQuery } from '@tanstack/react-query'
import { Archive, ArchiveRestore, ArrowLeft, Bell, ChevronsDown, ChevronsUp, Flag, Pencil } from 'lucide-react'
import { useState } from 'react'
import { Link, useParams } from 'react-router'
import { ApiError } from '../api/client'
import { goalsApi, habitsApi, todayApi } from '../api/endpoints'
import { Button } from '../components/ui/Button'
import { Card } from '../components/ui/Card'
import { ConfirmDialog } from '../components/ui/ConfirmDialog'
import { EmptyState, ErrorState, Spinner } from '../components/ui/States'
import { HabitHistory } from '../features/habits/HabitHistory'
import { HabitFormDialog } from '../features/habits/HabitFormDialog'
import { LevelTimeline } from '../features/habits/LevelTimeline'
import { LevelUpDialog } from '../features/habits/LevelUpDialog'
import { addDays, localDateOf, localToday } from '../lib/dates'
import { formatPercent, frequencyText, streakText } from '../lib/format'
import { queryKeys } from '../lib/queryKeys'
import { useAppMutation } from '../lib/useAppMutation'

type Modal = 'edit' | 'levelUp' | 'levelDown' | 'archive' | null

export function HabitDetailPage() {
  const id = Number(useParams().id)
  const habit = useQuery({ queryKey: queryKeys.habit(id), queryFn: () => habitsApi.get(id), enabled: Number.isInteger(id) })
  const today = useQuery({ queryKey: queryKeys.today, queryFn: todayApi.get, select: (t) => t.date })
  const goals = useQuery({ queryKey: queryKeys.currentGoals, queryFn: () => goalsApi.list() })
  const [modal, setModal] = useState<Modal>(null)
  const close = () => setModal(null)

  const archive = useAppMutation(() => habitsApi.archive(id), { successMessage: 'Hábito arquivado.', onSuccess: close })
  const dismiss = useAppMutation(() => habitsApi.dismissLevelUpSuggestion(id), {
    successMessage: 'Sugestão adiada por 7 dias.',
  })
  const restore = useAppMutation(() => habitsApi.restore(id), { successMessage: 'Hábito restaurado.' })
  const levelDown = useAppMutation(() => habitsApi.levelDown(id), {
    successMessage: 'Voltou um nível. O degrau continua na escada.',
    onSuccess: close,
  })
  const dismissDown = useAppMutation(() => habitsApi.dismissLevelDownSuggestion(id), {
    successMessage: 'Sugestão adiada por 7 dias.',
  })

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
  if (habit.isLoadingError) return <ErrorState error={habit.error} onRetry={() => void habit.refetch()} />
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

      {h.levelUpSuggested && (
        <Card className="flex flex-col gap-3 border-accent/60 bg-accent/10">
          <div>
            <p className="flex items-center gap-1.5 font-semibold text-accent">
              <ChevronsUp className="size-4" aria-hidden />
              Pronto para o nível {h.level + 1}?
            </p>
            <p className="mt-1 text-sm text-muted">
              {h.levelUpWeeksDone !== null ? (
                <>
                  Você cumpriu {h.levelUpWeeksDone} semanas seguidas com {h.levelUpDaysPerWeek}+ dias.
                </>
              ) : (
                <>
                  Você fez a dose completa {h.consistency.done} de {h.consistency.expected} vezes nos últimos 14 dias (
                  {formatPercent(h.consistency.rate)}). Parece que “{h.levelDescription}” ficou fácil.
                </>
              )}
              {h.plannedLevels[0] && <> Próximo degrau: “{h.plannedLevels[0].description}”.</>}
            </p>
          </div>
          <div className="flex gap-2">
            <Button variant="secondary" block loading={dismiss.isPending} onClick={() => dismiss.mutate()}>
              Agora não
            </Button>
            <Button block onClick={() => setModal('levelUp')}>
              Subir de nível
            </Button>
          </div>
        </Card>
      )}

      {h.levelDownSuggested && (
        <Card className="flex flex-col gap-3">
          <div>
            <p className="flex items-center gap-1.5 font-semibold">
              <ChevronsDown className="size-4 text-muted" aria-hidden />
              Que tal voltar ao nível {h.level - 1}?
            </p>
            <p className="mt-1 text-sm text-muted">
              Nas duas últimas semanas você ficou abaixo da metade da meta. Voltar um degrau mantém a constância;
              “{h.levelDescription}” volta para a escada e você sobe de novo quando estiver firme.
            </p>
          </div>
          <div className="flex gap-2">
            <Button variant="secondary" block loading={dismissDown.isPending} onClick={() => dismissDown.mutate()}>
              Agora não
            </Button>
            <Button variant="secondary" block onClick={() => setModal('levelDown')}>
              Voltar ao nível {h.level - 1}
            </Button>
          </div>
        </Card>
      )}

      <Card className="flex flex-col gap-3">
        <div>
          <p className="text-xs font-semibold uppercase tracking-wide text-muted">Dose atual</p>
          <p className="text-lg font-semibold">{h.levelDescription}</p>
          <p className="text-sm text-muted">Mínimo: {h.minimumDescription}</p>
          {!archived && (
            <div className="mt-3 flex flex-wrap gap-1.5 text-xs">
              <span
                className="rounded-full bg-surface-2 px-2.5 py-1 text-muted"
                title="Feito e Mínimo contam. Uma falha isolada por semana é perdoada; duas seguidas, ou duas na mesma semana, zeram."
              >
                <span aria-hidden>🔥 </span>
                Sequência: <span className="font-semibold text-text">{streakText(h.streak)}</span>
                {h.streak.best > h.streak.current && ` · recorde ${streakText({ ...h.streak, current: h.streak.best })}`}
              </span>
              {h.levelUpWeeksDone !== null && h.levelUpWeeks !== null ? (
                <span
                  className="rounded-full bg-surface-2 px-2.5 py-1 text-muted"
                  title={`Sobe depois de ${h.levelUpWeeks} semanas seguidas com ${h.levelUpDaysPerWeek}+ dias (Feito e Mínimo contam).`}
                >
                  Semanas com {h.levelUpDaysPerWeek}+ dias:{' '}
                  <span className="font-semibold text-text">
                    {Math.min(h.levelUpWeeksDone, h.levelUpWeeks)}/{h.levelUpWeeks}
                  </span>
                </span>
              ) : (
                <span
                  className="rounded-full bg-surface-2 px-2.5 py-1 text-muted"
                  title="Só “Feito” conta para a sugestão de subir de nível."
                >
                  14 dias:{' '}
                  <span className="font-semibold text-text">
                    {h.consistency.done}/{h.consistency.expected} · {formatPercent(h.consistency.rate)}
                  </span>
                </span>
              )}
              <span className="inline-flex items-center gap-1 rounded-full bg-surface-2 px-2.5 py-1 text-muted">
                <Bell className="size-3" aria-hidden />
                {h.reminderTime ? `Lembrete às ${h.reminderTime}` : 'Sem lembrete'}
              </span>
            </div>
          )}
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
          Histórico
        </h2>
        <Card>
          <HabitHistory
            habitId={h.id}
            today={today.data ?? localToday()}
            // Check-ins podem ser marcados até 7 dias antes da criação do hábito.
            firstDay={addDays(localDateOf(h.createdAt), -7)}
            editable={!archived}
          />
        </Card>
      </section>

      <section aria-labelledby="levels-title">
        <h2 id="levels-title" className="mb-2 text-sm font-semibold uppercase tracking-wide text-muted">
          Níveis
        </h2>
        <Card>
          <LevelTimeline levels={h.levels} planned={h.plannedLevels} current={h.level} />
          {!archived && h.level > 1 && (
            <Button variant="ghost" size="sm" className="mt-3" onClick={() => setModal('levelDown')}>
              <ChevronsDown className="size-4" aria-hidden />
              Voltar ao nível {h.level - 1}
            </Button>
          )}
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
        open={modal === 'levelDown'}
        onClose={close}
        onConfirm={() => levelDown.mutate()}
        loading={levelDown.isPending}
        title={`Voltar ao nível ${h.level - 1}?`}
        description={`“${h.levelDescription}” sai do nível atual e volta para o topo da escada planejada. O XP que você ganhou continua.`}
        confirmLabel={`Voltar ao nível ${h.level - 1}`}
      />
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
