const ns = {
  rate: 'Consistência',
  xp: 'XP',
  hours: 'Horas',
  metricLabel: 'Medida do gráfico',
  groupLabel: '{{metric}} nas últimas {{count}} semanas',
  weekOf: 'Semana de {{date}}: {{value}}',
  noHabitsShort: 'sem hábitos',
  current: 'atual',
  untilToday: ' (até hoje)',
  range: '{{start}} a {{end}}',
  noHabitsWeek: 'sem hábitos nesta semana',
  ofExpected: '({{done}} de {{expected}})',
}
export default ns
declare module '../../index' {
  interface ArcoResources {
    stats: typeof ns
  }
}
