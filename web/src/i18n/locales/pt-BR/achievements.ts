const ns = {
  title: 'Conquistas',
  count: '{{done}} de {{total}}',
  progress: '{{title}}: progresso',
}
export default ns
declare module '../../index' {
  interface ArcoResources {
    achievements: typeof ns
  }
}
