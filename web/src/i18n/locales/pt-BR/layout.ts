const ns = {
  nav: { today: 'Hoje', habits: 'Hábitos', cycle: 'Ciclo', progress: 'Progresso', profile: 'Perfil', admin: 'Admin', main: 'Principal' },
  routeError: {
    title: 'Algo deu errado',
    body: 'Não foi possível abrir esta tela. Tente recarregar a página.',
    staleTitle: 'Há uma versão nova do Arco',
    staleBody: 'Recarregue a página para continuar usando o app.',
    reload: 'Recarregar',
  },
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
