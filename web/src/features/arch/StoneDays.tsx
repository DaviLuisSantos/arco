import { useMemo } from 'react'
import type { ArchHabit, ArchStone } from '../../api/types'
import { WEEKDAY_SHORT, dayOfWeek, localToday } from '../../lib/dates'
import { formatDuration } from '../../lib/format'
import { habitColor } from '../../lib/habitColor'

interface StoneDaysProps {
  stone: ArchStone
  habits: ArchHabit[]
}

const COL_W = 42
const BRICK_W = 32
const BASE = 106
const PX_PER_MIN = 0.3
const GAP = 1.5
const MAX_H = BASE - 8
const SOURCE_LABEL = { Timer: 'cronômetro', Manual: 'manual' } as const

/** Sete colunas (dias da semana do ciclo); cada sessão de foco é um tijolo. */
export function StoneDays({ stone, habits }: StoneDaysProps) {
  const today = localToday()
  const names = useMemo(() => new Map(habits.map((h) => [h.habitId, h.name])), [habits])

  // Reduz a escala se a coluna mais alta não couber.
  const scale = useMemo(() => {
    const tallest = Math.max(
      0,
      ...stone.days.map((d) => d.bricks.reduce((sum, b) => sum + Math.max(3, b.minutes * PX_PER_MIN) + GAP, 0)),
    )
    return tallest > MAX_H ? MAX_H / tallest : 1
  }, [stone.days])

  const summary = stone.days
    .map((d) => `${WEEKDAY_SHORT[dayOfWeek(d.date)]}: ${formatDuration(d.minutes)}`)
    .join(', ')

  return (
    <svg
      className="mt-1 block h-auto w-full"
      viewBox="0 0 300 132"
      role="img"
      aria-label={`Sessões de foco por dia na semana ${stone.week}. ${summary}`}
    >
      <line x1={2} x2={298} y1={BASE} y2={BASE} className="stroke-border" strokeWidth={1.5} />
      {stone.days.map((day, i) => {
        const x = 6 + i * COL_W
        const isToday = day.date === today
        const future = day.date > today
        let y = BASE
        return (
          <g key={day.date}>
            {isToday && (
              <rect x={x - 4} y={2} width={40} height={BASE - 2} rx={6} className="fill-accent" opacity={0.12} />
            )}
            {day.bricks.map((b) => {
              const h = Math.max(3, b.minutes * PX_PER_MIN) * scale - GAP
              y -= h + GAP
              const name = names.get(b.habitId) ?? 'Hábito'
              return (
                <rect
                  key={b.sessionId}
                  x={x}
                  y={y.toFixed(1)}
                  width={BRICK_W}
                  height={Math.max(h, 1).toFixed(1)}
                  rx={2}
                  style={{ fill: habitColor(b.habitId) }}
                >
                  <title>{`${name} · ${formatDuration(b.minutes)} · ${SOURCE_LABEL[b.source]}`}</title>
                </rect>
              )
            })}
            <text
              x={x + BRICK_W / 2}
              y={124}
              textAnchor="middle"
              className={`text-[11px] ${isToday ? 'fill-accent font-bold' : 'fill-muted'} ${future ? 'opacity-60' : ''}`}
            >
              {isToday ? 'hoje' : WEEKDAY_SHORT[dayOfWeek(day.date)]}
            </text>
          </g>
        )
      })}
    </svg>
  )
}
