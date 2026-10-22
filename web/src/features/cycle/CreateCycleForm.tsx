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
    setError(null)
    create.mutate({ name: name.trim(), startDate: validStart ? startDate : undefined, weeks })
  }

  return (
    <Card>
      <h2 className="text-lg font-bold">Comece um ciclo de 6 semanas</h2>
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
            {Array.from({ length: 12 }, (_, i) => i + 1).map((n) => (
              <option key={n} value={n}>
                {n} {n === 1 ? 'semana' : 'semanas'}
              </option>
            ))}
          </Select>
        </div>
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
