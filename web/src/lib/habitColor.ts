/** Matiz (0–359) determinístico a partir do id do hábito. */
export function habitHue(habitId: number): number {
  // 137.5 = ângulo áureo: ids vizinhos ficam bem distantes no círculo.
  return Math.round((((habitId * 137.5) % 360) + 360) % 360)
}

/** Cor do hábito; saturação e luminosidade vêm dos tokens do tema (index.css). */
export function habitColor(habitId: number): string {
  return `hsl(${habitHue(habitId)} var(--habit-s) var(--habit-l))`
}
