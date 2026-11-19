const ns = {
  status: { Done: 'Feito', Minimum: 'Mínimo', Missed: 'Não fiz', Skipped: 'Pulei' },
  statusNegative: { Done: 'Limpo', Missed: 'Recaída', Skipped: 'Pulei' },
  card: {
    // pt-BR trata 0 como singular ("one"); "0 dias" soa natural, então o zero tem forma própria.
    clean_zero: '0 dias limpo · recorde {{best}}',
    clean_one: '{{count}} dia limpo · recorde {{best}}',
    clean_other: '{{count}} dias limpo · recorde {{best}}',
    nextMilestone: 'Próximo marco: {{days}} dias limpo',
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
    DoneNegative: '{{name}}: dia limpo ✅',
    MissedNegative: '{{name}}: recaída registrada, recomeçar também é avançar',
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
