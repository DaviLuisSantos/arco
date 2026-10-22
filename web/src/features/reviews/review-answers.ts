import type { ReviewAnswers } from '../../api/types'

export const MAX_ANSWER = 2000

export interface ReviewPrompts {
  wins: string
  obstacles: string
  next: string
}

export const WEEK_PROMPTS: ReviewPrompts = {
  wins: 'O que funcionou nesta semana?',
  obstacles: 'O que atrapalhou?',
  next: 'Qual é o foco da próxima semana?',
}

export const CYCLE_PROMPTS: ReviewPrompts = {
  wins: 'O que funcionou neste ciclo?',
  obstacles: 'O que não funcionou?',
  next: 'O que levar para o próximo ciclo?',
}

/** Rascunho das respostas no formulário. */
export type Answers = Record<keyof ReviewPrompts, string>

export const EMPTY_ANSWERS: Answers = { wins: '', obstacles: '', next: '' }

/** Respostas vazias viram null. */
export function cleanAnswers(a: Answers): ReviewAnswers {
  return { wins: a.wins.trim() || null, obstacles: a.obstacles.trim() || null, next: a.next.trim() || null }
}
