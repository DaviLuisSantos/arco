import { useState, type FormEvent } from 'react'
import { cyclesApi } from '../../api/endpoints'
import type { CycleInput } from '../../api/types'
import { Button } from '../../components/ui/Button'
import { Card } from '../../components/ui/Card'
import { Input, Select } from '../../components/ui/Input'
import { addDays, formatFullDate, isDateOnly } from '../../lib/dates'
import { useAppMutation } from '../../lib/useAppMutation'

export function CreateCycleForm({ today }: { today: string }) {
  const [name, setName] = useState('')
  const [startDate, setStartDate] = useState(today)
  const [weeks, setWeeks] = useState(6)
  const [hours, setHours] = useState('7')
  const [error, setError] = useState<string | null>(null)

  const create = useAppMutation((input: CycleInput) => cyclesApi.create(input), {
    successMessage: 'Ciclo iniciado. Bora!',
  })

  const validStart = isDateOnly(startDate)

  function handleSubmit(e: FormEvent) {
    e.preventDefault()
    if (!name.trim()) {
      setError('Dê um nome ao ciclo.')
      return
    }
    const h = Number(hours.replace(',', '.'))
    if (!Number.isFinite(h) || h < 1 || h > 56) {
      setError('Informe uma meta de foco semanal entre 1 e 56 horas.')
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
      <h2 className="text-lg font-bold">Comece um ciclo de {weeks} semanas</h2>
      <p className="mt-1 mb-4 text-sm text-muted">
        Um ciclo tem começo, meio e fim. Escolha até 3 objetivos e encerre com uma revisão.
      </p>
      <form onSubmit={handleSubmit} className="flex flex-col gap-4" noValidate>
        <Input
          label="Nome"
          value={name}
          onChange={(e) => setName(e.target.value)}
          placeholder="Ex.: Primavera em movimento"
          maxLength={80}
          error={error}
        />
        <div className="grid grid-cols-2 gap-3">
          <Input label="Início" type="date" value={startDate} onChange={(e) => setStartDate(e.target.value)} />
          <Select label="Semanas" value={weeks} onChange={(e) => setWeeks(Number(e.target.value))}>
            {[2, 4, 6, 8, 10, 12].map((n) => (
              <option key={n} value={n}>
                {n} semanas
              </option>
            ))}
          </Select>
        </div>
        <Input
          label="Meta de foco por semana (h)"
          type="number"
          inputMode="decimal"
          min={0.5}
          step={0.5}
          value={hours}
          onChange={(e) => setHours(e.target.value)}
        />
        {validStart && (
          <p className="text-sm text-muted">
            Termina em {formatFullDate(addDays(startDate, weeks * 7 - 1))} · {weeks * 7} dias
          </p>
        )}
        <Button type="submit" size="lg" loading={create.isPending}>
          Começar ciclo
        </Button>
      </form>
    </Card>
  )
}
