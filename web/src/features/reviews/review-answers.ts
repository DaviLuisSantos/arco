import type { ReviewAnswers } from '../../api/types'
import i18n from '../../i18n'

export const MAX_ANSWER = 2000

export interface ReviewPrompts {
  wins: string
  obstacles: string
  next: string
}

/** Lê o idioma atual a cada acesso (as constantes são usadas fora de componentes). */
function prompts(group: 'weekPrompts' | 'cyclePrompts'): ReviewPrompts {
  const keys: Array<keyof ReviewPrompts> = ['wins', 'obstacles', 'next']
  const result = {} as ReviewPrompts
  for (const key of keys)
    Object.defineProperty(result, key, { enumerable: true, get: () => i18n.t(`reviews:${group}.${key}`) })
  return result
}

export const WEEK_PROMPTS: ReviewPrompts = prompts('weekPrompts')

export const CYCLE_PROMPTS: ReviewPrompts = prompts('cyclePrompts')

/** Rascunho das respostas no formulário. */
export type Answers = Record<keyof ReviewPrompts, string>

export const EMPTY_ANSWERS: Answers = { wins: '', obstacles: '', next: '' }

/** Respostas vazias viram null. */
export function cleanAnswers(a: Answers): ReviewAnswers {
  return { wins: a.wins.trim() || null, obstacles: a.obstacles.trim() || null, next: a.next.trim() || null }
}
