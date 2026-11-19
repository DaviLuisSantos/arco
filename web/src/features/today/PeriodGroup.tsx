import { useId } from 'react'
import { useTranslation } from 'react-i18next'
import type { TimeOfDay, TodayHabit } from '../../api/types'
import { HabitCheckInCard } from '../checkins/HabitCheckInCard'

/** Ordem dos grupos no Hoje: os períodos fixos e, no fim, "qualquer hora". */
// eslint-disable-next-line react-refresh/only-export-components
export const PERIODS: TimeOfDay[] = ['Morning', 'Afternoon', 'Evening', 'Anytime']

interface PeriodGroupProps {
  period: TimeOfDay
  /** Já na ordem do dia (vinda do servidor). */
  habits: TodayHabit[]
  date: string
  /** Mostra o título do período (e o selo "Agora" quando for o atual). */
  showHeading: boolean
  isCurrent: boolean
}

export function PeriodGroup({ period, habits, date, showHeading, isCurrent }: PeriodGroupProps) {
  const { t } = useTranslation(['habits', 'pages'])
  const headingId = useId()

  return (
    <section aria-labelledby={showHeading ? headingId : undefined} data-period={period} className="flex flex-col gap-3">
      {showHeading && (
        <div className="flex items-center gap-2">
          <h3 id={headingId} className="text-sm font-semibold text-text">
            {t(`habits:form.period.${period}`)}
          </h3>
          {isCurrent && (
            <span className="rounded-full bg-accent/15 px-2 py-0.5 text-xs font-semibold text-accent">
              {t('pages:today.now')}
            </span>
          )}
        </div>
      )}
      {habits.map((h) => (
        <HabitCheckInCard key={h.id} habit={h} date={date} />
      ))}
    </section>
  )
}
