const ns = {
  levelAria: 'Nível {{level}}',
  level: 'Nível',
  xpToday: '{{xp}} hoje',
  zeroToday: '0 XP hoje',
  progressAria: 'Progresso para o nível {{level}}',
  remaining: 'Faltam {{xp}} XP para o nível {{level}}',
}
export default ns
declare module '../../index' {
  interface ArcoResources {
    player: typeof ns
  }
}
