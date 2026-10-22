import type { CheckInStatus, FrequencyType, Goal, Habit, XpSource } from '../api/types'
import { WEEKDAY_SHORT } from './dates'

const number = new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 2 })

export function formatNumber(value: number): string {
  return number.format(value)
}

export function formatXp(amount: number): string {
  const sign = amount > 0 ? '+' : amount < 0 ? '−' : ''
  return `${sign}${formatNumber(Math.abs(amount))} XP`
}

export const FREQUENCY_LABELS: Record<FrequencyType, string> = {
  Daily: 'Diário',
  SpecificDays: 'Dias específicos',
  WeeklyTarget: 'X vezes por semana',
}

export function frequencyText(habit: Pick<Habit, 'frequencyType' | 'frequencyDays' | 'timesPerWeek'>): string {
  switch (habit.frequencyType) {
    case 'Daily':
      return 'Todo dia'
    case 'SpecificDays': {
      const days = [...habit.frequencyDays].sort((a, b) => a - b)
      if (days.length === 7) return 'Todo dia'
      return days.map((d) => WEEKDAY_SHORT[d]).join(', ')
    }
    case 'WeeklyTarget':
      return `${habit.timesPerWeek ?? 1}x por semana`
  }
}

export const STATUS_LABELS: Record<CheckInStatus, string> = {
  Done: 'Feito',
  Minimum: 'Mínimo',
  Missed: 'Não fiz',
}

export const XP_SOURCE_LABELS: Record<XpSource, string> = {
  CheckIn: 'Check-in',
  HabitLevelUp: 'Hábito subiu de nível',
  Milestone: 'Marco concluído',
  GoalCompleted: 'Objetivo concluído',
  CycleCompleted: 'Ciclo encerrado',
}

/** XP esperado de um check-in (mesma regra do backend); usado só na atualização otimista. */
export function expectedCheckInXp(status: CheckInStatus, habitLevel: number): number {
  const done = 10 + 2 * (habitLevel - 1)
  if (status === 'Done') return done
  if (status === 'Minimum') return Math.floor(done / 2)
  return 0
}

export function goalProgressText(goal: Goal): string {
  if (goal.type === 'Milestones') {
    const done = goal.milestones.filter((m) => m.doneAt).length
    return `${done}/${goal.milestones.length} marcos`
  }
  const current = goal.currentValue ?? goal.startValue ?? 0
  const unit = goal.unit ? ` ${goal.unit}` : ''
  return `${formatNumber(current)} de ${formatNumber(goal.targetValue ?? 0)}${unit}`
}

/** Lê um número digitado em pt-BR ("5,5" ou "5.5"). null se inválido. */
export function parseDecimal(text: string): number | null {
  const normalized = text.trim().replace(/\s/g, '').replace(',', '.')
  if (!normalized) return null
  const value = Number(normalized)
  return Number.isFinite(value) ? value : null
}
