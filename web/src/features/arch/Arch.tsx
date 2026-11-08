import { useEffect, useMemo, useState } from 'react'
import type { KeyboardEvent } from 'react'
import { useTranslation } from 'react-i18next'
import type { ArchStone, CycleArch } from '../../api/types'
import { diffDays, localToday } from '../../lib/dates'
import { formatDuration } from '../../lib/format'
import { habitColor } from '../../lib/habitColor'
import {
  ARCH_VIEWBOX,
  CX,
  CY,
  PILLAR_HEIGHT,
  R1,
  R2,
  THICKNESS,
  bandPath,
  keystoneLabelPos,
  keystonePath,
  rowBand,
  rowKeystone,
  rowStone,
  rowViewBox,
  stoneLabelPos,
  stonePath,
  ROW_PAD,
  ROW_Y,
} from './geometry'

type StoneState = 'done' | 'now' | 'future'

interface ArchProps {
  arch: CycleArch
  selectedWeek?: number
  onSelect?: (week: number) => void
  /** Miniatura: sem texto e sem interação. */
  small?: boolean
  /** Quando muda (e o ciclo está encerrado), a pedra-chave desce de novo. */
  animateKey?: number | string
}

function stoneState(stone: ArchStone, today: string, closed: boolean): StoneState {
  if (closed) return 'done'
  if (stone.from > today) return 'future'
  if (stone.to < today) return 'done'
  return 'now'
}

/** Semana (1..weeks) em que "hoje" cai; limitada às pontas do ciclo. */
function currentWeek(arch: CycleArch, today: string): number {
  const w = Math.floor(diffDays(today, arch.startDate) / 7) + 1
  return Math.min(Math.max(w, 1), arch.weeks)
}

/** Pedra-chave: desce até encaixar quando o ciclo é encerrado. */
function useKeystoneSet(closed: boolean, animateKey: number | string | undefined, small: boolean) {
  const animated = closed && animateKey !== undefined && !small
  const [droppedKey, setDroppedKey] = useState<number | string | undefined>(undefined)
  useEffect(() => {
    if (!animated) return
    let inner = 0
    const outer = requestAnimationFrame(() => {
      // Dois quadros: o primeiro pinta a pedra no alto, o segundo dispara a transição.
      inner = requestAnimationFrame(() => setDroppedKey(animateKey))
    })
    return () => {
      cancelAnimationFrame(outer)
      cancelAnimationFrame(inner)
    }
  }, [animated, animateKey])
  return closed && (!animated || droppedKey === animateKey)
}

const STONE_BASE = 'stroke-[1.5] transition-[stroke] duration-150'

function stoneBaseClass(state: StoneState, selected: boolean, interactive: boolean): string {
  if (selected) return `${STONE_BASE} fill-surface-2 stroke-accent stroke-[3]`
  const hover = interactive ? ' group-hover:stroke-accent group-focus-visible:stroke-accent group-focus-visible:stroke-[3]' : ''
  if (state === 'future') return `${STONE_BASE} fill-none stroke-muted opacity-50${hover}`
  return `${STONE_BASE} fill-surface-2 stroke-border${hover}`
}

