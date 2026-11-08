import type ns from '../pt-BR/reviews'

const reviews_en: typeof ns = {
  statXp: 'XP',
  statDone: 'Done',
  statMinimum: 'Minimum',
  statMissed: 'Missed',
  focus: 'Focus in the period:',
  noHabits: 'No habits in this period.',
  leveled: 'Gained {{count}} level · now at {{level}}',
  leveledMany: 'Gained {{count}} levels · now at {{level}}',
  ready: 'Ready for level {{level}}',
  cycleEnded: 'The cycle has ended',
  cycleDesc: 'Close “{{name}}” with the guided review and earn +100 XP.',
  weekReview: "This week's review",
  weekDesc: 'Week of {{start}} to {{end}} · +30 XP',
  levelUpTitle: '{{habit}}: ready for level {{level}}',
  levelUpWeeks: '{{weeks}} weeks in a row with {{days}}+ days.',
  levelUpRate: '{{rate}} consistency over the last 14 days.',
  levelDownTitle: '{{habit}}: go back to level {{level}}?',
  levelDownDesc: 'Two weeks below half the target. One step back keeps you consistent.',
  weekPrompts: {
    wins: 'What worked this week?',
    obstacles: 'What got in the way?',
    next: "What's the focus for next week?",
  },
  cyclePrompts: {
    wins: 'What worked in this cycle?',
    obstacles: "What didn't work?",
    next: 'What to take into the next cycle?',
  },
}
export default reviews_en
