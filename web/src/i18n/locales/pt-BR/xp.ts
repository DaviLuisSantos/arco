const ns = {
  levelUp: 'Subiu de nível!',
  cont: 'Continuar',
  level: 'Nível {{level}}!',
  tagline: 'Cada check-in conta. Continue puxando o arco.',
}
export default ns
declare module '../../index' {
  interface ArcoResources {
    xp: typeof ns
  }
}
