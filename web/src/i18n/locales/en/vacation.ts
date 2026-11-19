import type ns from '../pt-BR/vacation'

const vacation_en: typeof ns = {
  title: 'Vacation mode',
  description: 'Pauses every active habit during the period, without breaking streaks or consistency.',
  start: 'Start',
  end: 'End',
  reason: 'Reason (optional)',
  activate: 'Turn on vacation mode',
  activeUntil: 'On vacation until {{date}}',
  activeFrom: 'Vacation from {{start}} to {{end}}',
  endVacation: 'End vacation',
  confirmTitle: 'End your vacation?',
  confirmDescription: 'Habits paused by the vacation resume today. Individual pauses stay.',
  activated_one: 'Vacation mode on: {{count}} habit paused.',
  activated_other: 'Vacation mode on: {{count}} habits paused.',
  ended_one: 'Vacation ended: {{count}} habit resumed.',
  ended_other: 'Vacation ended: {{count}} habits resumed.',
}
export default vacation_en
