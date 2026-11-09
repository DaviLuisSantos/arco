const ns = {
  title: 'Configurações',
  subtitle: 'Ajustes da sua conta e do app.',
  back: 'Voltar',
  themeLabel: 'Tema',
  theme: { system: 'Sistema', light: 'Claro', dark: 'Escuro' },
  openSettings: 'Configurações',
  openAdmin: 'Administração',
}
export default ns
declare module '../../index' {
  interface ArcoResources {
    settings: typeof ns
  }
}
