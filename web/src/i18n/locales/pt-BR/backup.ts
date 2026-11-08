const ns = {
  title: 'Exportar meus dados',
  description: 'Baixe um arquivo JSON com seus hábitos, check-ins, ciclos, objetivos e XP.',
  export: 'Exportar dados',
  exportOffline: 'Exportar dados (precisa de internet)',
}
export default ns
declare module '../../index' {
  interface ArcoResources {
    backup: typeof ns
  }
}
