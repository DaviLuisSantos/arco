import { useId, useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { pausesApi } from '../../api/endpoints'
import { Button } from '../../components/ui/Button'
import { Dialog, useDialogGuardedClose } from '../../components/ui/Dialog'
import { Input } from '../../components/ui/Input'
import { addDays, isDateOnly } from '../../lib/dates'
import { useAppMutation } from '../../lib/useAppMutation'

interface PauseHabitDialogProps {
  open: boolean
  onClose: () => void
  habitId: number
  today: string
}

export function PauseHabitDialog({ open, onClose, habitId, today }: PauseHabitDialogProps) {
  const { t } = useTranslation('pauses', { keyPrefix: 'dialog' })
  const { t: tc } = useTranslation('common')
  const formId = useId()
  const create = useAppMutation(
    (input: { startDate: string; endDate?: string; reason?: string }) => pausesApi.create(habitId, input),
    { successMessage: t('success'), onSuccess: onClose },
  )
  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={t('title')}
      description={t('description')}
      footer={
        <>
          <CancelButton label={tc('cancel')} />
          <Button type="submit" form={formId} block loading={create.isPending}>
            {t('submit')}
          </Button>
        </>
      }
    >
      <PauseForm id={formId} today={today} onSubmit={(input) => create.mutate(input)} />
    </Dialog>
  )
}

function CancelButton({ label }: { label: string }) {
  const close = useDialogGuardedClose()
  return (
    <Button variant="secondary" block onClick={close}>
      {label}
    </Button>
  )
}

function PauseForm({
  id,
  today,
  onSubmit,
}: {
  id: string
  today: string
  onSubmit: (input: { startDate: string; endDate?: string; reason?: string }) => void
}) {
  const { t } = useTranslation('pauses', { keyPrefix: 'dialog' })
  const [startDate, setStartDate] = useState(today)
  const [endDate, setEndDate] = useState('')
  const [reason, setReason] = useState('')
  const [error, setError] = useState<string | null>(null)

  function handleSubmit(e: FormEvent) {
    e.preventDefault()
    if (!isDateOnly(startDate)) return
    if (endDate && (!isDateOnly(endDate) || endDate < startDate)) {
      setError(t('endBeforeStart'))
      return
    }
    setError(null)
    onSubmit({
      startDate,
      endDate: endDate || undefined,
      reason: reason.trim() || undefined,
    })
  }

  return (
    <form id={id} onSubmit={handleSubmit} className="flex flex-col gap-4" noValidate>
      <div className="grid grid-cols-2 gap-3">
        <Input
          label={t('start')}
          type="date"
          min={addDays(today, -7)}
          value={startDate}
          onChange={(e) => setStartDate(e.target.value)}
        />
        <Input
          label={t('end')}
          type="date"
          min={startDate}
          value={endDate}
          onChange={(e) => setEndDate(e.target.value)}
          error={error}
        />
      </div>
      <p className="-mt-2 text-xs text-muted">{t('endHint')}</p>
      <Input
        label={t('reason')}
        value={reason}
        onChange={(e) => setReason(e.target.value)}
        placeholder={t('reasonPlaceholder')}
        maxLength={140}
      />
    </form>
  )
}
