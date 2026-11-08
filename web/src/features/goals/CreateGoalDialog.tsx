import { Plus, X } from 'lucide-react'
import { useId, useState, type FormEvent, type KeyboardEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { goalsApi } from '../../api/endpoints'
import type { Cycle, GoalInput, GoalType } from '../../api/types'
import { Button } from '../../components/ui/Button'
import { Dialog } from '../../components/ui/Dialog'
import { Input, Textarea } from '../../components/ui/Input'
import { Segmented } from '../../components/ui/Segmented'
import { isDateOnly } from '../../lib/dates'
import { parseDecimal } from '../../lib/format'
import { useAppMutation } from '../../lib/useAppMutation'

interface CreateGoalDialogProps {
  cycle: Cycle
  open: boolean
  onClose: () => void
}

export function CreateGoalDialog({ cycle, open, onClose }: CreateGoalDialogProps) {
  const { t } = useTranslation(['goals', 'common'])
  const formId = useId()
  const create = useAppMutation((input: GoalInput) => goalsApi.create(input), {
    successMessage: t('create.success'),
    onSuccess: onClose,
  })

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={t('create.title')}
      footer={
        <>
          <Button variant="secondary" block onClick={onClose}>
            {t('common:cancel')}
          </Button>
          <Button type="submit" form={formId} block loading={create.isPending}>
            {t('create.submit')}
          </Button>
        </>
      }
    >
      <GoalForm formId={formId} cycle={cycle} onSubmit={(input) => create.mutate(input)} />
    </Dialog>
  )
}

type Errors = Partial<Record<'title' | 'start' | 'target' | 'deadline' | 'milestones', string>>

function GoalForm({ formId, cycle, onSubmit }: { formId: string; cycle: Cycle; onSubmit: (input: GoalInput) => void }) {
  const { t } = useTranslation('goals')
  const [type, setType] = useState<GoalType>('Numeric')
  const [title, setTitle] = useState('')
  const [description, setDescription] = useState('')
  const [unit, setUnit] = useState('')
  const [startValue, setStartValue] = useState('')
  const [targetValue, setTargetValue] = useState('')
  const [deadline, setDeadline] = useState(cycle.endDate)
  const [milestones, setMilestones] = useState<string[]>([])
  const [draft, setDraft] = useState('')
  const [errors, setErrors] = useState<Errors>({})

  function addMilestone() {
    const text = draft.trim()
    if (!text || milestones.length >= 20) return
    setMilestones((m) => [...m, text])
    setDraft('')
  }

  function handleDraftKey(e: KeyboardEvent<HTMLInputElement>) {
    if (e.key === 'Enter') {
      e.preventDefault()
      addMilestone()
    }
  }

  function handleSubmit(e: FormEvent) {
    e.preventDefault()
    const start = parseDecimal(startValue)
    const target = parseDecimal(targetValue)
    // marco digitado e não adicionado também conta
    const allMilestones = draft.trim() ? [...milestones, draft.trim()] : milestones
    const next: Errors = {}
    if (!title.trim()) next.title = t('create.titleRequired')
    if (!isDateOnly(deadline)) next.deadline = t('create.deadlineRequired')
    if (type === 'Numeric') {
      if (start === null) next.start = t('create.startRequired')
      if (target === null) next.target = t('create.targetRequired')
      else if (start !== null && start === target) next.target = t('create.targetDiffers')
    } else if (allMilestones.length === 0) {
      next.milestones = t('create.milestoneRequired')
    } else if (allMilestones.length > 20) {
      next.milestones = t('create.milestoneMax')
    }
    setErrors(next)
    if (Object.keys(next).length > 0) return

    onSubmit({
      cycleId: cycle.id,
      title: title.trim(),
      description: description.trim() || null,
      type,
      deadline,
      ...(type === 'Numeric'
        ? { unit: unit.trim() || null, startValue: start, targetValue: target }
        : { milestones: allMilestones }),
    })
  }

  return (
    <form id={formId} onSubmit={handleSubmit} className="flex flex-col gap-4" noValidate>
      <Segmented
        label={t('create.type')}
        value={type}
        onChange={setType}
        options={[
          { value: 'Numeric', label: t('type.Numeric') },
          { value: 'Milestones', label: t('type.Milestones') },
        ]}
      />
      <Input
        label={t('create.titleLabel')}
        value={title}
        onChange={(e) => setTitle(e.target.value)}
        placeholder={type === 'Numeric' ? t('create.titlePlaceholderNumeric') : t('create.titlePlaceholderMilestones')}
        error={errors.title}
      />
      <Textarea
        label={t('create.description')}
        value={description}
        onChange={(e) => setDescription(e.target.value)}
        rows={2}
        className="min-h-0"
      />

      {type === 'Numeric' ? (
        <div className="grid grid-cols-3 gap-3">
          <Input label={t('create.unit')} value={unit} onChange={(e) => setUnit(e.target.value)} placeholder={t('create.unitPlaceholder')} />
          <Input
            label={t('create.initial')}
            inputMode="decimal"
            value={startValue}
            onChange={(e) => setStartValue(e.target.value)}
            placeholder="3"
            error={errors.start}
          />
          <Input
            label={t('create.target')}
            inputMode="decimal"
            value={targetValue}
            onChange={(e) => setTargetValue(e.target.value)}
            placeholder="10"
            error={errors.target}
          />
        </div>
      ) : (
        <div className="flex flex-col gap-2">
          <span className="text-sm font-medium">{t('create.milestones')}</span>
          {milestones.length > 0 && (
            <ol className="flex flex-col gap-1.5">
              {milestones.map((m, i) => (
                <li key={`${i}-${m}`} className="flex items-center gap-2 rounded-xl bg-surface-2 py-1.5 pr-1.5 pl-3 text-sm">
                  <span className="text-muted">{i + 1}.</span>
                  <span className="flex-1">{m}</span>
                  <button
                    type="button"
                    onClick={() => setMilestones((list) => list.filter((_, j) => j !== i))}
                    className="rounded-lg p-1.5 text-muted hover:text-danger"
                    aria-label={t('create.removeMilestone', { name: m })}
                  >
                    <X className="size-4" />
                  </button>
                </li>
              ))}
            </ol>
          )}
          <div className="flex gap-2">
            <div className="flex-1">
              <Input
                aria-label={t('create.newMilestone')}
                value={draft}
                onChange={(e) => setDraft(e.target.value)}
                onKeyDown={handleDraftKey}
                placeholder={t('create.nextMilestone')}
              />
            </div>
            <Button variant="secondary" onClick={addMilestone} aria-label={t('create.addMilestone')} disabled={milestones.length >= 20}>
              <Plus className="size-4" />
            </Button>
          </div>
          {errors.milestones && <p className="text-sm text-danger">{errors.milestones}</p>}
        </div>
      )}

      <Input
        label={t('create.deadline')}
        type="date"
        value={deadline}
        min={cycle.startDate}
        onChange={(e) => setDeadline(e.target.value)}
        hint={t('create.deadlineHint')}
        error={errors.deadline}
      />
    </form>
  )
}
