import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { cyclesApi } from '../../api/endpoints'
import type { CycleInput } from '../../api/types'
import { Button } from '../../components/ui/Button'
import { Card } from '../../components/ui/Card'
import { Input, Select } from '../../components/ui/Input'
import { addDays, formatFullDate, isDateOnly } from '../../lib/dates'
import { useAppMutation } from '../../lib/useAppMutation'

export function CreateCycleForm({ today }: { today: string }) {
  const { t } = useTranslation('cycle')
  const [name, setName] = useState('')
  const [startDate, setStartDate] = useState(today)
  const [weeks, setWeeks] = useState(6)
  const [hours, setHours] = useState('7')
  const [error, setError] = useState<string | null>(null)

  const create = useAppMutation((input: CycleInput) => cyclesApi.create(input), {
    successMessage: t('create.success'),
  })

  const validStart = isDateOnly(startDate)

  function handleSubmit(e: FormEvent) {
    e.preventDefault()
    if (!name.trim()) {
      setError(t('create.nameRequired'))
      return
    }
    const h = Number(hours.replace(',', '.'))
    if (!Number.isFinite(h) || h < 1 || h > 56) {
      setError(t('create.hoursInvalid'))
      return
    }
    setError(null)
    create.mutate({
      name: name.trim(),
      startDate: validStart ? startDate : undefined,
      weeks,
      weeklyFocusMinutes: Math.round(h * 60),
    })
  }

  return (
    <Card>
      <h2 className="text-lg font-bold">{t('create.title', { weeks })}</h2>
      <p className="mt-1 mb-4 text-sm text-muted">
        {t('create.intro')}
      </p>
      <form onSubmit={handleSubmit} className="flex flex-col gap-4" noValidate>
        <Input
          label={t('create.name')}
          value={name}
          onChange={(e) => setName(e.target.value)}
          placeholder={t('create.namePlaceholder')}
          maxLength={80}
          error={error}
        />
        <div className="grid grid-cols-2 gap-3">
          <Input label={t('create.start')} type="date" value={startDate} onChange={(e) => setStartDate(e.target.value)} />
          <Select label={t('create.weeks')} value={weeks} onChange={(e) => setWeeks(Number(e.target.value))}>
            {[2, 4, 6, 8, 10, 12].map((n) => (
              <option key={n} value={n}>
                {t('create.weeksOption', { count: n })}
              </option>
            ))}
          </Select>
        </div>
        <Input
          label={t('create.focusGoal')}
          type="number"
          inputMode="decimal"
          min={0.5}
          step={0.5}
          value={hours}
          onChange={(e) => setHours(e.target.value)}
        />
        {validStart && (
          <p className="text-sm text-muted">
            {t('create.ends', { date: formatFullDate(addDays(startDate, weeks * 7 - 1)), days: weeks * 7 })}
          </p>
        )}
        <Button type="submit" size="lg" loading={create.isPending}>
          {t('create.submit')}
        </Button>
      </form>
    </Card>
  )
}
