const ns = {
  state: { done: 'concluída', now: 'em andamento', future: 'ainda não começou' },
  stoneLabel: 'Semana {{week}}: {{minutes}} de {{target}}, {{state}}',
  svgLabel_one: 'Arco do ciclo com {{count}} semana: {{total}} de foco',
  svgLabel_other: 'Arco do ciclo com {{count}} semanas: {{total}} de foco',
  svgLabelClosed: ', ciclo concluído',
  sub: { closed: 'ciclo concluído', notStarted: 'ainda não começou', week: 'semana {{current}} de {{total}}' },
  end: 'fim',
  panel: {
    orderHint: 'O arco sobe dos dois pilares: semanas ímpares à esquerda, pares à direita.',
    goalPerWeek: 'Meta: {{duration}} por semana',
    emptyTitle: 'Nenhuma sessão de foco ainda',
    emptyDescription: 'Use o cronômetro ou registre os minutos no check-in. Cada sessão vira um tijolo do arco.',
    legend: 'Legenda dos hábitos',
    week: 'Semana {{week}}',
    of: 'de {{duration}}',
    removedHabit: 'Hábito removido',
    goalUpdated: 'Meta semanal atualizada.',
    hoursInvalid: 'Informe de {{min}} a {{max}} horas.',
    editGoal: 'Editar meta semanal',
    goalLabel: 'Meta de foco por semana (h)',
    hoursHint: 'De {{min}} a {{max}} horas.',
  },
  dayStone: {
    empty: 'Pedra do dia vazia',
    withBricks_one: 'Pedra do dia com {{count}} tijolo',
    withBricks_other: 'Pedra do dia com {{count}} tijolos',
    title: 'Pedra do dia',
    none: 'Nenhum foco hoje ainda. Inicie o timer em um hábito para assentar o primeiro tijolo.',
  },
  stoneDays: {
    label: 'Sessões de foco por dia na semana {{week}}. {{summary}}',
    habit: 'Hábito',
    today: 'hoje',
    source: { Timer: 'cronômetro', Manual: 'manual' },
  },
}
export default ns

declare module '../../index' {
  interface ArcoResources {
    arch: typeof ns
  }
}
