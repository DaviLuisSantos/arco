const ns = {
  title: 'Como foi seu dia?',
  values: { v1: 'Muito ruim', v2: 'Ruim', v3: 'Neutro', v4: 'Bom', v5: 'Muito bom' },
}
export default ns

declare module '../../index' {
  interface ArcoResources {
    moods: typeof ns
  }
}
