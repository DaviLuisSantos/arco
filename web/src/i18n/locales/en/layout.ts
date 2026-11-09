import type ns from '../pt-BR/layout'

const en: typeof ns = {
  nav: { today: 'Today', habits: 'Habits', cycle: 'Cycle', progress: 'Progress', profile: 'Profile', admin: 'Admin', main: 'Main' },
  routeError: {
    title: 'Something went wrong',
    body: 'We could not open this screen. Try reloading the page.',
    staleTitle: 'There is a new version of Arco',
    staleBody: 'Reload the page to keep using the app.',
    reload: 'Reload',
  },
  close: 'Close',
  cancel: 'Cancel',
  loading: 'Loading…',
  retry: 'Try again',
  offlineNoData: 'You are offline, and this screen has no data saved on this device yet. It will load when the connection is back.',
  achievementUnlocked: 'Achievement unlocked: {{emoji}} {{title}}',
}
export default en
