import type ns from '../pt-BR/lib'

const lib_en: typeof ns = {
  weekdayInitials: { d0: 'S', d1: 'M', d2: 'T', d3: 'W', d4: 'T', d5: 'F', d6: 'S' },
  weekdayNames: { d0: 'Sunday', d1: 'Monday', d2: 'Tuesday', d3: 'Wednesday', d4: 'Thursday', d5: 'Friday', d6: 'Saturday' },
  weekdayShort: { d0: 'Sun', d1: 'Mon', d2: 'Tue', d3: 'Wed', d4: 'Thu', d5: 'Fri', d6: 'Sat' },
  streakDay: '{{count}} day',
  streakDays: '{{count}} days',
  streakWeek: '{{count}} week',
  streakWeeks: '{{count}} weeks',
  everyDay: 'Every day',
  timesPerWeek: '{{count}}x per week',
  status: { Done: 'Done', Minimum: 'Minimum', Missed: 'Missed' },
  xpSource: {
    CheckIn: 'Check-in',
    HabitLevelUp: 'Habit leveled up',
    Milestone: 'Milestone completed',
    GoalCompleted: 'Goal completed',
    CycleCompleted: 'Cycle ended',
    WeeklyReview: 'Weekly review',
  },
  milestones: '{{done}}/{{total}} milestones',
  progressOf: '{{current}} of {{target}}{{unit}}',
  minutes: '{{m}} min',
  hours: '{{h}} h',
  hoursMinutes: '{{h}} h {{m}} min',
}
export default lib_en
