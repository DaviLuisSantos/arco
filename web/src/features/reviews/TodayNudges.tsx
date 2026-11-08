import { ChevronRight, ChevronsDown, ChevronsUp, NotebookPen } from 'lucide-react'
import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import type { Today } from '../../api/types'
import { diffDays, formatDate } from '../../lib/dates'
import { formatPercent } from '../../lib/format'

/** Lembretes da tela Hoje: revisão semanal pendente, ciclo encerrado e hábitos prontos para subir. */
export function TodayNudges({ today }: { today: Today }) {
  const { t } = useTranslation('reviews')
  const { weeklyReview, cycle, habits, date } = today
  const suggested = habits.filter((h) => h.levelUpSuggested)
  const goBack = habits.filter((h) => h.levelDownSuggested)
  const cycleOver = cycle !== null && diffDays(cycle.endDate, date) >= 0

  if (!weeklyReview.due && !cycleOver && suggested.length === 0 && goBack.length === 0) return null

  return (
    <div className="flex flex-col gap-2">
      {cycleOver && (
        <Nudge
          to="/cycle"
          icon={<NotebookPen className="size-5" aria-hidden />}
          title={t('cycleEnded')}
          description={t('cycleDesc', { name: cycle.name })}
        />
      )}
      {weeklyReview.due && (
        <Nudge
          to="/review"
          icon={<NotebookPen className="size-5" aria-hidden />}
          title={t('weekReview')}
          description={t('weekDesc', { start: formatDate(weeklyReview.weekStart), end: formatDate(weeklyReview.weekEnd) })}
        />
      )}
      {suggested.map((h) => (
        <Nudge
          key={h.id}
          to={`/habits/${h.id}`}
          icon={<ChevronsUp className="size-5" aria-hidden />}
          title={t('levelUpTitle', { habit: `${h.emoji ? `${h.emoji} ` : ''}${h.name}`, level: h.level + 1 })}
          description={
            h.levelUpWeeksDone !== null
              ? t('levelUpWeeks', { weeks: h.levelUpWeeksDone, days: h.levelUpDaysPerWeek })
              : t('levelUpRate', { rate: formatPercent(h.consistency.rate) })
          }
        />
      ))}
      {goBack.map((h) => (
        <Nudge
          key={`down-${h.id}`}
          to={`/habits/${h.id}`}
          icon={<ChevronsDown className="size-5" aria-hidden />}
          title={t('levelDownTitle', { habit: `${h.emoji ? `${h.emoji} ` : ''}${h.name}`, level: h.level - 1 })}
          description={t('levelDownDesc')}
        />
      ))}
    </div>
  )
}

function Nudge({ to, icon, title, description }: { to: string; icon: ReactNode; title: string; description: string }) {
  return (
    <Link
      to={to}
      className="flex items-center gap-3 rounded-2xl border border-accent/50 bg-accent/10 p-4 transition hover:bg-accent/15"
    >
      <span className="grid size-10 shrink-0 place-items-center rounded-xl bg-accent/20 text-accent">{icon}</span>
      <span className="min-w-0 flex-1">
        <span className="block truncate font-semibold">{title}</span>
        <span className="block text-sm text-muted">{description}</span>
      </span>
      <ChevronRight className="size-5 shrink-0 text-muted" aria-hidden />
    </Link>
  )
}
