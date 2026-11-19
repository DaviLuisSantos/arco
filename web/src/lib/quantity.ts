import type { CheckInStatus } from '../api/types'

/** Maior quantidade aceita (100000,0), igual ao servidor. */
const MAX = 100000

/** "2,5" / "2.5" -> 2.5; null se vazio, ≤ 0, > 100000 ou com mais de 1 casa decimal. */
export function parseQuantity(text: string): number | null {
  const s = text.trim().replace(',', '.')
  if (!/^\d+(\.\d)?$/.test(s)) return null
  const n = Number(s)
  return n > 0 && n <= MAX ? n : null
}

/** Como `parseQuantity`, mas aceita 0 (total do dia zerado). */
export function parseTotal(text: string): number | null {
  const s = text.trim().replace(',', '.')
  if (!/^\d+(\.\d)?$/.test(s)) return null
  const n = Number(s)
  return n >= 0 && n <= MAX ? n : null
}

/** 2.5 -> "2,5"; 8 -> "8". */
export function formatQuantity(n: number | null | undefined): string {
  return n == null ? '' : String(n).replace('.', ',')
}

/** 2.5, "km" -> "2,5 km". */
export function formatWithUnit(n: number, unit: string): string {
  return `${formatQuantity(n)} ${unit}`.trim()
}

/** Meta e mínimo válidos (mínimo ≤ meta)? */
export function validTargets(target: string, minimum: string): boolean {
  const t = parseQuantity(target)
  const m = parseQuantity(minimum)
  return t !== null && m !== null && m <= t
}

const tenths = (n: number) => Math.round(n * 10)

/** Soma sem erro de ponto flutuante (1 casa decimal) e limitada ao máximo. */
export function addQuantity(total: number, delta: number): number {
  return Math.min(MAX, Math.max(0, (tenths(total) + tenths(delta)) / 10))
}

/** Status que o total dá (mesma regra do servidor): Feito na meta, Mínimo no mínimo, senão nenhum. */
export function statusFor(total: number, target: number, minimum: number): 'Done' | 'Minimum' | null {
  const t = tenths(total)
  if (t >= tenths(target)) return 'Done'
  if (t >= tenths(minimum)) return 'Minimum'
  return null
}

/**
 * Status do dia depois de gravar o total: o derivado, se houver; abaixo do mínimo, um Feito/Mínimo anterior
 * some e Não fiz/Pulei ficam como estão.
 */
export function statusAfterTotal(
  current: CheckInStatus | null,
  total: number,
  target: number,
  minimum: number,
): CheckInStatus | null {
  const derived = statusFor(total, target, minimum)
  if (derived) return derived
  return current === 'Done' || current === 'Minimum' ? null : current
}
