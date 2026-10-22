import { Plus, X } from 'lucide-react'
import { Button } from '../../components/ui/Button'
import { Input, Select } from '../../components/ui/Input'
import { Segmented } from '../../components/ui/Segmented'

export interface PlannedLevelDraft {
  levelDescription: string
  minimumDescription: string
}

/** Próximos níveis do hábito (a escada). Ao subir de nível, o primeiro da lista já vem preenchido. */
export function PlannedLevelsField({
  currentLevel,
  currentMinimum,
  value,
  onChange,
  error,
}: {
  currentLevel: number
  /** Mínimo sugerido para o primeiro nível novo (os seguintes repetem o anterior). */
  currentMinimum: string
  value: PlannedLevelDraft[]
  onChange: (value: PlannedLevelDraft[]) => void
  error?: string
}) {
  function update(index: number, patch: Partial<PlannedLevelDraft>) {
    onChange(value.map((l, i) => (i === index ? { ...l, ...patch } : l)))
  }

  return (
    <fieldset className="flex flex-col gap-2">
      <legend className="mb-1 text-sm font-medium">Próximos níveis (opcional)</legend>
      <p className="-mt-1 text-xs text-muted">Planeje a escada. Ao subir de nível, o próximo degrau já vem preenchido.</p>
      {value.map((level, i) => {
        const n = currentLevel + i + 1
        return (
          <div key={i} className="flex flex-col gap-2 rounded-xl border border-border p-3">
            <div className="flex items-center justify-between">
              <span className="text-sm font-semibold">Nível {n}</span>
              <Button
                variant="ghost"
                size="sm"
                aria-label={`Remover nível ${n}`}
                onClick={() => onChange(value.filter((_, j) => j !== i))}
              >
                <X className="size-4" aria-hidden />
              </Button>
            </div>
            <Input
              aria-label={`Nível ${n}: dose`}
              value={level.levelDescription}
              onChange={(e) => update(i, { levelDescription: e.target.value })}
              placeholder="Dose (ex.: 25 min)"
              maxLength={120}
            />
            <Input
              aria-label={`Nível ${n}: mínimo`}
              value={level.minimumDescription}
              onChange={(e) => update(i, { minimumDescription: e.target.value })}
              placeholder="Mínimo (ex.: 5 min)"
              maxLength={120}
            />
          </div>
        )
      })}
      {error && <p className="text-sm text-danger">{error}</p>}
      {value.length < 10 && (
        <Button
          variant="secondary"
          size="sm"
          className="self-start"
          onClick={() =>
            onChange([...value, { levelDescription: '', minimumDescription: value.at(-1)?.minimumDescription ?? currentMinimum }])
          }
        >
          <Plus className="size-4" aria-hidden />
          Adicionar nível
        </Button>
      )}
    </fieldset>
  )
}

export interface LevelUpRuleDraft {
  /** null = automático (80% de "Feito" em 14 dias). */
  weeks: number | null
  daysPerWeek: number
}

/** Quando o app sugere subir de nível. */
export function LevelUpRuleField({
  value,
  onChange,
  defaultDays,
}: {
  value: LevelUpRuleDraft
  onChange: (value: LevelUpRuleDraft) => void
  /** Dias por semana sugeridos ao trocar para "Semanas seguidas" (pela frequência do hábito). */
  defaultDays: number
}) {
  const mode = value.weeks === null ? 'auto' : 'weeks'
  return (
    <fieldset className="flex flex-col gap-2">
      <legend className="mb-1.5 text-sm font-medium">Quando sugerir subir de nível</legend>
      <Segmented
        label="Quando sugerir subir de nível"
        value={mode}
        onChange={(m) => onChange(m === 'auto' ? { ...value, weeks: null } : { weeks: 3, daysPerWeek: defaultDays })}
        options={[
          { value: 'auto', label: '80% em 14 dias' },
          { value: 'weeks', label: 'Semanas seguidas' },
        ]}
      />
      {value.weeks === null ? (
        <p className="text-xs text-muted">Quando você fizer a dose completa (“Feito”) em 80% dos últimos 14 dias.</p>
      ) : (
        <>
          <div className="grid grid-cols-2 gap-2">
            <Select
              label="Semanas seguidas"
              value={value.weeks}
              onChange={(e) => onChange({ ...value, weeks: Number(e.target.value) })}
            >
              {[1, 2, 3, 4, 5, 6, 7, 8].map((n) => (
                <option key={n} value={n}>
                  {n}
                </option>
              ))}
            </Select>
            <Select
              label="Dias por semana"
              value={value.daysPerWeek}
              onChange={(e) => onChange({ ...value, daysPerWeek: Number(e.target.value) })}
            >
              {[1, 2, 3, 4, 5, 6, 7].map((n) => (
                <option key={n} value={n}>
                  {n} ou mais
                </option>
              ))}
            </Select>
          </div>
          <p className="text-xs text-muted">
            Sugere subir depois de {value.weeks} semana{value.weeks > 1 ? 's' : ''} seguida{value.weeks > 1 ? 's' : ''}{' '}
            com {value.daysPerWeek}+ dia{value.daysPerWeek > 1 ? 's' : ''} cumprido{value.daysPerWeek > 1 ? 's' : ''}.
            Feito e Mínimo contam; a semana vai de segunda a domingo.
          </p>
        </>
      )}
    </fieldset>
  )
}
