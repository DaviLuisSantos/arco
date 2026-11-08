import { useId, useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { habitsApi } from '../../api/endpoints'
import type { Habit, LevelUpInput } from '../../api/types'
import { Button } from '../../components/ui/Button'
import { Dialog } from '../../components/ui/Dialog'
import { Input } from '../../components/ui/Input'
import { useAppMutation } from '../../lib/useAppMutation'

interface LevelUpDialogProps {
  habit: Habit
  open: boolean
  onClose: () => void
}

export function LevelUpDialog({ habit, open, onClose }: LevelUpDialogProps) {
  const { t } = useTranslation(['habits', 'common'])
  const formId = useId()
  const levelUp = useAppMutation((input: LevelUpInput) => habitsApi.levelUp(habit.id, input), {
    onSuccess: onClose,
  })

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={t('levelUp.title', { level: habit.level + 1 })}
      description={
        <>
          {t('levelUp.today', { dose: habit.levelDescription, minimum: habit.minimumDescription })}{' '}
          {habit.plannedLevels.length > 0 ? t('levelUp.plannedNext') : t('levelUp.nextDose')}{' '}
          <span className="font-semibold text-xp">+50 XP</span>
        </>
      }
      footer={
        <>
          <Button variant="secondary" block onClick={onClose}>
            {t('common:cancel')}
          </Button>
          <Button type="submit" form={formId} block loading={levelUp.isPending}>
            {t('levelUp.submit')}
          </Button>
        </>
      }
    >
      <LevelUpForm formId={formId} habit={habit} onSubmit={(input) => levelUp.mutate(input)} />
    </Dialog>
  )
}

function LevelUpForm({
  formId,
  habit,
  onSubmit,
}: {
  formId: string
  habit: Habit
  onSubmit: (input: LevelUpInput) => void
}) {
  const { t } = useTranslation('habits')
  const next = habit.plannedLevels[0]
  const [levelDescription, setLevelDescription] = useState(next?.description ?? '')
  const [minimumDescription, setMinimumDescription] = useState(next?.minimumDescription ?? '')
  const [errors, setErrors] = useState<{ level?: string; minimum?: string }>({})

  function handleSubmit(e: FormEvent) {
    e.preventDefault()
    const found = {
      level: levelDescription.trim() ? undefined : t('levelUp.doseRequired'),
      minimum: minimumDescription.trim() ? undefined : t('levelUp.minimumRequired'),
    }
    setErrors(found)
    if (found.level || found.minimum) return
    onSubmit({ levelDescription: levelDescription.trim(), minimumDescription: minimumDescription.trim() })
  }

  return (
    <form id={formId} onSubmit={handleSubmit} className="flex flex-col gap-4" noValidate>
      <Input
        label={t('levelUp.newDose')}
        value={levelDescription}
        onChange={(e) => setLevelDescription(e.target.value)}
        placeholder="30 min"
        maxLength={120}
        autoFocus={!next}
        error={errors.level}
      />
      <Input
        label={t('levelUp.newMinimum')}
        value={minimumDescription}
        onChange={(e) => setMinimumDescription(e.target.value)}
        placeholder="10 min"
        maxLength={120}
        error={errors.minimum}
      />
    </form>
  )
}
