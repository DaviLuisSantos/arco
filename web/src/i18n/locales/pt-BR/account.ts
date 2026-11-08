const ns = {
  demoBanner: 'Conta de demonstração, apagada em 24h.',
  createAccount: 'Criar conta',
  verifyEmail: 'Confirme seu e-mail: enviamos um link para {{email}}.',
  resend: 'Reenviar',
  resent: 'E-mail de confirmação reenviado.',
  title: 'Conta',
  displayName: 'Nome de exibição',
  displayNameHint: 'Aparece no seu perfil público e para seus parceiros.',
  saveName: 'Salvar nome',
  nameSaved: 'Nome salvo.',
  verified: ' · verificado',
  unverified: ' · não verificado',
  googleStatus: 'Google: {{status}}',
  googleLinked: 'vinculado',
  googleNotLinked: 'não vinculado',
  changePassword: 'Trocar senha',
  setPassword: 'Definir senha',
  currentPassword: 'Senha atual',
  newPassword: 'Nova senha',
  passwordHint: 'Mínimo de 8 caracteres.',
  passwordSaved: 'Senha salva.',
  passwordShort: 'A senha precisa ter pelo menos 8 caracteres.',
  passwordCurrentRequired: 'Informe a senha atual.',
  deleteAccount: 'Excluir conta',
  deleteTitle: 'Excluir sua conta?',
  deleteDescription: 'Apaga a conta e todos os seus dados, de forma definitiva. Exporte os dados antes se quiser guardá-los.',
  deleteConfirmPassword: 'Confirme com sua senha',
  deleteConfirm: 'Excluir tudo',
}
export default ns
declare module '../../index' {
  interface ArcoResources {
    account: typeof ns
  }
}
