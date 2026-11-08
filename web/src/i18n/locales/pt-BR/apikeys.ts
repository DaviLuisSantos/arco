const ns = {
  revoked: 'Chave revogada.',
  title: 'Acesso pela API',
  descBefore: 'Tudo o que o app faz também funciona pela API, com o cabeçalho',
  descAfter: 'Cada chave dá acesso total: guarde como uma senha.',
  keyPlaceholder: 'chave',
  created: 'criada em {{date}}',
  lastUsed: 'último uso em {{date}}',
  neverUsed: 'nunca usada',
  revokeAria: 'Revogar {{name}}',
  newKey: 'Nova chave',
  revokeTitle: 'Revogar “{{name}}”?',
  revokeDescription: 'O que usa essa chave para de funcionar na hora. Não dá para desfazer.',
  revoke: 'Revogar',
  createdTitle: 'Chave criada',
  newTitle: 'Nova chave de API',
  createdDescription: 'Copie agora: por segurança, ela não será mostrada de novo.',
  newDescription: 'Dê um nome que diga onde ela será usada (ex.: “Claude”, “Atalhos do iPhone”).',
  done: 'Pronto',
  cancel: 'Cancelar',
  create: 'Criar chave',
  nameLabel: 'Nome',
  namePlaceholder: 'Claude',
  nameRequired: 'Dê um nome à chave.',
  copyAria: 'Copiar chave',
  example: 'Exemplo:',
}
export default ns
declare module '../../index' {
  interface ArcoResources {
    apikeys: typeof ns
  }
}
