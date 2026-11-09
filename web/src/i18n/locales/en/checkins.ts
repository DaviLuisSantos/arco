export default {
  status: { Done: 'Done', Minimum: 'Minimum', Missed: 'Missed' },
  card: {
    level: 'Lv {{level}}',
    minimum: 'Minimum: {{value}}',
    streakTitle: '{{current}} streak (best: {{best}})',
    streakSr: 'streak',
    weekProgress: '{{done}}/{{target}} this week',
    checkInOf: 'Check-in for {{name}}',
    minutesQuestion: 'How many minutes?',
    optional: 'optional',
    minutesHint: 'Saved when you leave the field',
  },
  feedback: {
    undo: 'Undo',
    Done: '{{name}}: done ✅',
    Minimum: '{{name}}: the minimum counts too',
    Missed: '{{name}}: no worries, tomorrow is another day',
    removed: 'Check-in for {{name}} removed',
    dayComplete: 'Day complete! Everything checked 🎉',
  },
}
