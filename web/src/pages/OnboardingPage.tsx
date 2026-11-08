import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate, useLocation } from 'react-router'
import { authApi, billingApi, cyclesApi, habitsApi, settingsApi } from '../api/endpoints'
import { Button } from '../components/ui/Button'
import { Card } from '../components/ui/Card'
import { Input, Select } from '../components/ui/Input'
import { useToast } from '../components/ui/toast-context'
import { HABIT_TEMPLATES, type TemplateId } from '../features/onboarding/templates'
import { LOCALES, currentLocale, setLocale, type Locale } from '../i18n'
import { cn } from '../lib/cn'
import { errorMessage } from '../lib/errors'
import { queryKeys } from '../lib/queryKeys'
import { COMMON_TIMEZONES } from '../lib/timezones'

const TOTAL = 4

export function OnboardingPage() {
  const { t } = useTranslation('onboarding')
  const { t: tc } = useTranslation('common')
  const navigate = useNavigate()
  const location = useLocation()
  // Volta para onde a pessoa ia (ex.: resumo de um parceiro) depois dos primeiros passos.
  const from = (location.state as { from?: string } | null)?.from
  const returnTo = from && from.startsWith('/') && !from.startsWith('//') && from !== '/welcome' ? from : '/'
  const qc = useQueryClient()
  const toast = useToast()
  const [step, setStep] = useState(1)
  const [busy, setBusy] = useState(false)

  const [locale, setLoc] = useState<Locale>(currentLocale())
  const [timezone, setTimezone] = useState(
    () => Intl.DateTimeFormat().resolvedOptions().timeZone || 'America/Sao_Paulo',
  )
  const [picked, setPicked] = useState<TemplateId[]>([])
  const [customName, setCustomName] = useState('')
  const [customDose, setCustomDose] = useState('')
  const [customMin, setCustomMin] = useState('')
  const [cycleName, setCycleName] = useState(() => t('cycle.defaultName'))
  const [weeks, setWeeks] = useState(6)
  const [hours, setHours] = useState(5)

  const billing = useQuery({ queryKey: ['billing'], queryFn: billingApi.get })
  const room =
    billing.data?.limits.habits == null ? null : Math.max(0, billing.data.limits.habits - billing.data.usage.habits)
  const customReady = Boolean(customName.trim() && customDose.trim() && customMin.trim())
  const total = picked.length + (customReady ? 1 : 0)
  const overLimit = room !== null && total > room

  const tzOptions = COMMON_TIMEZONES.some((z) => z.id === timezone)
    ? COMMON_TIMEZONES
    : [{ id: timezone, label: timezone }, ...COMMON_TIMEZONES]

  async function run(fn: () => Promise<void>, then?: () => void) {
    setBusy(true)
    try {
      await fn()
      then?.()
    } catch (e) {
      toast.error(errorMessage(e))
    } finally {
      setBusy(false)
    }
  }

  async function finish() {
    await run(
      async () => {
        await authApi.completeOnboarding()
        await qc.invalidateQueries({ queryKey: queryKeys.auth })
        await qc.invalidateQueries({ predicate: (q) => q.queryKey[0] !== 'auth' })
      },
      () => navigate(returnTo, { replace: true }),
    )
  }

  const next = () => setStep((s) => Math.min(TOTAL, s + 1))
  const toggle = (id: TemplateId) =>
    setPicked((p) => (p.includes(id) ? p.filter((x) => x !== id) : p.length >= 3 ? p : [...p, id]))

  function submit() {
    if (step === 1) {
      void run(async () => {
        await settingsApi.update({ timezone })
        await setLocale(locale)
      }, next)
    } else if (step === 2) {
      if (total === 0) return next()
      void run(async () => {
        for (const id of picked) {
          await habitsApi.create({
            name: t(`templates.${id}.name`),
            emoji: HABIT_TEMPLATES.find((x) => x.id === id)?.emoji,
            frequencyType: 'Daily',
            levelDescription: t(`templates.${id}.dose`),
            minimumDescription: t(`templates.${id}.minimum`),
          })
        }
        if (customReady) {
          await habitsApi.create({
            name: customName.trim(),
            frequencyType: 'Daily',
            levelDescription: customDose.trim(),
            minimumDescription: customMin.trim(),
          })
        }
        setPicked([])
        setCustomName('')
        setCustomDose('')
        setCustomMin('')
        toast.success(t('habits.created', { count: total }))
      }, next)
    } else if (step === 3) {
      void run(async () => {
        await cyclesApi.create({
          name: cycleName.trim() || t('cycle.defaultName'),
          weeks,
          weeklyFocusMinutes: Math.round(hours * 60),
        })
      }, next)
    } else {
      void finish()
    }
  }

  return (
    <div className="mx-auto flex min-h-dvh max-w-xl flex-col gap-4 px-4 py-6">
      <header className="flex items-center justify-between gap-3">
        <div>
          <h1 className="text-xl font-bold">{t('title')}</h1>
          <p className="text-sm text-muted">{t('step', { current: step, total: TOTAL })}</p>
        </div>
        <Button
          variant="ghost"
          size="sm"
          data-testid="onboarding-skip"
          disabled={busy}
          onClick={() => void finish()}
        >
          {t('skip')}
        </Button>
      </header>

      <Card className="flex flex-col gap-4">
        {step === 1 && (
          <>
            <h2 className="text-lg font-bold">{t('language.title')}</h2>
            <p className="text-sm text-muted">{t('language.hint')}</p>
            <Select label={tc('language')} value={locale} onChange={(e) => setLoc(e.target.value as Locale)}>
              {LOCALES.map((l) => (
                <option key={l} value={l}>
                  {tc(`locales.${l}`)}
                </option>
              ))}
            </Select>
            <Select label={t('language.timezone')} value={timezone} onChange={(e) => setTimezone(e.target.value)}>
              {tzOptions.map((z) => (
                <option key={z.id} value={z.id}>
                  {z.label}
                </option>
              ))}
            </Select>
          </>
        )}

        {step === 2 && (
          <>
            <h2 className="text-lg font-bold">{t('habits.title')}</h2>
            <p className="text-sm text-muted">{t('habits.hint')}</p>
            <div className="grid gap-2 sm:grid-cols-2">
              {HABIT_TEMPLATES.map((h) => {
                const on = picked.includes(h.id)
                return (
                  <button
                    key={h.id}
                    type="button"
                    aria-pressed={on}
                    onClick={() => toggle(h.id)}
                    className={cn(
                      'rounded-xl border p-3 text-left transition',
                      on ? 'border-accent bg-surface-2' : 'border-border hover:border-accent',
                    )}
                  >
                    <div className="font-semibold">
                      {h.emoji} {t(`templates.${h.id}.name`)}
                    </div>
                    <div className="text-xs text-muted">
                      {t('habits.dose')}: {t(`templates.${h.id}.dose`)}
                    </div>
                    <div className="text-xs text-muted">
                      {t('habits.minimum')}: {t(`templates.${h.id}.minimum`)}
                    </div>
                  </button>
                )
              })}
            </div>
            <details className="rounded-xl border border-border p-3">
              <summary className="cursor-pointer text-sm font-medium">{t('habits.custom')}</summary>
              <div className="mt-3 flex flex-col gap-3">
                <Input
                  label={t('habits.customName')}
                  value={customName}
                  onChange={(e) => setCustomName(e.target.value)}
                  maxLength={80}
                />
                <Input label={t('habits.customDose')} value={customDose} onChange={(e) => setCustomDose(e.target.value)} />
                <Input label={t('habits.customMinimum')} value={customMin} onChange={(e) => setCustomMin(e.target.value)} />
              </div>
            </details>
            <p className={cn('text-sm', overLimit ? 'text-danger' : 'text-muted')}>
              {t('habits.selected', { count: total })}
              {room !== null && ` · ${t('habits.limit', { count: room })}`}
            </p>
          </>
        )}

        {step === 3 && (
          <>
            <h2 className="text-lg font-bold">{t('cycle.title')}</h2>
            <p className="text-sm text-muted">{t('cycle.hint')}</p>
            <Input label={t('cycle.name')} value={cycleName} onChange={(e) => setCycleName(e.target.value)} maxLength={80} />
            <Select label={t('cycle.weeks')} value={weeks} onChange={(e) => setWeeks(Number(e.target.value))}>
              {[4, 6, 8, 10, 12].map((w) => (
                <option key={w} value={w}>
                  {w}
                </option>
              ))}
            </Select>
            <Input
              label={t('cycle.focusHours')}
              type="number"
              min={1}
              max={60}
              value={hours}
              onChange={(e) => setHours(Math.max(1, Number(e.target.value) || 1))}
            />
          </>
        )}

        {step === 4 && (
          <>
            <h2 className="text-lg font-bold">{t('done.title')}</h2>
            <p className="text-sm text-muted">{t('done.hint')}</p>
          </>
        )}

        <div className="flex justify-between gap-2">
          <Button variant="secondary" disabled={busy || step === 1} onClick={() => setStep((s) => Math.max(1, s - 1))}>
            {t('back')}
          </Button>
          <Button loading={busy} disabled={step === 2 && overLimit} onClick={submit}>
            {step === TOTAL ? t('done.go') : t('next')}
          </Button>
        </div>
      </Card>
    </div>
  )
}
