const ns = {
  complete: {
    success: 'Ciclo encerrado. Hora de planejar o próximo!',
    notesTooLong: 'Use até {{max}} caracteres nas notas.',
    reviewTitle: 'Revisão: {{name}}',
    endCycle: 'Encerrar ciclo',
    numbersDescription: 'Como foi o ciclo, em números.',
    questionsDescription: 'Responda o que fizer sentido e encerre.',
    continue: 'Continuar',
    notes: 'Notas livres',
    optional: 'Opcional.',
    goalsDone: 'Objetivos · {{done}} de {{total}} concluídos',
    habits: 'Hábitos',
    weeklyReviewsNone: 'Nenhuma revisão semanal neste ciclo.',
    weeklyReviews_one: '{{count}} revisão semanal neste ciclo.',
    weeklyReviews_other: '{{count}} revisões semanais neste ciclo.',
  },
  create: {
    success: 'Ciclo iniciado. Bora!',
    nameRequired: 'Dê um nome ao ciclo.',
    hoursInvalid: 'Informe uma meta de foco semanal entre 1 e 56 horas.',
    title: 'Comece um ciclo de {{weeks}} semanas',
    intro: 'Um ciclo tem começo, meio e fim. Escolha até 3 objetivos e encerre com uma revisão.',
    name: 'Nome',
    namePlaceholder: 'Ex.: Primavera em movimento',
    start: 'Início',
    weeks: 'Semanas',
    weeksOption: '{{count}} semanas',
    focusGoal: 'Meta de foco por semana (h)',
    ends: 'Termina em {{date}} · {{days}} dias',
    submit: 'Começar ciclo',
  },
  strip: {
    startTitle: 'Comece um ciclo de 6 semanas',
    startHint: 'Defina até 3 objetivos e dê direção aos seus hábitos.',
    day: 'Dia {{day}} de {{total}}',
    remaining: '{{count}} dias restantes',
    progress: 'Progresso do ciclo',
  },
  delete: {
    success: 'Ciclo excluído.',
    button: 'Excluir ciclo',
    title: 'Excluir “{{name}}”?',
    description:
      'O ciclo, os objetivos e os marcos dele são apagados, e o XP que deram (encerramento, objetivos e marcos) sai do seu total. Os hábitos continuam. Não dá para desfazer.',
    activeHint: 'Para fechar um ciclo que você cumpriu, use “Encerrar ciclo”.',
  },
  past: {
    title: 'Ciclos anteriores',
    viewArch: 'Ver o arco de {{name}}',
    archError: 'Não foi possível carregar o arco.',
    archLoading: 'Carregando o arco…',
    archDescription: 'Arco do ciclo encerrado.',
    days: '{{count}} dias',
    noReview: 'Sem revisão.',
  },
}
export default ns
declare module '../../index' {
  interface ArcoResources {
    cycle: typeof ns
  }
}
