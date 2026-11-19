import { Plus, X } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Button } from '../../components/ui/Button'
import { Input, Select } from '../../components/ui/Input'
import { Segmented } from '../../components/ui/Segmented'
import { formatQuantity, parseQuantity } from '../../lib/quantity'

export interface PlannedLevelDraft {
  levelDescription: string
  minimumDescription: string
  /** Texto digitado (aceita vírgula); só em hábito quantitativo. */
  target: string
  minimumTarget: string
}

/** Próximos níveis do hábito (a escada). Ao subir de nível, o primeiro da lista já vem preenchido. */
export function PlannedLevelsField({
  currentLevel,
  currentMinimum,
  currentMinimumTarget = '',
  unit,
  value,
  onChange,
  error,
}: {
  currentLevel: number
  /** Mínimo sugerido para o primeiro nível novo (os seguintes repetem o anterior). */
  currentMinimum: string
  /** Mínimo numérico sugerido para o primeiro nível novo (hábito quantitativo). */
  currentMinimumTarget?: string
  /** Presente = hábito quantitativo: cada nível pede meta e mínimo numéricos. */
  unit?: string
  value: PlannedLevelDraft[]
  onChange: (value: PlannedLevelDraft[]) => void
  error?: string
}) {
  const { t } = useTranslation('habits')
  function update(index: number, patch: Partial<PlannedLevelDraft>) {
    onChange(value.map((l, i) => (i === index ? { ...l, ...patch } : l)))
  }

  return (
    <fieldset className="flex flex-col gap-2">
      <legend className="mb-1 text-sm font-medium">{t('ladder.title')}</legend>
      <p className="-mt-1 text-xs text-muted">{t('ladder.hint')}</p>
      {value.map((level, i) => {
        const n = currentLevel + i + 1
        return (
          <div key={i} className="flex flex-col gap-2 rounded-xl border border-border p-3">
            <div className="flex items-center justify-between">
              <span className="text-sm font-semibold">{t('ladder.level', { level: n })}</span>
              <Button
                variant="ghost"
                size="sm"
                aria-label={t('ladder.removeLevel', { level: n })}
                onClick={() => onChange(value.filter((_, j) => j !== i))}
              >
                <X className="size-4" aria-hidden />
              </Button>
            </div>
            {unit !== undefined && (
              <div className="grid grid-cols-2 gap-2">
                <Input
                  aria-label={t('ladder.target', { level: n, unit })}
                  inputMode="decimal"
                  value={level.target}
                  onChange={(e) => update(i, { target: e.target.value })}
                  placeholder={t('ladder.targetPlaceholder')}
                  maxLength={9}
                />
                <Input
                  aria-label={t('ladder.minimumTarget', { level: n, unit })}
                  inputMode="decimal"
                  value={level.minimumTarget}
                  onChange={(e) => update(i, { minimumTarget: e.target.value })}
                  placeholder={t('ladder.minimumTargetPlaceholder')}
                  maxLength={9}
                />
              </div>
            )}
            <Input
              aria-label={t('ladder.dose', { level: n })}
              value={level.levelDescription}
              onChange={(e) => update(i, { levelDescription: e.target.value })}
              placeholder={
                unit !== undefined && parseQuantity(level.target) !== null
                  ? t('ladder.autoPlaceholder', { value: `${formatQuantity(parseQuantity(level.target))} ${unit}` })
                  : t('ladder.dosePlaceholder')
              }
              maxLength={120}
            />
            <Input
              aria-label={t('ladder.minimum', { level: n })}
              value={level.minimumDescription}
              onChange={(e) => update(i, { minimumDescription: e.target.value })}
              placeholder={
                unit !== undefined && parseQuantity(level.minimumTarget) !== null
                  ? t('ladder.autoPlaceholder', {
                      value: `${formatQuantity(parseQuantity(level.minimumTarget))} ${unit}`,
                    })
                  : t('ladder.minimumPlaceholder')
              }
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
            onChange([
              ...value,
              {
                levelDescription: '',
                minimumDescription: unit !== undefined ? '' : (value.at(-1)?.minimumDescription ?? currentMinimum),
                target: '',
                minimumTarget: value.at(-1)?.minimumTarget ?? currentMinimumTarget,
              },
            ])
          }
        >
          <Plus className="size-4" aria-hidden />
          {t('ladder.add')}
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
  const { t } = useTranslation('habits')
  const mode = value.weeks === null ? 'auto' : 'weeks'
  return (
    <fieldset className="flex flex-col gap-2">
      <legend className="mb-1.5 text-sm font-medium">{t('rule.title')}</legend>
      <Segmented
        label={t('rule.title')}
        value={mode}
        onChange={(m) => onChange(m === 'auto' ? { ...value, weeks: null } : { weeks: 3, daysPerWeek: defaultDays })}
        options={[
          { value: 'auto', label: t('rule.auto') },
          { value: 'weeks', label: t('rule.weeks') },
        ]}
      />
      {value.weeks === null ? (
        <p className="text-xs text-muted">{t('rule.autoHint')}</p>
      ) : (
        <>
          <div className="grid grid-cols-2 gap-2">
            <Select
              label={t('rule.weeks')}
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
              label={t('rule.daysPerWeek')}
              value={value.daysPerWeek}
              onChange={(e) => onChange({ ...value, daysPerWeek: Number(e.target.value) })}
            >
              {[1, 2, 3, 4, 5, 6, 7].map((n) => (
                <option key={n} value={n}>
                  {t('rule.orMore', { count: n })}
                </option>
              ))}
            </Select>
          </div>
          <p className="text-xs text-muted">
            {t('rule.weeksHint', {
              weeks: value.weeks,
              weeksWord: t('rule.weekWord', { count: value.weeks }),
              days: value.daysPerWeek,
              daysWord: t('rule.dayWord', { count: value.daysPerWeek }),
            })}
          </p>
        </>
      )}
    </fieldset>
  )
}
