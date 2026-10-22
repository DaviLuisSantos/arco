import { useId, useState, type FormEvent } from 'react'
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
  const formId = useId()
  const levelUp = useAppMutation((input: LevelUpInput) => habitsApi.levelUp(habit.id, input), {
    onSuccess: onClose,
  })

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={`Subir para o nível ${habit.level + 1}`}
      description={
        <>
          Hoje: “{habit.levelDescription}” (mínimo “{habit.minimumDescription}”). Qual é a próxima dose?{' '}
          <span className="font-semibold text-xp">+50 XP</span>
        </>
      }
      footer={
        <>
          <Button variant="secondary" block onClick={onClose}>
            Cancelar
          </Button>
          <Button type="submit" form={formId} block loading={levelUp.isPending}>
            Subir de nível
          </Button>
        </>
      }
    >
      <LevelUpForm formId={formId} onSubmit={(input) => levelUp.mutate(input)} />
    </Dialog>
  )
}

function LevelUpForm({ formId, onSubmit }: { formId: string; onSubmit: (input: LevelUpInput) => void }) {
  const [levelDescription, setLevelDescription] = useState('')
  const [minimumDescription, setMinimumDescription] = useState('')
  const [errors, setErrors] = useState<{ level?: string; minimum?: string }>({})

  function handleSubmit(e: FormEvent) {
    e.preventDefault()
    const next = {
      level: levelDescription.trim() ? undefined : 'Descreva a nova dose.',
      minimum: minimumDescription.trim() ? undefined : 'Descreva o novo mínimo.',
    }
    setErrors(next)
    if (next.level || next.minimum) return
    onSubmit({ levelDescription: levelDescription.trim(), minimumDescription: minimumDescription.trim() })
  }

  return (
    <form id={formId} onSubmit={handleSubmit} className="flex flex-col gap-4" noValidate>
      <Input
        label="Nova dose"
        value={levelDescription}
        onChange={(e) => setLevelDescription(e.target.value)}
        placeholder="30 min"
        maxLength={120}
        autoFocus
        error={errors.level}
      />
      <Input
        label="Novo mínimo"
        value={minimumDescription}
        onChange={(e) => setMinimumDescription(e.target.value)}
        placeholder="10 min"
        maxLength={120}
        error={errors.minimum}
      />
    </form>
  )
}
