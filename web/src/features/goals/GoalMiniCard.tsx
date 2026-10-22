import { CircleCheck } from 'lucide-react'
import { Link } from 'react-router'
import type { Goal } from '../../api/types'
import { ProgressBar } from '../../components/ui/ProgressBar'
import { formatDate } from '../../lib/dates'
import { goalProgressText } from '../../lib/format'

export function GoalMiniCard({ goal }: { goal: Goal }) {
  const done = goal.completedAt !== null
  return (
    <Link to="/cycle" className="block rounded-2xl border border-border bg-surface p-3 transition hover:border-accent/60">
      <div className="mb-2 flex items-start justify-between gap-2">
        <p className="line-clamp-2 text-sm font-semibold">{goal.title}</p>
        {done && <CircleCheck className="size-4 shrink-0 text-done" aria-label="Concluído" />}
      </div>
      <ProgressBar value={goal.progress} size="sm" tone={done ? 'done' : 'accent'} label={goal.title} />
      <div className="mt-1.5 flex justify-between gap-2 text-xs text-muted">
        <span className="truncate">{goalProgressText(goal)}</span>
        <span className="shrink-0">até {formatDate(goal.deadline)}</span>
      </div>
    </Link>
  )
}
