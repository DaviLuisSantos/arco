import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { useSearchParams } from 'react-router'
import { playerApi } from '../api/endpoints'
import { Card } from '../components/ui/Card'
import { PageHeader } from '../components/ui/PageHeader'
import { Segmented } from '../components/ui/Segmented'
import { AchievementsSection } from '../features/achievements/AchievementsSection'
import { PlayerProgress } from '../features/player/PlayerProgress'
import { RecentXp } from '../features/player/RecentXp'
import { formatNumber } from '../lib/format'
import { queryKeys } from '../lib/queryKeys'
import { ReviewPage } from './ReviewPage'
import { StatsPage } from './StatsPage'

type Tab = 'stats' | 'review' | 'achievements'
const TABS: Tab[] = ['stats', 'review', 'achievements']

/** Hub de Progresso: Estatísticas, Revisão e Conquistas. A aba ativa vive em `?tab=`. */
export function ProgressPage() {
  const { t } = useTranslation('progress')
  const [params, setParams] = useSearchParams()
  const raw = params.get('tab')
  const tab: Tab = TABS.includes(raw as Tab) ? (raw as Tab) : 'stats'

  return (
    <div className="flex flex-col gap-5">
      <PageHeader title={t('title')} subtitle={t('subtitle')} />
      <Segmented
        label={t('tabsLabel')}
        value={tab}
        onChange={(v) => setParams({ tab: v }, { replace: true })}
        options={TABS.map((v) => ({ value: v, label: t(`tabs.${v}`) }))}
      />
      {tab === 'stats' && <StatsPage embedded />}
      {tab === 'review' && <ReviewPage embedded />}
      {tab === 'achievements' && <RewardsTab />}
    </div>
  )
}

/** Tudo o que é recompensa num só lugar: nível do jogador, medalhas e os últimos lançamentos de XP. */
function RewardsTab() {
  const { t } = useTranslation('pages', { keyPrefix: 'profile' })
  const player = useQuery({ queryKey: queryKeys.player, queryFn: playerApi.get })
  return (
    <>
      {player.data && (
        <Card className="flex flex-col gap-4">
          <PlayerProgress player={player.data} />
          <p className="text-sm text-muted">
            {t('total')} <span className="font-semibold text-text">{formatNumber(player.data.totalXp)} XP</span>
          </p>
        </Card>
      )}
      <AchievementsSection />
      {player.data && <RecentXp events={player.data.recentEvents} />}
    </>
  )
}
