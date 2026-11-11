import type { CycleArch } from '../../api/types'
import { diffDays } from '../../lib/dates'

/** Semana (1..weeks) em que `today` cai, limitada às pontas. */
export function currentWeek(arch: Pick<CycleArch, 'startDate' | 'weeks'>, today: string): number {
  const w = Math.floor(diffDays(arch.startDate, today) / 7) + 1
  return Math.min(Math.max(w, 1), arch.weeks)
}
