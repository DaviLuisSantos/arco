import { ApiError } from '../api/client'

const MESSAGES: Record<string, string> = {
  invalid_password: 'Senha incorreta.',
  invalid_credentials: 'E-mail ou senha incorretos.',
  email_taken: 'Já existe uma conta com esse e-mail.',
  invalid_token: 'Link inválido ou expirado. Peça um novo.',
  unauthorized: 'Sua sessão expirou. Entre novamente.',
  rate_limited: 'Muitas tentativas. Aguarde um minuto e tente de novo.',
  not_found: 'Não encontrado. Talvez já tenha sido removido.',
  active_cycle_exists: 'Já existe um ciclo ativo. Encerre-o antes de começar outro.',
  conflict: 'Essa ação já foi feita.',
  internal_error: 'Erro no servidor. Tente novamente em instantes.',
  network: 'Sem conexão com o servidor. Verifique sua internet.',
  unavailable: 'Servidor indisponível no momento. Tente de novo em instantes.',
}

/** Converte qualquer erro em uma mensagem pt-BR para o usuário. */
export function errorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.code === 'network' && !navigator.onLine) {
      return 'Você está offline. Sem internet, só o check-in funciona.'
    }
    // validation/conflict: o backend já devolve uma mensagem pt-BR específica.
    if ((error.code === 'validation' || error.code === 'conflict') && error.message) {
      return error.message
    }
    const known = MESSAGES[error.code]
    if (known) return known
    if (error.status >= 500) return MESSAGES.internal_error
    return error.message || 'Algo deu errado.'
  }
  return 'Algo deu errado. Tente novamente.'
}
