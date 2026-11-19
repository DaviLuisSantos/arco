const ns = {
  dialog: {
    title: 'Pausar hábito',
    description: 'Dias pausados não quebram a sequência nem contam como falha.',
    start: 'Início',
    end: 'Fim (opcional)',
    endHint: 'Sem fim, a pausa dura até você retomar.',
    reason: 'Motivo (opcional)',
    reasonPlaceholder: 'Viagem, doença…',
    endBeforeStart: 'O fim não pode ser antes do início.',
    submit: 'Pausar',
    success: 'Hábito pausado.',
  },
  card: {
    title: 'Pausas',
    pauseButton: 'Pausar hábito',
    empty: 'Nenhuma pausa agendada.',
    current: 'Em curso',
    future: 'Agendada',
    range: '{{start}} até {{end}}',
    openEnded: 'desde {{start}}, até retomar',
    resume: 'Retomar agora',
    remove: 'Excluir',
    removeAria: 'Excluir pausa de {{start}}',
    resumed: 'Pausa encerrada.',
    removed: 'Pausa excluída.',
  },
  today: {
    title: 'Pausados ({{count}})',
    until: 'até {{date}}',
    untilResume: 'até retomar',
    resume: 'Retomar agora',
    resumeAria: 'Retomar {{name}}',
  },
}
export default ns
declare module '../../index' {
  interface ArcoResources {
    pauses: typeof ns
  }
}
