import { ApiError } from '../api/client'
import i18n from '../i18n'
import errorsPt from '../i18n/locales/pt-BR/errors'

type ErrorKey = keyof typeof errorsPt

function msg(key: ErrorKey): string {
  return i18n.t(`errors:${key}`)
}

function isKnown(code: string): code is ErrorKey {
  return Object.prototype.hasOwnProperty.call(errorsPt, code)
}

/** Converte qualquer erro em uma mensagem no idioma atual para o usuário. */
export function errorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    // Em pt-BR o backend já devolve mensagens específicas; em outros idiomas, valem as do namespace `errors`.
    const pt = i18n.language.toLowerCase().startsWith('pt')
    if (error.code === 'network' && !navigator.onLine) return msg('offline')
    if (pt && (error.code === 'validation' || error.code === 'conflict') && error.message) return error.message
    if (isKnown(error.code)) return msg(error.code)
    if (error.status >= 500) return msg('internal_error')
    return (pt && error.message) || msg('generic')
  }
  return msg('genericRetry')
}
