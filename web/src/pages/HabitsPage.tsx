import { useQuery } from '@tanstack/react-query'
import { Archive, ChevronRight, Plus, Sprout } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useSearchParams } from 'react-router'
import { billingApi, habitsApi } from '../api/endpoints'
import type { Habit } from '../api/types'
import { Button } from '../components/ui/Button'
import { PageHeader } from '../components/ui/PageHeader'
import { Segmented } from '../components/ui/Segmented'
import { EmptyState, ErrorState, Spinner } from '../components/ui/States'
import { HabitFormDialog } from '../features/habits/HabitFormDialog'
import { frequencyText } from '../lib/format'
import { queryKeys } from '../lib/queryKeys'

type Tab = 'active' | 'archived'

export function HabitsPage() {
  const { t } = useTranslation('pages', { keyPrefix: 'habits' })
  const [params, setParams] = useSearchParams()
  const [tab, setTab] = useState<Tab>('active')
  const habits = useQuery({ queryKey: queryKeys.habits, queryFn: () => habitsApi.list(true) })

  const { t: tb } = useTranslation('billing')
  const billing = useQuery({ queryKey: ['billing'], queryFn: billingApi.get })
  const limit = billing.data?.limits.habits
  const activeCount = billing.data?.usage.habits ?? 0

  const creating = params.get('new') === '1'
  const setCreating = (open: boolean) =>
    setParams(
      (p) => {
        if (open) p.set('new', '1')
        else p.delete('new')
        return p
      },
      { replace: true },
    )

  const list = (habits.data ?? []).filter((h) => (tab === 'active' ? !h.archivedAt : !!h.archivedAt))

  return (
    <div>
      <PageHeader
        title={t('title')}
        action={
          <Button size="sm" onClick={() => setCreating(true)}>
            <Plus className="size-4" aria-hidden />
            {t('new')}
          </Button>
        }
      />

      <Segmented
        label={t('filter')}
        value={tab}
        onChange={setTab}
        options={[
          { value: 'active', label: t('active') },
          { value: 'archived', label: t('archived') },
        ]}
        className="mb-4"
      />

      {limit !== null && limit !== undefined && activeCount >= Math.max(1, limit - 1) && (
        <p className="mb-3 text-sm text-muted">
          {tb(activeCount >= limit ? 'habitsUsage' : 'habitsUsageNear', { used: activeCount, limit })}
        </p>
      )}

      {habits.isPending ? (
        <Spinner />
      ) : habits.isLoadingError ? (
        <ErrorState error={habits.error} onRetry={() => void habits.refetch()} />
      ) : list.length === 0 ? (
        tab === 'active' ? (
          <EmptyState
            icon={<Sprout className="size-6" />}
            title={t('noActiveTitle')}
            description={t('noActiveDescription')}
            action={
              <Button onClick={() => setCreating(true)}>
                <Plus className="size-4" aria-hidden />
                {t('create')}
              </Button>
            }
          />
        ) : (
          <EmptyState
            icon={<Archive className="size-6" />}
            title={t('noArchivedTitle')}
            description={t('noArchivedDescription')}
          />
        )
      ) : (
        <ul className="flex flex-col gap-2">
          {list.map((h) => (
            <li key={h.id}>
              <HabitRow habit={h} />
            </li>
          ))}
        </ul>
      )}

      <HabitFormDialog open={creating} onClose={() => setCreating(false)} />
    </div>
  )
}

function HabitRow({ habit }: { habit: Habit }) {
  const { t } = useTranslation('pages', { keyPrefix: 'habits' })
  return (
    <Link
      to={`/habits/${habit.id}`}
      className="flex items-center gap-3 rounded-2xl border border-border bg-surface p-3 transition hover:border-accent/60"
    >
      <span className="grid size-11 shrink-0 place-items-center rounded-xl bg-surface-2 text-2xl" aria-hidden>
        {habit.emoji || '•'}
      </span>
      <span className="min-w-0 flex-1">
        <span className="block truncate font-semibold">{habit.name}</span>
        <span className="block truncate text-sm text-muted">
          {t('rowLevel', { level: habit.level, description: habit.levelDescription, frequency: frequencyText(habit) })}
        </span>
      </span>
      <ChevronRight className="size-5 shrink-0 text-muted" aria-hidden />
    </Link>
  )
}
