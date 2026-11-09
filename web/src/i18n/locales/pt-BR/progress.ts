const ns = {
  title: 'Progresso',
  subtitle: 'Sua evolução em um só lugar.',
  tabsLabel: 'Seções de progresso',
  tabs: { stats: 'Estatísticas', review: 'Revisão', achievements: 'Conquistas' },
  back: 'Progresso',
  statsEmpty: {
    title: 'Ainda sem dados para mostrar',
    description: 'Faça check-ins nos seus hábitos e as estatísticas das últimas semanas aparecem aqui.',
    action: 'Ir para Hoje',
  },
  secondaryError: 'Não foi possível carregar {{what}}.',
  retry: 'Tentar de novo',
  what: { pastReviews: 'as revisões anteriores', cycles: 'os ciclos anteriores' },
}
export default ns
declare module '../../index' {
  interface ArcoResources {
    progress: typeof ns
  }
}
