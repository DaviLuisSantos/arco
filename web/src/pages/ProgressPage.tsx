import { useTranslation } from 'react-i18next'
import { useSearchParams } from 'react-router'
import { PageHeader } from '../components/ui/PageHeader'
import { Segmented } from '../components/ui/Segmented'
import { AchievementsSection } from '../features/achievements/AchievementsSection'
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
      {tab === 'achievements' && <AchievementsSection />}
    </div>
  )
}