export function Arch({ arch, selectedWeek, onSelect, small = false, animateKey }: ArchProps) {
  const { t } = useTranslation('arch')
  const today = localToday()
  const interactive = !small && !!onSelect
  const keySet = useKeystoneSet(arch.closed, animateKey, small)
  const isRow = arch.layout === 'Row'
  const target = arch.weeklyTargetMinutes
  const cur = currentWeek(arch, today)
  const started = today >= arch.startDate

  const habitOrder = useMemo(() => arch.habits.map((h) => h.habitId), [arch.habits])
  const stones = useMemo(() => [...arch.stones].sort((a, b) => a.week - b.week), [arch.stones])

  /** Faixas da pedra: [habitId, minutos] na ordem dos hábitos do ciclo. */
  function bandsOf(stone: ArchStone): [number, number][] {
    const by = new Map(stone.byHabit.map((b) => [b.habitId, b.minutes]))
    const ids = [...habitOrder, ...stone.byHabit.map((b) => b.habitId).filter((id) => !habitOrder.includes(id))]
    return ids.map((id) => [id, by.get(id) ?? 0] as [number, number]).filter(([, m]) => m > 0)
  }

  function stoneLabel(stone: ArchStone, state: StoneState): string {
    return t('stoneLabel', {
      week: stone.week,
      minutes: formatDuration(stone.minutes),
      target: formatDuration(target),
      state: t(`state.${state}`),
    })
  }

  const svgLabel =
    t('svgLabel', { count: arch.weeks, total: formatDuration(arch.totalMinutes) }) +
    (arch.closed ? t('svgLabelClosed') : '')

  const subText = arch.closed
    ? t('sub.closed')
    : !started
      ? t('sub.notStarted')
      : t('sub.week', { current: cur, total: arch.weeks })

  function handleKey(e: KeyboardEvent, week: number) {
    if (e.key === 'Enter' || e.key === ' ') {
      e.preventDefault()
      onSelect?.(week)
    }
  }

  function stoneProps(stone: ArchStone, state: StoneState) {
    if (!interactive) return {}
    return {
      role: 'button' as const,
      tabIndex: 0,
      'aria-label': stoneLabel(stone, state),
      'aria-pressed': selectedWeek === stone.week,
      onClick: () => onSelect?.(stone.week),
      onKeyDown: (e: KeyboardEvent) => handleKey(e, stone.week),
    }
  }

  const keyTransform = {
    transform: `translateY(${keySet ? 0 : -34}px)`,
    transition: 'transform 0.7s cubic-bezier(0.55, 0, 0.85, 0.35)',
  }
  const keyClass = `motion-reduce:!transition-none ${
    keySet ? 'fill-accent stroke-accent' : 'fill-none stroke-muted opacity-60'
  } stroke-[1.5]`
  const keyDash = keySet ? undefined : '5 5'

  const svgClass = 'block h-auto w-full overflow-visible'

  if (isRow) {
    const ks = rowKeystone(arch.weeks)
    return (
      <svg className={svgClass} viewBox={rowViewBox(arch.weeks)} role="img" aria-label={svgLabel}>
        <line
          className="stroke-border"
          strokeWidth={2}
          x1={ROW_PAD - 10}
          x2={ks.x + ks.w + 10}
          y1={ROW_Y + 120 + 8}
          y2={ROW_Y + 120 + 8}
        />
        {stones.map((stone) => {
          const state = stoneState(stone, today, arch.closed)
          const r = rowStone(stone.week)
          const denom = Math.max(stone.minutes, target, 1)
          let cum = 0
          return (
            <g
              key={stone.week}
              className={`group outline-none ${interactive ? 'cursor-pointer' : ''}`}
              {...stoneProps(stone, state)}
            >
              <rect
                x={r.x}
                y={r.y}
                width={r.w}
                height={r.h}
                rx={4}
                strokeDasharray={state === 'future' ? '5 5' : undefined}
                className={stoneBaseClass(state, selectedWeek === stone.week, interactive)}
              />
              {bandsOf(stone).map(([habitId, minutes]) => {
                const b = rowBand(stone.week, cum, minutes, denom)
                cum += minutes
                return (
                  <rect
                    key={habitId}
                    x={b.x}
                    y={b.y}
                    width={b.w}
                    height={b.h}
                    rx={2}
                    style={{ fill: habitColor(habitId) }}
                    className="pointer-events-none"
                  />
                )
              })}
              {!small && (
                <text
                  x={r.x + r.w / 2}
                  y={r.y + r.h + 26}
                  textAnchor="middle"
                  className={`pointer-events-none text-[13px] ${
                    state === 'now' ? 'fill-accent font-bold' : 'fill-muted'
                  }`}
                >
                  {stone.week}
                </text>
              )}
            </g>
          )
        })}
        <g style={keyTransform} className={keyClass}>
          <rect
            x={ks.x}
            y={ks.y}
            width={ks.w}
            height={ks.h}
            rx={4}
            strokeDasharray={keyDash}
            className="fill-inherit stroke-inherit"
          />
        </g>
        {!small && (
          <>
            <text x={ks.x + ks.w / 2} y={ks.y + ks.h + 26} textAnchor="middle" className="fill-muted text-[13px]">
              {t('end')}
            </text>
            <text x={ROW_PAD} y={ROW_Y - 16} className="fill-text text-[22px] font-bold">
              {formatDuration(arch.totalMinutes)}
            </text>
            <text x={ROW_PAD} y={ROW_Y - 36} className="fill-muted text-[13px]">
              {subText}
            </text>
          </>
        )}
      </svg>
    )
  }

  const [kx, ky] = keystoneLabelPos()
  const pillarW = THICKNESS
  return (
    <svg className={svgClass} viewBox={ARCH_VIEWBOX} role="img" aria-label={svgLabel}>
      <rect
        x={CX - R2}
        y={CY}
        width={pillarW}
        height={PILLAR_HEIGHT}
        className="fill-surface-2 stroke-border"
        strokeWidth={1.5}
      />
      <rect
        x={CX + R1}
        y={CY}
        width={pillarW}
        height={PILLAR_HEIGHT}
        className="fill-surface-2 stroke-border"
        strokeWidth={1.5}
      />
      <line
        x1={40}
        x2={600}
        y1={CY + PILLAR_HEIGHT}
        y2={CY + PILLAR_HEIGHT}
        className="stroke-border"
        strokeWidth={2}
      />
      {stones.map((stone) => {
        const state = stoneState(stone, today, arch.closed)
        const denom = Math.max(stone.minutes, target, 1)
        const [lx, ly] = stoneLabelPos(stone.week, arch.weeks)
        let cum = 0
        return (
          <g
            key={stone.week}
            className={`group outline-none ${interactive ? 'cursor-pointer' : ''}`}
            {...stoneProps(stone, state)}
          >
            <path
              d={stonePath(stone.week, arch.weeks)}
              strokeDasharray={state === 'future' ? '5 5' : undefined}
              className={stoneBaseClass(state, selectedWeek === stone.week, interactive)}
            />
            {bandsOf(stone).map(([habitId, minutes]) => {
              const d = bandPath(stone.week, arch.weeks, cum, minutes, denom)
              cum += minutes
              return <path key={habitId} d={d} style={{ fill: habitColor(habitId) }} className="pointer-events-none" />
            })}
            {!small && (
              <text
                x={lx.toFixed(1)}
                y={(ly + 4).toFixed(1)}
                textAnchor="middle"
                className={`pointer-events-none text-[13px] ${
                  state === 'now' ? 'fill-accent font-bold' : 'fill-muted'
                }`}
              >
                {stone.week}
              </text>
            )}
          </g>
        )
      })}
      <g style={keyTransform} className={keyClass}>
        <path
          d={keystonePath(arch.weeks)}
          strokeDasharray={keyDash}
          className="fill-inherit stroke-inherit"
        />
      </g>
      {!small && (
        <>
          <text x={kx.toFixed(1)} y={(ky + 4).toFixed(1)} textAnchor="middle" className="fill-muted text-[13px]">
            {t('end')}
          </text>
          <text x={CX} y={CY - 30} textAnchor="middle" className="fill-text text-[34px] font-bold tabular-nums">
            {formatDuration(arch.totalMinutes)}
          </text>
          <text x={CX} y={CY - 4} textAnchor="middle" className="fill-muted text-[15px]">
            {subText}
          </text>
        </>
      )}
    </svg>
  )
}
