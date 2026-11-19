import { ChevronDown } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { pausesApi } from '../../api/endpoints'
import type { TodayHabit } from '../../api/types'
import { Button } from '../../components/ui/Button'
import { formatDate } from '../../lib/dates'
import { useAppMutation } from '../../lib/useAppMutation'

export function PausedHabitsSection({ habits }: { habits: TodayHabit[] }) {
  const { t } = useTranslation('pauses', { keyPrefix: 'today' })
  const resume = useAppMutation((h: TodayHabit) => pausesApi.resume(h.id))
  if (habits.length === 0) return null

  return (
    <details className="group">
      <summary className="flex cursor-pointer list-none items-center justify-between rounded-xl px-1 py-2 text-sm font-semibold uppercase tracking-wide text-muted [&::-webkit-details-marker]:hidden">
        {t('title', { count: habits.length })}
        <ChevronDown className="size-4 transition group-open:rotate-180" aria-hidden />
      </summary>
      <ul className="mt-2 flex flex-col gap-2">
        {habits.map((h) => (
          <li key={h.id} className="flex items-center gap-3 rounded-2xl border border-border bg-surface p-3">
            <span className="grid size-10 shrink-0 place-items-center rounded-xl bg-surface-2 text-xl" aria-hidden>
              {h.emoji || '•'}
            </span>
            <div className="min-w-0 flex-1">
              <p className="truncate font-semibold">{h.name}</p>
              <p className="text-sm text-muted">
                {h.pausedUntil ? t('until', { date: formatDate(h.pausedUntil, { day: '2-digit', month: '2-digit' }) }) : t('untilResume')}
              </p>
            </div>
            <Button
              variant="secondary"
              size="sm"
              loading={resume.isPending && resume.variables?.id === h.id}
              aria-label={t('resumeAria', { name: h.name })}
              onClick={() => resume.mutate(h)}
            >
              {t('resume')}
            </Button>
          </li>
        ))}
      </ul>
    </details>
  )
}
