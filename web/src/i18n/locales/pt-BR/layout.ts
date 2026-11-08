const ns = {
  nav: { today: 'Hoje', habits: 'Hábitos', cycle: 'Ciclo', profile: 'Perfil', admin: 'Admin', main: 'Principal' },
  close: 'Fechar',
  cancel: 'Cancelar',
  loading: 'Carregando…',
  retry: 'Tentar de novo',
  offlineNoData: 'Sem internet, e esta tela ainda não tem dados salvos neste aparelho. Ela carrega quando a conexão voltar.',
  achievementUnlocked: 'Conquista desbloqueada: {{emoji}} {{title}}',
}
export default ns
declare module '../../index' {
  interface ArcoResources {
    layout: typeof ns
  }
}
