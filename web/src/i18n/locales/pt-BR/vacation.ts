const ns = {
  title: 'Modo férias',
  description: 'Pausa todos os hábitos ativos no período, sem quebrar sequência nem consistência.',
  start: 'Início',
  end: 'Fim',
  reason: 'Motivo (opcional)',
  activate: 'Ativar modo férias',
  activeUntil: 'Em férias até {{date}}',
  activeFrom: 'Férias a partir de {{start}} até {{end}}',
  endVacation: 'Encerrar férias',
  confirmTitle: 'Encerrar as férias?',
  confirmDescription: 'Os hábitos pausados pelas férias voltam hoje. Pausas individuais continuam.',
  activated_one: 'Modo férias ativado: {{count}} hábito pausado.',
  activated_other: 'Modo férias ativado: {{count}} hábitos pausados.',
  ended_one: 'Férias encerradas: {{count}} hábito retomado.',
  ended_other: 'Férias encerradas: {{count}} hábitos retomados.',
}
export default ns
declare module '../../index' {
  interface ArcoResources {
    vacation: typeof ns
  }
}
