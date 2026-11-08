import { useMutation, useQuery } from '@tanstack/react-query'
import { useId, useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { goalsApi, habitsApi } from '../../api/endpoints'
import type { FrequencyType, Habit, HabitInput } from '../../api/types'
import { Button } from '../../components/ui/Button'
import { Dialog } from '../../components/ui/Dialog'
import { Input, Select } from '../../components/ui/Input'
import { Segmented } from '../../components/ui/Segmented'
import { useToast } from '../../components/ui/toast-context'
import { cn } from '../../lib/cn'
import { WEEKDAY_INITIALS, WEEKDAY_NAMES } from '../../lib/dates'
import { errorMessage } from '../../lib/errors'
import { queryKeys } from '../../lib/queryKeys'
import { useInvalidateData } from '../../lib/useInvalidateData'
import { PlanLimitNotice, usePlanLimitHandler } from '../billing'
import { LevelUpRuleField, PlannedLevelsField, type LevelUpRuleDraft, type PlannedLevelDraft } from './LadderFields'

interface HabitFormDialogProps {
  open: boolean
  onClose: () => void
  /** Ausente = criar. */
  habit?: Habit
  onSaved?: (habit: Habit) => void
}

export function HabitFormDialog({ open, onClose, habit, onSaved }: HabitFormDialogProps) {
  const { t } = useTranslation(['habits', 'common'])
  const formId = useId()
  const [pending, setPending] = useState(false)
  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={habit ? t('form.editTitle') : t('form.newTitle')}
      footer={
        <>
          <Button variant="secondary" block onClick={onClose}>
            {t('common:cancel')}
          </Button>
          <Button type="submit" form={formId} block loading={pending}>
            {habit ? t('common:save') : t('form.create')}
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
  const { t } = useTranslation('habits')
  const toast = useToast()
  const invalidate = useInvalidateData()
  const handlePlanLimit = usePlanLimitHandler()
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
  const [limit, setLimit] = useState(false)

  const save = useMutation({
    mutationFn: (input: HabitInput) => (habit ? habitsApi.update(habit.id, input) : habitsApi.create(input)),
    onSuccess: (h) => {
      toast.success(habit ? t('form.updated') : t('form.created'))
      onSaved(h)
    },
    // Limite do plano: aviso com link para o Plano; outros erros, o toast genérico.
    onError: (err) => {
      if (handlePlanLimit(err)) setLimit(true)
      else toast.error(errorMessage(err))
    },
    onSettled: () => invalidate(),
  })

  const currentGoals = goals.data ?? []
  const goalFromOtherCycle = habit?.goalId != null && goals.isSuccess && !currentGoals.some((g) => g.id === habit.goalId)

  function toggleDay(day: number) {
    setDays((d) => (d.includes(day) ? d.filter((x) => x !== day) : [...d, day].sort((a, b) => a - b)))
  }

  function validate(): Errors {
    const e: Errors = {}
    if (!name.trim()) e.name = t('form.nameRequired')
    else if (name.trim().length > 80) e.name = t('form.maxChars', { max: 80 })
    if (emoji.trim().length > 8) e.emoji = t('form.maxChars', { max: 8 })
    if (frequencyType === 'SpecificDays' && days.length === 0) e.days = t('form.daysRequired')
    if (!levelDescription.trim()) e.level = t('form.levelRequired')
    else if (levelDescription.trim().length > 120) e.level = t('form.maxChars', { max: 120 })
    if (!minimumDescription.trim()) e.minimum = t('form.minimumRequired')
    else if (minimumDescription.trim().length > 120) e.minimum = t('form.maxChars', { max: 120 })
    if (reminderTime && !/^([01]\d|2[0-3]):[0-5]\d$/.test(reminderTime)) e.reminder = t('form.timeFormat')
    if (planned.some((l) => !l.levelDescription.trim() || !l.minimumDescription.trim())) {
      e.planned = t('form.plannedIncomplete')
    }
    return e
  }

  function handleSubmit(ev: FormEvent) {
    ev.preventDefault()
    const e = validate()
    setErrors(e)
    if (Object.keys(e).length > 0) return
    setLimit(false)
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
          description: l.levelDescription.trim(),
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
          label={t('form.emoji')}
          value={emoji}
          onChange={(e) => setEmoji(e.target.value)}
          placeholder="🏃"
          className="text-center text-xl"
          maxLength={8}
          error={errors.emoji}
        />
        <Input
          label={t('form.name')}
          value={name}
          onChange={(e) => setName(e.target.value)}
          placeholder={t('form.namePlaceholder')}
          maxLength={80}
          error={errors.name}
        />
      </div>

      <Select
        label={t('form.goal')}
        value={goalId}
        onChange={(e) => setGoalId(e.target.value)}
        hint={
          goals.isSuccess && currentGoals.length === 0 ? t('form.noGoals') : undefined
        }
      >
        <option value="">{t('form.none')}</option>
        {currentGoals.map((g) => (
          <option key={g.id} value={g.id}>
            {g.title}
          </option>
        ))}
        {goalFromOtherCycle && <option value={habit?.goalId ?? ''}>{t('form.otherCycleGoal')}</option>}
      </Select>

      <fieldset className="flex flex-col gap-2">
        <legend className="mb-1.5 text-sm font-medium">{t('form.frequency')}</legend>
        <Segmented
          label={t('form.frequency')}
          value={frequencyType}
          onChange={setFrequencyType}
          options={[
            { value: 'Daily', label: t('form.freq.Daily') },
            { value: 'SpecificDays', label: t('form.freq.SpecificDays') },
            { value: 'WeeklyTarget', label: t('form.freq.WeeklyTarget') },
          ]}
        />
        {frequencyType === 'SpecificDays' && (
          <div>
            <div className="flex justify-between gap-1.5" role="group" aria-label={t('form.weekdays')}>
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
            label={t('form.timesPerWeek')}
            value={timesPerWeek}
            onChange={(e) => setTimesPerWeek(Number(e.target.value))}
            hint={t('form.weekStartsMonday')}
          >
            {[1, 2, 3, 4, 5, 6, 7].map((n) => (
              <option key={n} value={n}>
                {t('form.nTimesPerWeek', { n })}
              </option>
            ))}
          </Select>
        )}
      </fieldset>

      <Input
        label={habit ? t('form.levelDose', { level: habit.level }) : t('form.levelDose', { level: 1 })}
        value={levelDescription}
        onChange={(e) => setLevelDescription(e.target.value)}
        placeholder={t('form.dosePlaceholder')}
        maxLength={120}
        hint={t('form.doseHint')}
        error={errors.level}
      />
      <Input
        label={t('form.minimum')}
        value={minimumDescription}
        onChange={(e) => setMinimumDescription(e.target.value)}
        placeholder={t('form.minimumPlaceholder')}
        maxLength={120}
        hint={t('form.minimumHint')}
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
            label={t('form.reminder')}
            type="time"
            value={reminderTime}
            onChange={(e) => setReminderTime(e.target.value)}
            error={errors.reminder}
          />
        </div>
        {reminderTime && (
          <Button variant="ghost" className="shrink-0" onClick={() => setReminderTime('')}>
            {t('form.removeReminder')}
          </Button>
        )}
      </div>
      <p className="-mt-2 text-xs text-muted">
        {t('form.reminderHint')}
      </p>
      {limit && <PlanLimitNotice />}
    </form>
  )
}
