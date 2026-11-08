import { useQuery } from '@tanstack/react-query'
import { Landmark, Pencil } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { cyclesApi } from '../../api/endpoints'
import type { ArchStone, Cycle, CycleArch } from '../../api/types'
import { Button } from '../../components/ui/Button'
import { Input } from '../../components/ui/Input'
import { EmptyState, ErrorState, Spinner } from '../../components/ui/States'
import { diffDays, formatDate, localToday } from '../../lib/dates'
import { formatDuration } from '../../lib/format'
import { habitColor } from '../../lib/habitColor'
import { queryKeys } from '../../lib/queryKeys'
import { useAppMutation } from '../../lib/useAppMutation'
import { Arch } from './Arch'
import { StoneDays } from './StoneDays'

const MIN_HOURS = 1
const MAX_HOURS = 56

interface ArchPanelProps {
  cycleId: number
  /** Ciclo ativo: habilita a edição da meta semanal (precisa de nome e fim para o PUT). */
  cycle?: Cycle
  /** Quando muda (e o ciclo está encerrado), a pedra-chave desce de novo. */
  animateKey?: number | string
}

/** Semana pré-selecionada: a de hoje (limitada às pontas) ou, se encerrado, a última. */
function defaultWeek(arch: CycleArch): number {
  if (arch.closed) return arch.weeks
  const w = Math.floor(diffDays(localToday(), arch.startDate) / 7) + 1
  return Math.min(Math.max(w, 1), arch.weeks)
}

/** Arco do ciclo: pedras por semana, legenda dos hábitos, detalhe da semana e dos dias. */
export function ArchPanel({ cycleId, cycle, animateKey }: ArchPanelProps) {
  const { t } = useTranslation('arch')
  const query = useQuery({ queryKey: queryKeys.cycleArch(cycleId), queryFn: () => cyclesApi.arch(cycleId) })
  const [picked, setPicked] = useState<number | null>(null)

  if (query.isPending) return <Spinner />
  if (query.isLoadingError) return <ErrorState error={query.error} onRetry={() => void query.refetch()} />

  const arch = query.data
  const week = picked ?? defaultWeek(arch)
  const stone = arch.stones.find((s) => s.week === week)
  const canEdit = !!cycle && cycle.status === 'Active' && !arch.closed

  return (
    <div className="flex flex-col gap-4">
      <Arch arch={arch} selectedWeek={week} onSelect={setPicked} animateKey={animateKey} />

      <div className="flex flex-wrap items-baseline justify-between gap-x-3 gap-y-1 text-sm">
        <span className="font-semibold">{t('panel.totalInCycle', { duration: formatDuration(arch.totalMinutes) })}</span>
        <span className="text-muted">{t('panel.goalPerWeek', { duration: formatDuration(arch.weeklyTargetMinutes) })}</span>
      </div>

      {canEdit && <EditGoal cycle={cycle} current={arch.weeklyTargetMinutes} />}

      {arch.habits.length === 0 ? (
        <EmptyState
          icon={<Landmark className="size-6" />}
          title={t('panel.emptyTitle')}
          description={t('panel.emptyDescription')}
        />
      ) : (
        <>
          <ul className="flex flex-wrap gap-x-4 gap-y-1.5" aria-label={t('panel.legend')}>
            {arch.habits.map((h) => (
              <li key={h.habitId} className="flex min-w-0 items-center gap-1.5 text-xs text-muted">
                <span
                  className="size-2.5 shrink-0 rounded-sm"
                  style={{ backgroundColor: habitColor(h.habitId) }}
                  aria-hidden
                />
                <span className="truncate">
                  {h.emoji ? `${h.emoji} ` : ''}
                  {h.name}
                </span>
              </li>
            ))}
          </ul>
          {stone && <WeekDetail stone={stone} arch={arch} />}
        </>
      )}
    </div>
  )
}

