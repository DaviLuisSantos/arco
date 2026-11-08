const ns = {
  status: { Done: 'Feito', Minimum: 'Mínimo', Missed: 'Não fiz' },
  card: {
    level: 'Nv {{level}}',
    minimum: 'Mínimo: {{value}}',
    streakTitle: 'Sequência de {{current}} (recorde: {{best}})',
    streakSr: 'de sequência',
    weekProgress: '{{done}}/{{target}} na semana',
    checkInOf: 'Check-in de {{name}}',
    minutesQuestion: 'Quantos minutos?',
    optional: 'opcional',
  },
}
export default ns

declare module '../../index' {
  interface ArcoResources {
    checkins: typeof ns
  }
}
