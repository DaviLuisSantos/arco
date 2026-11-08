import type { CheckInStatus, Goal, Habit, Streak, XpSource } from '../api/types'
import i18n, { currentLocale } from '../i18n'
import { WEEKDAY_SHORT } from './dates'

export function formatNumber(value: number): string {
  return new Intl.NumberFormat(currentLocale(), { maximumFractionDigits: 2 }).format(value)
}

export function formatXp(amount: number): string {
  const sign = amount > 0 ? '+' : amount < 0 ? '−' : ''
  return `${sign}${formatNumber(Math.abs(amount))} XP`
}

/** "5 dias", "1 semana"… */
export function streakText({ current, unit }: Pick<Streak, 'current' | 'unit'>): string {
  if (unit === 'Weeks') return i18n.t(current === 1 ? 'lib:streakWeek' : 'lib:streakWeeks', { count: current })
  return i18n.t(current === 1 ? 'lib:streakDay' : 'lib:streakDays', { count: current })
}

export function frequencyText(habit: Pick<Habit, 'frequencyType' | 'frequencyDays' | 'timesPerWeek'>): string {
  switch (habit.frequencyType) {
    case 'Daily':
      return i18n.t('lib:everyDay')
    case 'SpecificDays': {
      const days = [...habit.frequencyDays].sort((a, b) => a - b)
      if (days.length === 7) return i18n.t('lib:everyDay')
      return days.map((d) => WEEKDAY_SHORT[d]).join(', ')
    }
    case 'WeeklyTarget':
      return i18n.t('lib:timesPerWeek', { count: habit.timesPerWeek ?? 1 })
  }
}

/** Objeto que lê o idioma atual a cada acesso (usado fora de componentes). */
function lazyRecord<K extends string>(keys: K[], label: (key: K) => string): Record<K, string> {
  // Getters reais por chave: Object.keys/entries funcionam e o texto vem do idioma atual.
  const record = {} as Record<K, string>
  for (const key of keys) Object.defineProperty(record, key, { enumerable: true, get: () => label(key) })
  return record
}

export const STATUS_LABELS: Record<CheckInStatus, string> = lazyRecord(['Done', 'Minimum', 'Missed'], (k) =>
  i18n.t(`lib:status.${k}`),
)

export const XP_SOURCE_LABELS: Record<XpSource, string> = lazyRecord(
  ['CheckIn', 'HabitLevelUp', 'Milestone', 'GoalCompleted', 'CycleCompleted', 'WeeklyReview'],
  (k) => i18n.t(`lib:xpSource.${k}`),
)

export function formatPercent(rate: number): string {
  return `${Math.round(rate * 100)}%`
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
    return i18n.t('lib:milestones', { done, total: goal.milestones.length })
  }
  const current = goal.currentValue ?? goal.startValue ?? 0
  const unit = goal.unit ? ` ${goal.unit}` : ''
  return i18n.t('lib:progressOf', { current: formatNumber(current), target: formatNumber(goal.targetValue ?? 0), unit })
}

/** Lê um número digitado em pt-BR ("5,5" ou "5.5"). null se inválido. */
export function parseDecimal(text: string): number | null {
  const normalized = text.trim().replace(/\s/g, '').replace(',', '.')
  if (!normalized) return null
  const value = Number(normalized)
  return Number.isFinite(value) ? value : null
}

/** "1 h 25 min", "45 min", "2 h". */
export function formatDuration(min: number): string {
  const total = Math.max(0, Math.round(min))
  const h = Math.floor(total / 60)
  const m = total % 60
  if (h === 0) return i18n.t('lib:minutes', { m })
  return m === 0 ? i18n.t('lib:hours', { h }) : i18n.t('lib:hoursMinutes', { h, m })
}
