import { useQuery } from '@tanstack/react-query'
import { Archive, ChevronRight, Plus, Sprout } from 'lucide-react'
import { useState } from 'react'
import { Link, useSearchParams } from 'react-router'
import { habitsApi } from '../api/endpoints'
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
  const [params, setParams] = useSearchParams()
  const [tab, setTab] = useState<Tab>('active')
  const habits = useQuery({ queryKey: queryKeys.habits, queryFn: () => habitsApi.list(true) })

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
        title="Hábitos"
        action={
          <Button size="sm" onClick={() => setCreating(true)}>
            <Plus className="size-4" aria-hidden />
            Novo hábito
          </Button>
        }
      />

      <Segmented
        label="Filtro de hábitos"
        value={tab}
        onChange={setTab}
        options={[
          { value: 'active', label: 'Ativos' },
          { value: 'archived', label: 'Arquivados' },
        ]}
        className="mb-4"
      />

      {habits.isPending ? (
        <Spinner />
      ) : habits.isError ? (
        <ErrorState error={habits.error} onRetry={() => void habits.refetch()} />
      ) : list.length === 0 ? (
        tab === 'active' ? (
          <EmptyState
            icon={<Sprout className="size-6" />}
            title="Nenhum hábito ativo"
            description="Comece pequeno: um hábito com uma dose possível e uma versão mínima."
            action={
              <Button onClick={() => setCreating(true)}>
                <Plus className="size-4" aria-hidden />
                Criar hábito
              </Button>
            }
          />
        ) : (
          <EmptyState
            icon={<Archive className="size-6" />}
            title="Nada arquivado"
            description="Hábitos arquivados aparecem aqui e podem ser restaurados."
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
          Nv {habit.level} · {habit.levelDescription} · {frequencyText(habit)}
        </span>
      </span>
      <ChevronRight className="size-5 shrink-0 text-muted" aria-hidden />
    </Link>
  )
}
