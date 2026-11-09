import { useQuery } from '@tanstack/react-query'
import { ArrowLeft } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { settingsApi } from '../api/endpoints'
import type { SettingsInput } from '../api/types'
import { Button } from '../components/ui/Button'
import { Card } from '../components/ui/Card'
import { Select } from '../components/ui/Input'
import { PageHeader } from '../components/ui/PageHeader'
import { Segmented } from '../components/ui/Segmented'
import { ErrorState, Spinner } from '../components/ui/States'
import { AccountCard } from '../features/account/AccountCard'
import { ApiKeysCard } from '../features/apikeys/ApiKeysCard'
import { BackupCard } from '../features/backup/BackupCard'
import { NotificationsCard } from '../features/push/NotificationsCard'
import { SecurityCard } from '../features/security/SecurityCard'
import { LOCALES, setLocale, useLocale, type Locale } from '../i18n'
import { queryKeys } from '../lib/queryKeys'
import { COMMON_TIMEZONES } from '../lib/timezones'
import { getTheme, setTheme, type Theme } from '../lib/theme'
import { useAppMutation } from '../lib/useAppMutation'

export function SettingsPage() {
  const { t } = useTranslation('settings')
  return (
    <div className="flex flex-col gap-5">
      <div>
        <Link to="/profile" className="mb-4 inline-flex min-h-11 items-center gap-1 text-sm text-muted hover:text-text">
          <ArrowLeft className="size-4" aria-hidden />
          {t('back')}
        </Link>
        <PageHeader title={t('title')} subtitle={t('subtitle')} />
      </div>
      <NotificationsSection />
      <PreferencesCard />
      <SecurityCard />
      <BackupCard />
      <ApiKeysCard />
      <AccountCard />
    </div>
  )
}

function NotificationsSection() {
  const { t } = useTranslation('pages', { keyPrefix: 'profile' })
  const settings = useQuery({ queryKey: queryKeys.settings, queryFn: settingsApi.get })
  return (
    <section aria-labelledby="notifications-title">
      <h2 id="notifications-title" className="mb-2 text-sm font-semibold uppercase tracking-wide text-muted">
        {t('notifications')}
      </h2>
      {settings.isPending ? (
        <Spinner />
      ) : settings.isLoadingError ? (
        <ErrorState error={settings.error} onRetry={() => void settings.refetch()} />
      ) : (
        <NotificationsCard settings={settings.data} />
      )}
    </section>
  )
}

function PreferencesCard() {
  const { t } = useTranslation('pages', { keyPrefix: 'profile' })
  const { t: ts } = useTranslation('settings')
  const { locale } = useLocale()
  const { t: tc } = useTranslation('common')
  const settings = useQuery({ queryKey: queryKeys.settings, queryFn: settingsApi.get })
  const [selected, setSelected] = useState<string | null>(null)
  const [theme, setThemeState] = useState<Theme>(getTheme)
  const save = useAppMutation((input: SettingsInput) => settingsApi.update(input), {
    successMessage: t('timezoneSaved'),
    onSuccess: () => setSelected(null),
  })

  const currentTz = settings.data?.timezone ?? ''
  const value = selected ?? currentTz
  const options = COMMON_TIMEZONES.some((tz) => tz.id === currentTz) || !currentTz
    ? COMMON_TIMEZONES
    : [{ id: currentTz, label: currentTz }, ...COMMON_TIMEZONES]

  return (
    <section aria-labelledby="settings-title">
      <h2 id="settings-title" className="mb-2 text-sm font-semibold uppercase tracking-wide text-muted">
        {t('settings')}
      </h2>
      <Card className="flex flex-col gap-3">
        {settings.isPending ? (
          <Spinner />
        ) : settings.isLoadingError ? (
          <ErrorState error={settings.error} onRetry={() => void settings.refetch()} />
        ) : (
          <>
            <Select
              label={t('timezone')}
              value={value}
              onChange={(e) => setSelected(e.target.value)}
              hint={t('timezoneHint', { value })}
            >
              {options.map((tz) => (
                <option key={tz.id} value={tz.id}>
                  {tz.label}
                </option>
              ))}
            </Select>
            <Button
              variant="secondary"
              disabled={value === currentTz}
              loading={save.isPending}
              onClick={() => save.mutate({ timezone: value })}
            >
              {t('saveTimezone')}
            </Button>
            <Select
              label={t('language')}
              value={locale}
              onChange={(e) => void setLocale(e.target.value as Locale)}
            >
              {LOCALES.map((l) => (
                <option key={l} value={l}>
                  {tc(`locales.${l}`)}
                </option>
              ))}
            </Select>
            <div className="flex flex-col gap-1.5">
              <span className="text-sm font-medium">{ts('themeLabel')}</span>
              <Segmented
                label={ts('themeLabel')}
                value={theme}
                onChange={(v) => {
                  setTheme(v)
                  setThemeState(v)
                }}
                options={(['system', 'light', 'dark'] as const).map((v) => ({ value: v, label: ts(`theme.${v}`) }))}
              />
            </div>
          </>
        )}
      </Card>
    </section>
  )
}
