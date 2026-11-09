import { useQuery } from '@tanstack/react-query'
import { ChevronDown, Plus, Sprout } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { todayApi } from '../api/endpoints'
import { Card } from '../components/ui/Card'
import { ProgressBar } from '../components/ui/ProgressBar'
import { Skeleton } from '../components/ui/Skeleton'
import { EmptyState, ErrorState } from '../components/ui/States'
import { DayStone } from '../features/arch/DayStone'
import { HabitCheckInCard } from '../features/checkins/HabitCheckInCard'
import { CycleStrip } from '../features/cycle/CycleStrip'
import { GoalMiniCard } from '../features/goals/GoalMiniCard'
import { FirstSessionChecklist } from '../features/onboarding/FirstSessionChecklist'
import { PushPrimingCard } from '../features/onboarding/PushPrimingCard'
import { TemplateShortcuts } from '../features/onboarding/TemplateShortcuts'
import { PlayerProgress } from '../features/player/PlayerProgress'
import { TodayNudges } from '../features/reviews/TodayNudges'
import { formatLongDate } from '../lib/dates'
import { queryKeys } from '../lib/queryKeys'

export function TodayPage() {
  const { t } = useTranslation('pages', { keyPrefix: 'today' })
  const today = useQuery({ queryKey: queryKeys.today, queryFn: todayApi.get })

  if (today.isPending) return <TodaySkeleton />
  if (today.isLoadingError) return <ErrorState error={today.error} onRetry={() => void today.refetch()} />

  const { date, player, xpToday, cycle, goals, habits } = today.data
  // Hábito já marcado hoje continua na lista principal (ex.: meta semanal recém-batida).
  const dueHabits = habits.filter((h) => h.isDueToday || h.checkIn !== null)
  const otherHabits = habits.filter((h) => !h.isDueToday && h.checkIn === null)
  const pending = dueHabits.filter((h) => h.checkIn === null).length
  const doneCount = dueHabits.length - pending

  return (
    <div className="flex flex-col gap-5">
      <header>
        <p className="text-sm text-muted first-letter:uppercase">{formatLongDate(date)}</p>
        <h1 className="text-2xl font-extrabold tracking-tight">{t('title')}</h1>
      </header>

      <Card className="p-3!">
        <PlayerProgress player={player} xpToday={xpToday} compact />
      </Card>

      <section aria-labelledby="habits-title">
        <div className="mb-2 flex items-baseline justify-between">
          <h2 id="habits-title" className="text-sm font-semibold uppercase tracking-wide text-muted">
            {t('habitsToday')}
          </h2>
          {dueHabits.length > 0 && (
            <span className="text-sm text-muted">
              {pending === 0 ? t('allDone') : `${t('dayCount', { done: doneCount, total: dueHabits.length })} · ${t('pending', { count: pending })}`}
            </span>
          )}
        </div>
        {dueHabits.length > 0 && (
          <ProgressBar
            value={doneCount / dueHabits.length}
            tone="done"
            size="sm"
            label={t('dayProgressAria', { done: doneCount, total: dueHabits.length })}
            className="mb-3"
          />
        )}

        {habits.length === 0 ? (
          <>
            <EmptyState
              icon={<Sprout className="size-6" />}
              title={t('noHabitsTitle')}
              description={t('noHabitsDescription')}
              action={
                <Link
                  to="/habits?new=1"
                  className="inline-flex h-11 items-center gap-2 rounded-xl bg-accent-strong px-4 text-sm font-semibold text-on-accent"
                >
                  <Plus className="size-4" aria-hidden />
                  {t('createHabit')}
                </Link>
              }
            />
            <TemplateShortcuts />
          </>
        ) : dueHabits.length === 0 ? (
          <p className="rounded-2xl border border-dashed border-border px-4 py-6 text-center text-sm text-muted">
            {t('nothingScheduled')}
          </p>
        ) : (
          <div className="flex flex-col gap-3">
            {dueHabits.map((h) => (
              <HabitCheckInCard key={h.id} habit={h} date={date} />
            ))}
          </div>
        )}
      </section>

      <FirstSessionChecklist habits={habits} />

      <PushPrimingCard habits={habits} />

      <TodayNudges today={today.data} />

      {otherHabits.length > 0 && (
        <details className="group">
          <summary className="flex cursor-pointer list-none items-center justify-between rounded-xl px-1 py-2 text-sm font-semibold uppercase tracking-wide text-muted [&::-webkit-details-marker]:hidden">
            {t('outsideToday', { count: otherHabits.length })}
            <ChevronDown className="size-4 transition group-open:rotate-180" aria-hidden />
          </summary>
          <div className="mt-2 flex flex-col gap-3">
            {otherHabits.map((h) => (
              <HabitCheckInCard key={h.id} habit={h} date={date} />
            ))}
          </div>
        </details>
      )}

      <CycleStrip cycle={cycle} />

      <DayStone date={date} />

      {goals.length > 0 && (
        <section aria-labelledby="goals-title">
          <h2 id="goals-title" className="mb-2 text-sm font-semibold uppercase tracking-wide text-muted">
            {t('cycleGoals')}
          </h2>
          <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
            {goals.map((g) => (
              <GoalMiniCard key={g.id} goal={g} />
            ))}
          </div>
        </section>
      )}
    </div>
  )
}

function TodaySkeleton() {
  return (
    <div className="flex flex-col gap-4" role="status" aria-busy="true">
      <div className="flex flex-col gap-2">
        <Skeleton className="h-4 w-40" />
        <Skeleton className="h-8 w-24" />
      </div>
      <Skeleton className="h-[4.5rem] w-full rounded-2xl" />
      <Skeleton className="h-24 w-full rounded-2xl" />
      <Skeleton className="h-24 w-full rounded-2xl" />
    </div>
  )
}
