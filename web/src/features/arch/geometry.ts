// Geometria pura do arco de pedra (porta do protótipo arco-de-pedra.html).

export const CX = 320
export const CY = 330
export const R1 = 130
export const R2 = 200
export const KEY_EXTRA = 16
/** Folga angular (rad) entre pedras. */
export const GAP = 0.012
export const PILLAR_HEIGHT = 46
export const ARCH_VIEWBOX = '0 80 640 320'
export const THICKNESS = R2 - R1

export function polar(r: number, a: number): [number, number] {
  return [CX + r * Math.cos(a), CY - r * Math.sin(a)]
}

const pt = (p: [number, number]) => `${p[0].toFixed(1)} ${p[1].toFixed(1)}`

/** Setor anular entre os raios ra..rb e os ângulos a0..a1 (rad). */
export function sector(ra: number, rb: number, a0: number, a1: number): string {
  return (
    `M${pt(polar(ra, a0))} L${pt(polar(rb, a0))}` +
    ` A${rb} ${rb} 0 0 1 ${pt(polar(rb, a1))}` +
    ` L${pt(polar(ra, a1))}` +
    ` A${ra} ${ra} 0 0 0 ${pt(polar(ra, a0))} Z`
  )
}

/** Posição (0..n-1, da esquerda p/ a direita) da semana w (1..) num arco de n pedras (semanas + chave). */
export function stoneIndex(w: number, n: number): number {
  const j = w - 1
  const k = j >> 1
  return j % 2 === 0 ? k : n - 1 - k
}

export interface StoneAngles {
  a0: number
  a1: number
}

/** Ângulos da posição idx num arco com n pedras. */
export function stoneAngles(idx: number, n: number): StoneAngles {
  const d = Math.PI / n
  return { a0: Math.PI - idx * d - GAP, a1: Math.PI - (idx + 1) * d + GAP }
}

export function stonePath(week: number, weeks: number): string {
  const { a0, a1 } = stoneAngles(stoneIndex(week, weeks + 1), weeks + 1)
  return sector(R1, R2, a0, a1)
}

/** Faixa radial de um hábito: [de, até] em raio, dado o acumulado em minutos. */
export function bandPath(week: number, weeks: number, cumMinutes: number, minutes: number, denom: number): string {
  const { a0, a1 } = stoneAngles(stoneIndex(week, weeks + 1), weeks + 1)
  const from = R1 + (THICKNESS * cumMinutes) / denom
  const to = R1 + (THICKNESS * (cumMinutes + minutes)) / denom
  return sector(from, to, a0, a1)
}

/** Posição do rótulo (fora do arco) da semana. */
export function stoneLabelPos(week: number, weeks: number): [number, number] {
  const { a0, a1 } = stoneAngles(stoneIndex(week, weeks + 1), weeks + 1)
  return polar(R2 + 18, (a0 + a1) / 2)
}

/** Pedra-chave (centro do arco, mais alta). */
export function keystonePath(weeks: number): string {
  const n = weeks + 1
  const { a0, a1 } = stoneAngles(weeks / 2, n)
  return sector(R1, R2 + KEY_EXTRA, a0, a1)
}

export function keystoneLabelPos(): [number, number] {
  return polar(R2 + KEY_EXTRA + 18, Math.PI / 2)
}

// ---------- layout Row (ciclos ímpares / semana parcial) ----------

export const ROW_STONE_W = 64
export const ROW_STONE_H = 120
export const ROW_GAP = 6
export const ROW_Y = 150
export const ROW_PAD = 20

export interface RowStone {
  x: number
  y: number
  w: number
  h: number
}

/** Retângulo da pedra da semana w (1..weeks) na fila reta. */
export function rowStone(week: number): RowStone {
  return { x: ROW_PAD + (week - 1) * (ROW_STONE_W + ROW_GAP), y: ROW_Y, w: ROW_STONE_W, h: ROW_STONE_H }
}

/** Pedra de fecho, depois da última semana. */
export function rowKeystone(weeks: number): RowStone {
  const s = rowStone(weeks + 1)
  return { ...s, y: s.y - 12, h: s.h + 12 }
}

export function rowViewBox(weeks: number): string {
  const width = ROW_PAD * 2 + (weeks + 1) * (ROW_STONE_W + ROW_GAP) - ROW_GAP
  return `0 ${ROW_Y - 40} ${width} ${ROW_STONE_H + 90}`
}

/** Faixa de um hábito dentro da pedra Row (empilha de baixo para cima). */
export function rowBand(week: number, cumMinutes: number, minutes: number, denom: number): RowStone {
  const s = rowStone(week)
  const h = (s.h * minutes) / denom
  const y = s.y + s.h - (s.h * cumMinutes) / denom - h
  return { x: s.x, y, w: s.w, h }
}
