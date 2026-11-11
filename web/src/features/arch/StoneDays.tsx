import { useMemo } from 'react'
import { useTranslation } from 'react-i18next'
import type { ArchHabit, ArchStone } from '../../api/types'
import { WEEKDAY_SHORT, dayOfWeek } from '../../lib/dates'
import { formatDuration } from '../../lib/format'
import { habitColor } from '../../lib/habitColor'

interface StoneDaysProps {
  stone: ArchStone
  habits: ArchHabit[]
  /** "Hoje" no fuso do usuário. */
  today: string
}

const COL_W = 42
const BRICK_W = 32
const BASE = 106
const PX_PER_MIN = 0.3
const GAP = 1.5
const MAX_H = BASE - 8
const MAX_SCALE = 3

/** Sete colunas (dias da semana do ciclo); cada sessão de foco é um tijolo. */
export function StoneDays({ stone, habits, today }: StoneDaysProps) {
  const { t } = useTranslation('arch')
  const names = useMemo(() => new Map(habits.map((h) => [h.habitId, h.name])), [habits])

  // Ajusta a escala: reduz se a coluna mais alta não couber e amplia (até MAX_SCALE) se sobrar espaço.
  const scale = useMemo(() => {
    const tallest = Math.max(
      0,
      ...stone.days.map((d) => d.bricks.reduce((sum, b) => sum + Math.max(3, b.minutes * PX_PER_MIN) + GAP, 0)),
    )
    return tallest > 0 ? Math.min(MAX_H / tallest, MAX_SCALE) : 1
  }, [stone.days])

  const summary = stone.days
    .map((d) => `${WEEKDAY_SHORT[dayOfWeek(d.date)]}: ${formatDuration(d.minutes)}`)
    .join(', ')

  return (
    <svg
      className="mx-auto mt-1 block h-auto w-full max-w-sm"
      viewBox="0 0 300 132"
      role="img"
      aria-label={t('stoneDays.label', { week: stone.week, summary })}
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
              const name = names.get(b.habitId) ?? t('stoneDays.habit')
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
                  <title>{`${name} · ${formatDuration(b.minutes)} · ${t(`stoneDays.source.${b.source}`)}`}</title>
                </rect>
              )
            })}
            <text
              x={x + BRICK_W / 2}
              y={124}
              textAnchor="middle"
              className={`text-[11px] ${isToday ? 'fill-accent font-bold' : 'fill-muted'} ${future ? 'opacity-60' : ''}`}
            >
              {isToday ? t('stoneDays.today') : WEEKDAY_SHORT[dayOfWeek(day.date)]}
            </text>
          </g>
        )
      })}
    </svg>
  )
}
