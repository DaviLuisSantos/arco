const ns = {
  weekdayInitials: { d0: 'D', d1: 'S', d2: 'T', d3: 'Q', d4: 'Q', d5: 'S', d6: 'S' },
  weekdayNames: { d0: 'Domingo', d1: 'Segunda', d2: 'Terça', d3: 'Quarta', d4: 'Quinta', d5: 'Sexta', d6: 'Sábado' },
  weekdayShort: { d0: 'dom', d1: 'seg', d2: 'ter', d3: 'qua', d4: 'qui', d5: 'sex', d6: 'sáb' },
  streakDay: '{{count}} dia',
  streakDays: '{{count}} dias',
  streakWeek: '{{count}} semana',
  streakWeeks: '{{count}} semanas',
  everyDay: 'Todo dia',
  timesPerWeek: '{{count}}x por semana',
  status: { Done: 'Feito', Minimum: 'Mínimo', Missed: 'Não fiz' },
  xpSource: {
    CheckIn: 'Check-in',
    HabitLevelUp: 'Hábito subiu de nível',
    Milestone: 'Marco concluído',
    GoalCompleted: 'Objetivo concluído',
    CycleCompleted: 'Ciclo encerrado',
    WeeklyReview: 'Revisão semanal',
  },
  milestones: '{{done}}/{{total}} marcos',
  progressOf: '{{current}} de {{target}}{{unit}}',
  minutes: '{{m}} min',
  hours: '{{h}} h',
  hoursMinutes: '{{h}} h {{m}} min',
}
export default ns
declare module '../../index' {
  interface ArcoResources {
    lib: typeof ns
  }
}
