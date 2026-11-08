import { useMutation, useQuery } from '@tanstack/react-query'
import { Archive, ArchiveRestore, ArrowLeft, Bell, ChevronsDown, ChevronsUp, Flag, Pencil } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router'
import { ApiError } from '../api/client'
import { goalsApi, habitsApi, todayApi } from '../api/endpoints'
import { Button } from '../components/ui/Button'
import { Card } from '../components/ui/Card'
import { ConfirmDialog } from '../components/ui/ConfirmDialog'
import { EmptyState, ErrorState, Spinner } from '../components/ui/States'
import { FocusTimer } from '../features/focus/FocusTimer'
import { HabitHistory } from '../features/habits/HabitHistory'
import { HabitFormDialog } from '../features/habits/HabitFormDialog'
import { LevelTimeline } from '../features/habits/LevelTimeline'
import { LevelUpDialog } from '../features/habits/LevelUpDialog'
import { addDays, localDateOf, localToday } from '../lib/dates'
import { formatPercent, frequencyText, streakText } from '../lib/format'
import { queryKeys } from '../lib/queryKeys'
import { errorMessage } from '../lib/errors'
import { useInvalidateData } from '../lib/useInvalidateData'
import { useToast } from '../components/ui/toast-context'
import { usePlanLimitHandler } from '../features/billing'
import { useAppMutation } from '../lib/useAppMutation'

type Modal = 'edit' | 'levelUp' | 'levelDown' | 'archive' | null

