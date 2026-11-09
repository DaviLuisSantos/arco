const ns = {
  email: 'E-mail',
  password: 'Senha',
  minPassword: 'Mínimo de 8 caracteres.',
  passwordTooShort: 'A senha precisa ter ao menos 8 caracteres.',
  forgot: {
    title: 'Esqueci a senha',
    typeEmail: 'Digite o e-mail.',
    sent: 'Se o e-mail estiver cadastrado e verificado, enviamos um link para redefinir a senha. Ele vale por 1 hora.',
    submit: 'Enviar link',
    back: 'Voltar para o login',
  },
  register: {
    title: 'Criar conta',
    subtitle: 'Comece seu primeiro ciclo no Arco.',
    name: 'Nome (opcional)',
    invalidEmail: 'Digite um e-mail válido.',
    submit: 'Criar conta',
    google: 'Cadastrar com Google',
    or: 'ou',
    privacyPrefix: 'Ao se cadastrar, você concorda com a',
    privacyLink: 'Política de Privacidade',
    haveAccount: 'Já tem conta?',
    login: 'Entrar',
  },
  reset: {
    title: 'Nova senha',
    invalidLink: 'Link inválido. Peça um novo em "Esqueci a senha".',
    done: 'Senha alterada. Agora é só entrar.',
    newPassword: 'Nova senha',
    submit: 'Redefinir senha',
    toLogin: 'Ir para o login',
  },
  verify: {
    title: 'Confirmação de e-mail',
    invalidLink: 'Link inválido.',
    ok: 'E-mail confirmado. Tudo certo!',
    continue: 'Continuar para o Arco',
  },
}
export default ns
declare module '../../index' {
  interface ArcoResources {
    auth: typeof ns
  }
}
