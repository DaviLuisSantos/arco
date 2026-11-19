const ns = {
  status: { Done: 'Feito', Minimum: 'Mínimo', Missed: 'Não fiz', Skipped: 'Pulei' },
  card: {
    level: 'Nv {{level}}',
    minimum: 'Mínimo: {{value}}',
    streakTitle: 'Sequência de {{current}} (recorde: {{best}})',
    streakSr: 'de sequência',
    weekProgress: '{{done}}/{{target}} na semana',
    checkInOf: 'Check-in de {{name}}',
    minutesQuestion: 'Quantos minutos?',
    optional: 'opcional',
    noteLabel: 'Motivo (opcional)',
    noteOptional: 'Nota (opcional)',
    addNote: 'Adicionar nota',
    minutesHint: 'Salva ao sair do campo',
    after: 'Depois de {{name}}',
    yourTurn: 'Sua vez',
  },
  qty: {
    groupLabel: 'Quantidade de {{name}}',
    barLabel: 'Progresso de {{name}}',
    progress: '{{total}} / {{target}} {{unit}}',
    minimumMark: 'mín. {{value}}',
    pending: 'Pendente',
    addOne: '+1',
    addValue: '+ valor',
    correct: 'Corrigir total',
    addLabel: 'Quanto somar?',
    totalLabel: 'Total do dia',
    add: 'Somar',
    save: 'Salvar total',
    invalid: 'Informe um número de 0 a 100000, com até 1 casa decimal.',
  },
  feedback: {
    undo: 'Desfazer',
    Done: '{{name}}: concluído ✅',
    Minimum: '{{name}}: o mínimo também conta',
    Missed: '{{name}}: sem problema, amanhã tem mais',
    Skipped: '{{name}}: pulado, sem culpa',
    removed: 'Check-in de {{name}} removido',
    dayComplete: 'Dia completo! Tudo marcado 🎉',
  },
}
export default ns

declare module '../../index' {
  interface ArcoResources {
    checkins: typeof ns
  }
}
