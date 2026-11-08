const ns = {
  state: {
    on: 'Ativadas neste dispositivo.',
    off: 'Desativadas neste dispositivo.',
    denied: 'Bloqueadas pelo navegador. Libere as notificações do Arco nas configurações do navegador.',
    noSw: 'Disponíveis só no app publicado (o modo de desenvolvimento não registra o service worker).',
    needsInstall: 'No iPhone/iPad, instale o Arco na tela de início (Compartilhar → Adicionar à Tela de Início) e abra por lá.',
    unsupported: 'Este navegador não suporta notificações push.',
  },
  disabledToast: 'Notificações desativadas neste dispositivo.',
  enabledToast: 'Notificações ativadas.',
  testSent: 'Teste enviado para {{count}} dispositivo',
  testSentMany: 'Teste enviado para {{count}} dispositivos',
  testNone: 'Nenhum dispositivo recebeu o teste.',
  devices: '{{count}} dispositivo inscrito.',
  devicesMany: '{{count}} dispositivos inscritos.',
  disable: 'Desativar',
  enable: 'Ativar notificações',
  sendTest: 'Enviar teste',
  reminderHint: 'O horário do lembrete de cada hábito é definido na edição do hábito.',
  reminderSaved: 'Lembrete da revisão salvo.',
  weeklyReminder: 'Lembrete da revisão semanal',
  day: 'Dia',
  time: 'Horário',
  timeInvalid: 'Use HH:mm.',
  saveReminder: 'Salvar lembrete',
}
export default ns
declare module '../../index' {
  interface ArcoResources {
    push: typeof ns
  }
}