function WeekDetail({ stone, arch }: { stone: ArchStone; arch: CycleArch }) {
  const { t } = useTranslation('arch')
  const target = arch.weeklyTargetMinutes
  const names = new Map(arch.habits.map((h) => [h.habitId, h]))
  const bands = [...stone.byHabit].filter((b) => b.minutes > 0).sort((a, b) => b.minutes - a.minutes)

  return (
    <section aria-label={t('panel.week', { week: stone.week })} className="flex flex-col gap-3">
      <div className="flex flex-wrap items-baseline justify-between gap-x-3">
        <h3 className="text-sm font-semibold">{t('panel.week', { week: stone.week })}</h3>
        <span className="text-xs text-muted">
          {formatDate(stone.from, { day: 'numeric', month: 'short' })} –{' '}
          {formatDate(stone.to, { day: 'numeric', month: 'short' })}
        </span>
      </div>
      <p className="text-sm">
        <span className="font-semibold">{formatDuration(stone.minutes)}</span>
        <span className="text-muted"> {t('panel.of', { duration: formatDuration(target) })}</span>
      </p>
      {bands.length > 0 && (
        <ul className="flex flex-col gap-1 text-sm">
          {bands.map((b) => {
            const h = names.get(b.habitId)
            return (
              <li key={b.habitId} className="flex items-center justify-between gap-3">
                <span className="flex min-w-0 items-center gap-1.5">
                  <span
                    className="size-2.5 shrink-0 rounded-sm"
                    style={{ backgroundColor: habitColor(b.habitId) }}
                    aria-hidden
                  />
                  <span className="truncate">{h ? `${h.emoji ? `${h.emoji} ` : ''}${h.name}` : t('panel.removedHabit')}</span>
                </span>
                <span className="shrink-0 text-muted">{formatDuration(b.minutes)}</span>
              </li>
            )
          })}
        </ul>
      )}
      <div className="overflow-x-auto">
        <StoneDays stone={stone} habits={arch.habits} />
      </div>
    </section>
  )
}

function EditGoal({ cycle, current }: { cycle: Cycle; current: number }) {
  const { t } = useTranslation(['arch', 'common'])
  const [editing, setEditing] = useState(false)
  const [hours, setHours] = useState('')
  const [error, setError] = useState<string | null>(null)
  const save = useAppMutation(
    (minutes: number) =>
      cyclesApi.update(cycle.id, { name: cycle.name, endDate: cycle.endDate, weeklyFocusMinutes: minutes }),
    {
      successMessage: t('panel.goalUpdated'),
      onSuccess: () => setEditing(false),
    },
  )

  function open() {
    setHours(String(Math.round((current / 60) * 100) / 100))
    setError(null)
    setEditing(true)
  }

  function submit() {
    const h = Number(hours.replace(',', '.'))
    if (!Number.isFinite(h) || h < MIN_HOURS || h > MAX_HOURS) {
      setError(t('panel.hoursInvalid', { min: MIN_HOURS, max: MAX_HOURS }))
      return
    }
    setError(null)
    save.mutate(Math.round(h * 60))
  }

  if (!editing) {
    return (
      <div>
        <Button variant="ghost" size="sm" onClick={open}>
          <Pencil className="size-4" aria-hidden />
          {t('panel.editGoal')}
        </Button>
      </div>
    )
  }

  return (
    <form
      className="flex flex-col gap-3 rounded-2xl border border-border bg-surface-2/50 p-3"
      onSubmit={(e) => {
        e.preventDefault()
        submit()
      }}
    >
      <Input
        label={t('panel.goalLabel')}
        type="number"
        inputMode="decimal"
        min={MIN_HOURS}
        max={MAX_HOURS}
        step="0.5"
        value={hours}
        onChange={(e) => setHours(e.target.value)}
        error={error}
        hint={t('panel.hoursHint', { min: MIN_HOURS, max: MAX_HOURS })}
      />
      <div className="flex gap-2">
        <Button type="button" variant="secondary" block onClick={() => setEditing(false)}>
          {t('common:cancel')}
        </Button>
        <Button type="submit" block loading={save.isPending}>
          {t('common:save')}
        </Button>
      </div>
    </form>
  )
}
