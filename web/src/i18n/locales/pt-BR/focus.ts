const ns = {
  lessThanMinute: 'Menos de 1 minuto: nada foi registrado.',
  markedDone: 'Marcado como Feito.',
  brickFilling: '{{name}}: tijolo enchendo',
  remaining: 'Tempo restante',
  elapsed: 'Tempo decorrido',
  focusOf: 'Foco de {{duration}}. Pode fechar o app: a contagem continua.',
  freeTimerHint: 'Cronômetro livre. Conclua quando quiser.',
  discard: 'Descartar',
  finishNow: 'Concluir agora',
  discardTitle: 'Descartar esta sessão?',
  discardDescription: 'O tempo decorrido não será registrado. Se preferir guardar, use “Concluir agora”.',
  pendingText: 'A sessão de <b>{{duration}}</b> ainda não foi salva. Seus minutos estão guardados neste aparelho.',
  savedFocus: '{{duration}} de foco salvos',
  newBrick: 'Um tijolo novo na pedra do dia.',
  markDone: 'Marcar como Feito',
  dismiss: 'Fechar aviso',
  durationGroup: 'Duração do foco',
  presetMinutes: '{{count}} min',
  freeTimer: 'Cronômetro livre',
}
export default ns

declare module '../../index' {
  interface ArcoResources {
    focus: typeof ns
  }
}
