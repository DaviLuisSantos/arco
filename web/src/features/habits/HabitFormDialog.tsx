import { useQuery } from '@tanstack/react-query'
import { useId, useState, type FormEvent } from 'react'
import { goalsApi, habitsApi } from '../../api/endpoints'
import type { FrequencyType, Habit, HabitInput } from '../../api/types'
import { Button } from '../../components/ui/Button'
import { Dialog } from '../../components/ui/Dialog'
import { Input, Select } from '../../components/ui/Input'
import { Segmented } from '../../components/ui/Segmented'
import { cn } from '../../lib/cn'
import { WEEKDAY_INITIALS, WEEKDAY_NAMES } from '../../lib/dates'
import { queryKeys } from '../../lib/queryKeys'
import { useAppMutation } from '../../lib/useAppMutation'
import { LevelUpRuleField, PlannedLevelsField, type LevelUpRuleDraft, type PlannedLevelDraft } from './LadderFields'

interface HabitFormDialogProps {
  open: boolean
  onClose: () => void
  /** Ausente = criar. */
  habit?: Habit
  onSaved?: (habit: Habit) => void
}

export function HabitFormDialog({ open, onClose, habit, onSaved }: HabitFormDialogProps) {
  const formId = useId()
  const [pending, setPending] = useState(false)
  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={habit ? 'Editar hábito' : 'Novo hábito'}
      footer={
        <>
          <Button variant="secondary" block onClick={onClose}>
            Cancelar
          </Button>
          <Button type="submit" form={formId} block loading={pending}>
            {habit ? 'Salvar' : 'Criar hábito'}
          </Button>
        </>
      }
    >
      <HabitForm
        formId={formId}
        habit={habit}
        onPendingChange={setPending}
        onSaved={(h) => {
          onSaved?.(h)
          onClose()
        }}
      />
    </Dialog>
  )
}

type Errors = Partial<Record<'name' | 'emoji' | 'days' | 'level' | 'minimum' | 'reminder' | 'planned', string>>

interface HabitFormProps {
  formId: string
  habit?: Habit
  onSaved: (habit: Habit) => void
  onPendingChange: (pending: boolean) => void
}

