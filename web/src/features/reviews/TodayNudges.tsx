import { ChevronRight, ChevronsDown, ChevronsUp, NotebookPen } from 'lucide-react'
import type { ReactNode } from 'react'
import { Link } from 'react-router'
import type { Today } from '../../api/types'
import { diffDays, formatDate } from '../../lib/dates'
import { formatPercent } from '../../lib/format'

/** Lembretes da tela Hoje: revisão semanal pendente, ciclo encerrado e hábitos prontos para subir. */
export function TodayNudges({ today }: { today: Today }) {
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
          title="O ciclo chegou ao fim"
          description={`Feche “${cycle.name}” com a revisão guiada e ganhe +100 XP.`}
        />
      )}
      {weeklyReview.due && (
        <Nudge
          to="/review"
          icon={<NotebookPen className="size-5" aria-hidden />}
          title="Revisão da semana"
          description={`Semana de ${formatDate(weeklyReview.weekStart)} a ${formatDate(weeklyReview.weekEnd)} · +30 XP`}
        />
      )}
      {suggested.map((h) => (
        <Nudge
          key={h.id}
          to={`/habits/${h.id}`}
          icon={<ChevronsUp className="size-5" aria-hidden />}
          title={`${h.emoji ? `${h.emoji} ` : ''}${h.name}: pronto para o nível ${h.level + 1}`}
          description={
            h.levelUpWeeksDone !== null
              ? `${h.levelUpWeeksDone} semanas seguidas com ${h.levelUpDaysPerWeek}+ dias.`
              : `${formatPercent(h.consistency.rate)} de consistência nos últimos 14 dias.`
          }
        />
      ))}
      {goBack.map((h) => (
        <Nudge
          key={`down-${h.id}`}
          to={`/habits/${h.id}`}
          icon={<ChevronsDown className="size-5" aria-hidden />}
          title={`${h.emoji ? `${h.emoji} ` : ''}${h.name}: voltar ao nível ${h.level - 1}?`}
          description="Duas semanas abaixo da metade da meta. Um degrau atrás mantém a constância."
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
