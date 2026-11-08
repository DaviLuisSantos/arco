const ns = {
  statXp: 'XP',
  statDone: 'Feito',
  statMinimum: 'Mínimo',
  statMissed: 'Não fiz',
  focus: 'Foco no período:',
  noHabits: 'Nenhum hábito neste período.',
  leveled: 'Subiu {{count}} nível · agora no {{level}}',
  leveledMany: 'Subiu {{count}} níveis · agora no {{level}}',
  ready: 'Pronto para o nível {{level}}',
  cycleEnded: 'O ciclo chegou ao fim',
  cycleDesc: 'Feche “{{name}}” com a revisão guiada e ganhe +100 XP.',
  weekReview: 'Revisão da semana',
  weekDesc: 'Semana de {{start}} a {{end}} · +30 XP',
  levelUpTitle: '{{habit}}: pronto para o nível {{level}}',
  levelUpWeeks: '{{weeks}} semanas seguidas com {{days}}+ dias.',
  levelUpRate: '{{rate}} de consistência nos últimos 14 dias.',
  levelDownTitle: '{{habit}}: voltar ao nível {{level}}?',
  levelDownDesc: 'Duas semanas abaixo da metade da meta. Um degrau atrás mantém a constância.',
  weekPrompts: {
    wins: 'O que funcionou nesta semana?',
    obstacles: 'O que atrapalhou?',
    next: 'Qual é o foco da próxima semana?',
  },
  cyclePrompts: {
    wins: 'O que funcionou neste ciclo?',
    obstacles: 'O que não funcionou?',
    next: 'O que levar para o próximo ciclo?',
  },
}
export default ns
declare module '../../index' {
  interface ArcoResources {
    reviews: typeof ns
  }
}