function HabitForm({ formId, habit, onSaved, onPendingChange }: HabitFormProps) {
  const goals = useQuery({ queryKey: queryKeys.currentGoals, queryFn: () => goalsApi.list() })

  const [name, setName] = useState(habit?.name ?? '')
  const [emoji, setEmoji] = useState(habit?.emoji ?? '')
  const [goalId, setGoalId] = useState(habit?.goalId ? String(habit.goalId) : '')
  const [frequencyType, setFrequencyType] = useState<FrequencyType>(habit?.frequencyType ?? 'Daily')
  const [days, setDays] = useState<number[]>(habit?.frequencyDays ?? [1, 3, 5])
  const [timesPerWeek, setTimesPerWeek] = useState(habit?.timesPerWeek ?? 3)
  const [levelDescription, setLevelDescription] = useState(habit?.levelDescription ?? '')
  const [minimumDescription, setMinimumDescription] = useState(habit?.minimumDescription ?? '')
  const [reminderTime, setReminderTime] = useState(habit?.reminderTime ?? '')
  const [planned, setPlanned] = useState<PlannedLevelDraft[]>(
    habit?.plannedLevels.map((l) => ({ levelDescription: l.description, minimumDescription: l.minimumDescription })) ?? [],
  )
  const [rule, setRule] = useState<LevelUpRuleDraft>({
    weeks: habit?.levelUpWeeks ?? null,
    daysPerWeek: habit?.levelUpDaysPerWeek ?? 5,
  })
  const [errors, setErrors] = useState<Errors>({})

  const save = useAppMutation((input: HabitInput) => (habit ? habitsApi.update(habit.id, input) : habitsApi.create(input)), {
    successMessage: habit ? 'Hábito atualizado.' : 'Hábito criado.',
    onSuccess: (h) => onSaved(h),
  })

  const currentGoals = goals.data ?? []
  const goalFromOtherCycle = habit?.goalId != null && goals.isSuccess && !currentGoals.some((g) => g.id === habit.goalId)

  function toggleDay(day: number) {
    setDays((d) => (d.includes(day) ? d.filter((x) => x !== day) : [...d, day].sort((a, b) => a - b)))
  }

  function validate(): Errors {
    const e: Errors = {}
    if (!name.trim()) e.name = 'Dê um nome ao hábito.'
    else if (name.trim().length > 80) e.name = 'Use até 80 caracteres.'
    if (emoji.trim().length > 8) e.emoji = 'Use até 8 caracteres.'
    if (frequencyType === 'SpecificDays' && days.length === 0) e.days = 'Escolha ao menos um dia.'
    if (!levelDescription.trim()) e.level = 'Descreva a dose do nível atual.'
    else if (levelDescription.trim().length > 120) e.level = 'Use até 120 caracteres.'
    if (!minimumDescription.trim()) e.minimum = 'Descreva a versão mínima.'
    else if (minimumDescription.trim().length > 120) e.minimum = 'Use até 120 caracteres.'
    if (reminderTime && !/^([01]\d|2[0-3]):[0-5]\d$/.test(reminderTime)) e.reminder = 'Use o formato HH:mm.'
    if (planned.some((l) => !l.levelDescription.trim() || !l.minimumDescription.trim())) {
      e.planned = 'Preencha a dose e o mínimo de cada nível (ou remova os vazios).'
    }
    return e
  }

  function handleSubmit(ev: FormEvent) {
    ev.preventDefault()
    const e = validate()
    setErrors(e)
    if (Object.keys(e).length > 0) return
    onPendingChange(true)
    save.mutate(
      {
        name: name.trim(),
        emoji: emoji.trim() || null,
        goalId: goalId ? Number(goalId) : null,
        frequencyType,
        frequencyDays: frequencyType === 'SpecificDays' ? days : [],
        timesPerWeek: frequencyType === 'WeeklyTarget' ? timesPerWeek : null,
        levelDescription: levelDescription.trim(),
        minimumDescription: minimumDescription.trim(),
        reminderTime: reminderTime || null,
        plannedLevels: planned.map((l) => ({
          levelDescription: l.levelDescription.trim(),
          minimumDescription: l.minimumDescription.trim(),
        })),
        levelUpWeeks: rule.weeks,
        levelUpDaysPerWeek: rule.weeks === null ? null : rule.daysPerWeek,
      },
      { onSettled: () => onPendingChange(false) },
    )
  }

  return (
    <form id={formId} onSubmit={handleSubmit} className="flex flex-col gap-4" noValidate>
      <div className="grid grid-cols-[5rem_1fr] gap-3">
        <Input
          label="Emoji"
          value={emoji}
          onChange={(e) => setEmoji(e.target.value)}
          placeholder="🏃"
          className="text-center text-xl"
          maxLength={8}
          error={errors.emoji}
        />
        <Input
          label="Nome"
          value={name}
          onChange={(e) => setName(e.target.value)}
          placeholder="Correr"
          maxLength={80}
          error={errors.name}
        />
      </div>

      <Select
        label="Objetivo"
        value={goalId}
        onChange={(e) => setGoalId(e.target.value)}
        hint={
          goals.isSuccess && currentGoals.length === 0 ? 'Sem objetivos no ciclo atual — o vínculo é opcional.' : undefined
        }
      >
        <option value="">Nenhum</option>
        {currentGoals.map((g) => (
          <option key={g.id} value={g.id}>
            {g.title}
          </option>
        ))}
        {goalFromOtherCycle && <option value={habit?.goalId ?? ''}>Objetivo de um ciclo anterior</option>}
      </Select>

      <fieldset className="flex flex-col gap-2">
        <legend className="mb-1.5 text-sm font-medium">Frequência</legend>
        <Segmented
          label="Frequência"
          value={frequencyType}
          onChange={setFrequencyType}
          options={[
            { value: 'Daily', label: 'Diário' },
            { value: 'SpecificDays', label: 'Dias' },
            { value: 'WeeklyTarget', label: 'X/semana' },
          ]}
        />
        {frequencyType === 'SpecificDays' && (
          <div>
            <div className="flex justify-between gap-1.5" role="group" aria-label="Dias da semana">
              {WEEKDAY_INITIALS.map((initial, day) => {
                const on = days.includes(day)
                return (
                  <button
                    key={day}
                    type="button"
                    aria-pressed={on}
                    aria-label={WEEKDAY_NAMES[day]}
                    onClick={() => toggleDay(day)}
                    className={cn(
                      'grid size-10 place-items-center rounded-full border text-sm font-semibold transition',
                      on ? 'border-accent bg-accent text-on-accent' : 'border-border bg-surface-2 text-muted',
                    )}
                  >
                    {initial}
                  </button>
                )
              })}
            </div>
            {errors.days && <p className="mt-1.5 text-sm text-danger">{errors.days}</p>}
          </div>
        )}
        {frequencyType === 'WeeklyTarget' && (
          <Select
            label="Vezes por semana"
            value={timesPerWeek}
            onChange={(e) => setTimesPerWeek(Number(e.target.value))}
            hint="A semana começa na segunda."
          >
            {[1, 2, 3, 4, 5, 6, 7].map((n) => (
              <option key={n} value={n}>
                {n}x por semana
              </option>
            ))}
          </Select>
        )}
      </fieldset>

      <Input
        label={habit ? `Nível ${habit.level} — dose` : 'Nível 1 — dose'}
        value={levelDescription}
        onChange={(e) => setLevelDescription(e.target.value)}
        placeholder="20 min"
        maxLength={120}
        hint="O que conta como “Feito” neste nível."
        error={errors.level}
      />
      <Input
        label="Versão mínima"
        value={minimumDescription}
        onChange={(e) => setMinimumDescription(e.target.value)}
        placeholder="5 min"
        maxLength={120}
        hint="O mínimo para não quebrar a corrente em dias difíceis."
        error={errors.minimum}
      />
      <PlannedLevelsField
        currentLevel={habit?.level ?? 1}
        currentMinimum={minimumDescription.trim()}
        value={planned}
        onChange={setPlanned}
        error={errors.planned}
      />
      <LevelUpRuleField
        value={rule}
        onChange={setRule}
        defaultDays={
          frequencyType === 'WeeklyTarget' ? timesPerWeek : frequencyType === 'SpecificDays' ? Math.max(1, days.length) : 5
        }
      />
      <div className="flex items-end gap-2">
        <div className="min-w-0 flex-1">
          <Input
            label="Lembrete (opcional)"
            type="time"
            value={reminderTime}
            onChange={(e) => setReminderTime(e.target.value)}
            error={errors.reminder}
          />
        </div>
        {reminderTime && (
          <Button variant="ghost" className="shrink-0" onClick={() => setReminderTime('')}>
            Remover
          </Button>
        )}
      </div>
      <p className="-mt-2 text-xs text-muted">
        Notificação nos dias em que o hábito é devido e ainda não foi marcado. Ative as notificações no Perfil.
      </p>
    </form>
  )
}
