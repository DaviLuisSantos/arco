import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { achievementsApi } from '../../api/endpoints'
import type { Achievement } from '../../api/types'
import { ProgressBar } from '../../components/ui/ProgressBar'
import { ErrorState, Spinner } from '../../components/ui/States'
import { cn } from '../../lib/cn'
import { formatInstant } from '../../lib/dates'
import { queryKeys } from '../../lib/queryKeys'
import { achievementText } from './achievementText'

/** Conquistas no Perfil: desbloqueadas em destaque, bloqueadas em cinza com o progresso. */
export function AchievementsSection() {
  const { t } = useTranslation('achievements')
  const achievements = useQuery({ queryKey: queryKeys.achievements, queryFn: achievementsApi.list })

  return (
    <section aria-labelledby="achievements-title">
      <div className="mb-2 flex items-baseline justify-between">
        <h2 id="achievements-title" className="text-sm font-semibold uppercase tracking-wide text-muted">
          {t('title')}
        </h2>
        {achievements.data && (
          <span className="text-sm text-muted">
            {t('count', { done: achievements.data.filter((a) => a.unlockedAt).length, total: achievements.data.length })}
          </span>
        )}
      </div>
      {achievements.isPending ? (
        <Spinner />
      ) : achievements.isLoadingError ? (
        <ErrorState error={achievements.error} onRetry={() => void achievements.refetch()} />
      ) : (
        <ul className="grid grid-cols-2 gap-2 sm:grid-cols-3">
          {achievements.data.map((a) => (
            <AchievementCard key={a.key} achievement={a} />
          ))}
        </ul>
      )}
    </section>
  )
}

function AchievementCard({ achievement: a }: { achievement: Achievement }) {
  const { t } = useTranslation('achievements')
  const unlocked = a.unlockedAt !== null
  return (
    <li
      className={cn(
        'flex min-w-0 flex-col gap-1 rounded-2xl border p-3',
        unlocked ? 'border-xp/40 bg-xp/10' : 'border-border bg-surface',
      )}
    >
      <span className={cn('text-2xl', !unlocked && 'opacity-40 grayscale')} aria-hidden>
        {a.emoji}
      </span>
      <p className={cn('text-sm font-semibold break-words', !unlocked && 'text-muted')}>{achievementText(a).title}</p>
      <p className="text-xs text-muted">{achievementText(a).description}</p>
      {unlocked ? (
        <p className="mt-auto pt-1 text-xs text-xp">{formatInstant(a.unlockedAt!)}</p>
      ) : (
        <div className="mt-auto flex items-center gap-2 pt-1">
          <ProgressBar value={a.progress / a.target} size="sm" label={t('progress', { title: achievementText(a).title })} />
          <span className="shrink-0 text-xs text-muted">
            {a.progress}/{a.target}
          </span>
        </div>
      )}
    </li>
  )
}