export function HabitDetailPage() {
  const { t } = useTranslation('pages', { keyPrefix: 'habitDetail' })
  const id = Number(useParams().id)
  const habit = useQuery({ queryKey: queryKeys.habit(id), queryFn: () => habitsApi.get(id), enabled: Number.isInteger(id) })
  const today = useQuery({ queryKey: queryKeys.today, queryFn: todayApi.get, select: (t) => t.date })
  const goals = useQuery({ queryKey: queryKeys.currentGoals, queryFn: () => goalsApi.list() })
  const toast = useToast()
  const invalidate = useInvalidateData()
  const handlePlanLimit = usePlanLimitHandler()
  const [modal, setModal] = useState<Modal>(null)
  const close = () => setModal(null)

  const archive = useAppMutation(() => habitsApi.archive(id), { successMessage: t('archivedSuccess'), onSuccess: close })
  const dismiss = useAppMutation(() => habitsApi.dismissLevelUpSuggestion(id), {
    successMessage: t('snoozed'),
  })
  const restore = useMutation({
    mutationFn: () => habitsApi.restore(id),
    onSuccess: () => toast.success(t('restored')),
    // Limite do plano: aviso com link para o Plano; outros erros, o toast genérico.
    onError: (err) => {
      if (!handlePlanLimit(err)) toast.error(errorMessage(err))
    },
    onSettled: () => invalidate(),
  })
  const levelDown = useAppMutation(() => habitsApi.levelDown(id), {
    successMessage: t('levelDownSuccess'),
    onSuccess: close,
  })
  const dismissDown = useAppMutation(() => habitsApi.dismissLevelDownSuggestion(id), {
    successMessage: t('snoozed'),
  })

  const back = (
    <Link to="/habits" className="mb-4 inline-flex items-center gap-1 text-sm text-muted hover:text-text">
      <ArrowLeft className="size-4" aria-hidden />
      {t('back')}
    </Link>
  )

  if (habit.isPending && Number.isInteger(id)) return <Spinner />
  if (!Number.isInteger(id) || (habit.error instanceof ApiError && habit.error.status === 404)) {
    return (
      <div>
        {back}
        <EmptyState icon={<Flag className="size-6" />} title={t('notFound')} description={t('notFoundDescription')} />
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
              {t('levelLine', { level: h.level, frequency: frequencyText(h) })}
              {archived && t('archivedSuffix')}
            </p>
            {goal && (
              <span className="mt-1 inline-flex items-center gap-1 rounded-full bg-accent/15 px-2 py-0.5 text-xs font-medium text-accent">
                <Flag className="size-3" aria-hidden />
                {goal.title}
              </span>
            )}
          </div>
          <Button variant="ghost" size="sm" onClick={() => setModal('edit')} aria-label={t('edit')}>
            <Pencil className="size-4" aria-hidden />
          </Button>
        </header>
      </div>

      {h.levelUpSuggested && (
        <Card className="flex flex-col gap-3 border-accent/60 bg-accent/10">
          <div>
            <p className="flex items-center gap-1.5 font-semibold text-accent">
              <ChevronsUp className="size-4" aria-hidden />
              {t('readyFor', { level: h.level + 1 })}
            </p>
            <p className="mt-1 text-sm text-muted">
              {h.levelUpWeeksDone !== null ? (
                t('weeksDone', { weeks: h.levelUpWeeksDone, days: h.levelUpDaysPerWeek })
              ) : (
                t('easy', {
                  done: h.consistency.done,
                  expected: h.consistency.expected,
                  rate: formatPercent(h.consistency.rate),
                  description: h.levelDescription,
                })
              )}
              {h.plannedLevels[0] && t('nextStep', { description: h.plannedLevels[0].description })}
            </p>
          </div>
          <div className="flex gap-2">
            <Button variant="secondary" block loading={dismiss.isPending} onClick={() => dismiss.mutate()}>
              {t('notNow')}
            </Button>
            <Button block onClick={() => setModal('levelUp')}>
              {t('levelUp')}
            </Button>
          </div>
        </Card>
      )}

      {h.levelDownSuggested && (
        <Card className="flex flex-col gap-3">
          <div>
            <p className="flex items-center gap-1.5 font-semibold">
              <ChevronsDown className="size-4 text-muted" aria-hidden />
              {t('backTo', { level: h.level - 1 })}
            </p>
            <p className="mt-1 text-sm text-muted">
              {t('backToText', { description: h.levelDescription })}
            </p>
          </div>
          <div className="flex gap-2">
            <Button variant="secondary" block loading={dismissDown.isPending} onClick={() => dismissDown.mutate()}>
              {t('notNow')}
            </Button>
            <Button variant="secondary" block onClick={() => setModal('levelDown')}>
              {t('backToButton', { level: h.level - 1 })}
            </Button>
          </div>
        </Card>
      )}

      <Card className="flex flex-col gap-3">
        <div>
          <p className="text-xs font-semibold uppercase tracking-wide text-muted">{t('currentDose')}</p>
          <p className="text-lg font-semibold">{h.levelDescription}</p>
          <p className="text-sm text-muted">{t('minimum', { description: h.minimumDescription })}</p>
          {!archived && (
            <div className="mt-3 flex flex-wrap gap-1.5 text-xs">
              <span
                className="rounded-full bg-surface-2 px-2.5 py-1 text-muted"
                title={t('streakTitle')}
              >
                <span aria-hidden>🔥 </span>
                {t('streak')} <span className="font-semibold text-text">{streakText(h.streak)}</span>
                {h.streak.best > h.streak.current &&
                  t('record', { value: streakText({ ...h.streak, current: h.streak.best }) })}
              </span>
              {h.levelUpWeeksDone !== null && h.levelUpWeeks !== null ? (
                <span
                  className="rounded-full bg-surface-2 px-2.5 py-1 text-muted"
                  title={t('weeksTitle', { weeks: h.levelUpWeeks, days: h.levelUpDaysPerWeek })}
                >
                  {t('weeksWith', { days: h.levelUpDaysPerWeek })}{' '}
                  <span className="font-semibold text-text">
                    {Math.min(h.levelUpWeeksDone, h.levelUpWeeks)}/{h.levelUpWeeks}
                  </span>
                </span>
              ) : (
                <span
                  className="rounded-full bg-surface-2 px-2.5 py-1 text-muted"
                  title={t('days14Title')}
                >
                  {t('days14')}{' '}
                  <span className="font-semibold text-text">
                    {h.consistency.done}/{h.consistency.expected} · {formatPercent(h.consistency.rate)}
                  </span>
                </span>
              )}
              <span className="inline-flex items-center gap-1 rounded-full bg-surface-2 px-2.5 py-1 text-muted">
                <Bell className="size-3" aria-hidden />
                {h.reminderTime ? t('reminderAt', { time: h.reminderTime }) : t('noReminder')}
              </span>
            </div>
          )}
        </div>
        {!archived && (
          <Button onClick={() => setModal('levelUp')}>
            <ChevronsUp className="size-4" aria-hidden />
            {t('levelUp')} <span className="font-bold opacity-80">+50 XP</span>
          </Button>
        )}
      </Card>

      {!archived && (
        <section aria-labelledby="focus-title">
          <h2 id="focus-title" className="mb-2 text-sm font-semibold uppercase tracking-wide text-muted">
            {t('focus')}
          </h2>
          <Card>
            <FocusTimer key={h.id} habitId={h.id} emoji={h.emoji} name={h.name} />
          </Card>
        </section>
      )}

      <section aria-labelledby="grid-title">
        <h2 id="grid-title" className="mb-2 text-sm font-semibold uppercase tracking-wide text-muted">
          {t('history')}
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
          {t('levels')}
        </h2>
        <Card>
          <LevelTimeline levels={h.levels} planned={h.plannedLevels} current={h.level} />
          {!archived && h.level > 1 && (
            <Button variant="ghost" size="sm" className="mt-3" onClick={() => setModal('levelDown')}>
              <ChevronsDown className="size-4" aria-hidden />
              {t('backToButton', { level: h.level - 1 })}
            </Button>
          )}
        </Card>
      </section>

      {archived ? (
        <Button variant="secondary" loading={restore.isPending} onClick={() => restore.mutate()}>
          <ArchiveRestore className="size-4" aria-hidden />
          {t('restore')}
        </Button>
      ) : (
        <Button variant="ghost" onClick={() => setModal('archive')}>
          <Archive className="size-4" aria-hidden />
          {t('archive')}
        </Button>
      )}

      <HabitFormDialog open={modal === 'edit'} onClose={close} habit={h} />
      <LevelUpDialog open={modal === 'levelUp'} onClose={close} habit={h} />
      <ConfirmDialog
        open={modal === 'levelDown'}
        onClose={close}
        onConfirm={() => levelDown.mutate()}
        loading={levelDown.isPending}
        title={t('levelDownTitle', { level: h.level - 1 })}
        description={t('levelDownDescription', { description: h.levelDescription })}
        confirmLabel={t('backToButton', { level: h.level - 1 })}
      />
      <ConfirmDialog
        open={modal === 'archive'}
        onClose={close}
        onConfirm={() => archive.mutate()}
        loading={archive.isPending}
        title={t('archiveTitle')}
        description={t('archiveDescription')}
        confirmLabel={t('archiveConfirm')}
      />
    </div>
  )
}
