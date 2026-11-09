import type ns from '../pt-BR/progress'

const progress_en: typeof ns = {
  title: 'Progress',
  subtitle: 'Your growth in one place.',
  tabsLabel: 'Progress sections',
  tabs: { stats: 'Statistics', review: 'Review', achievements: 'Achievements' },
  back: 'Progress',
  statsEmpty: {
    title: 'No data to show yet',
    description: 'Check in on your habits and the stats for the last weeks will show up here.',
    action: 'Go to Today',
  },
  secondaryError: 'Could not load {{what}}.',
  retry: 'Try again',
  what: { pastReviews: 'past reviews', cycles: 'past cycles' },
}
export default progress_en
