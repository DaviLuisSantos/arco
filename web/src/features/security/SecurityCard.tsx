import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Download, LogOut, ShieldCheck } from 'lucide-react'
import QRCode from 'qrcode'
import { useEffect, useState, type FormEvent } from 'react'
import { authApi } from '../../api/endpoints'
import { useTranslation } from 'react-i18next'
import type { TwoFactorSetup } from '../../api/types'
import { Button } from '../../components/ui/Button'
import { Card } from '../../components/ui/Card'
import { Dialog } from '../../components/ui/Dialog'
import { Input } from '../../components/ui/Input'
import { useToast } from '../../components/ui/toast-context'
import { formatInstant } from '../../lib/dates'
import { errorMessage } from '../../lib/errors'
import { queryKeys } from '../../lib/queryKeys'
import { useAppMutation } from '../../lib/useAppMutation'
import { useAuthStatus } from '../../lib/useAuthStatus'

const SESSIONS_KEY = ['auth', 'sessions'] as const

/** Segurança da conta: 2FA por app autenticador e sessões abertas. */
export function SecurityCard() {
  const { t } = useTranslation('security')
  const me = useAuthStatus().data
  const qc = useQueryClient()
  const toast = useToast()
  const sessions = useQuery({ queryKey: SESSIONS_KEY, queryFn: authApi.sessions, enabled: !!me && !me.isDemo })
  const [setup, setSetup] = useState<TwoFactorSetup | null>(null)
  const [qr, setQr] = useState<string | null>(null)
  const [code, setCode] = useState('')
  const [codeError, setCodeError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [recovery, setRecovery] = useState<string[] | null>(null)
  const [disabling, setDisabling] = useState(false)

  const revoke = useAppMutation((id: number) => authApi.revokeSession(id), {
    successMessage: t('card.revoked'),
    onSuccess: () => void qc.invalidateQueries({ queryKey: SESSIONS_KEY }),
  })
  const revokeOthers = useAppMutation(() => authApi.revokeOtherSessions(), {
    successMessage: t('card.revokedOthers'),
    onSuccess: () => void qc.invalidateQueries({ queryKey: SESSIONS_KEY }),
  })

  useEffect(() => {
    let cancelled = false
    if (!setup) return
    QRCode.toDataURL(setup.otpauthUri, { margin: 1, width: 200 })
      .then((url) => {
        if (!cancelled) setQr(url)
      })
      .catch(() => {
        if (!cancelled) setQr(null)
      })
    return () => {
      cancelled = true
    }
  }, [setup])

  if (!me || me.isDemo) return null
  const enabled = me.twoFactorEnabled

  function closeSetup() {
    setSetup(null)
    setCode('')
    setCodeError(null)
  }

  async function startSetup() {
    setBusy(true)
    try {
      setSetup(await authApi.twoFactorSetup())
    } catch (err) {
      toast.error(errorMessage(err))
    } finally {
      setBusy(false)
    }
  }

  async function confirmEnable(e: FormEvent) {
    e.preventDefault()
    setCodeError(null)
    setBusy(true)
    try {
      const res = await authApi.twoFactorEnable(code.trim())
      closeSetup()
      setRecovery(res.recoveryCodes)
      await qc.invalidateQueries({ queryKey: queryKeys.auth })
      toast.success(t('card.enabledToast'))
    } catch (err) {
      setCodeError(errorMessage(err))
    } finally {
      setBusy(false)
    }
  }

  async function confirmDisable(e: FormEvent) {
    e.preventDefault()
    setCodeError(null)
    setBusy(true)
    try {
      await authApi.twoFactorDisable(code.trim())
      setDisabling(false)
      setCode('')
      await qc.invalidateQueries({ queryKey: queryKeys.auth })
      toast.success(t('card.disabledToast'))
    } catch (err) {
      setCodeError(errorMessage(err))
    } finally {
      setBusy(false)
    }
  }

  async function copyCodes() {
    try {
      await navigator.clipboard.writeText((recovery ?? []).join('\n'))
      toast.success(t('card.copied'))
    } catch {
      /* sem clipboard */
    }
  }

  function downloadCodes() {
    const blob = new Blob([(recovery ?? []).join('\n') + '\n'], { type: 'text/plain' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = 'arco-recovery-codes.txt'
    a.click()
    URL.revokeObjectURL(url)
  }

  const others = (sessions.data ?? []).filter((s) => !s.current)

  return (
    <Card className="flex flex-col gap-4">
      <div className="flex items-start gap-3">
        <ShieldCheck className="mt-0.5 size-5 shrink-0 text-accent" aria-hidden />
        <div className="min-w-0">
          <p className="font-semibold">{t('card.title')}</p>
          <p className="text-sm text-muted">{t('card.subtitle')}</p>
        </div>
      </div>

      <div className="flex flex-col gap-2">
        <p className="text-sm font-medium">{t('card.twoFactor')}</p>
        <p className="text-sm text-muted">{enabled ? t('card.twoFactorOn') : t('card.twoFactorOff')}</p>
        {enabled ? (
          <Button
            variant="secondary"
            onClick={() => {
              setCode('')
              setCodeError(null)
              setDisabling(true)
            }}
          >
            {t('card.disable')}
          </Button>
        ) : (
          <Button variant="secondary" loading={busy && !setup} onClick={() => void startSetup()}>
            {t('card.enable')}
          </Button>
        )}
      </div>

      <div className="flex flex-col gap-2">
        <p className="text-sm font-medium">{t('card.sessions')}</p>
        <p className="text-sm text-muted">{t('card.sessionsDesc')}</p>
        {sessions.data && (
          <ul className="divide-y divide-border rounded-xl border border-border">
            {sessions.data.map((s) => {
              const device = s.userAgent || t('card.unknownDevice')
              return (
                <li key={s.id} className="flex items-center gap-3 px-3 py-2.5">
                  <div className="min-w-0 flex-1">
                    <p className="truncate text-sm font-medium">
                      {device}
                      {s.current && <span className="ml-2 text-xs font-semibold text-accent">{t('card.current')}</span>}
                    </p>
                    <p className="truncate text-xs text-muted">
                      {t('card.lastSeen', { when: formatInstant(s.lastSeenAt) })}
                    </p>
                  </div>
                  {!s.current && (
                    <Button
                      variant="ghost"
                      size="sm"
                      aria-label={t('card.revokeAria', { device })}
                      onClick={() => revoke.mutate(s.id)}
                    >
                      <LogOut className="size-4" aria-hidden />
                    </Button>
                  )}
                </li>
              )
            })}
          </ul>
        )}
        {others.length > 0 && (
          <Button variant="secondary" loading={revokeOthers.isPending} onClick={() => revokeOthers.mutate()}>
            {t('card.revokeOthers')}
          </Button>
        )}
      </div>

      <Dialog open={!!setup} onClose={closeSetup} title={t('card.twoFactor')} description={t('card.scanQr')}>
        <form onSubmit={(e) => void confirmEnable(e)} className="flex flex-col gap-3" noValidate>
          {setup && qr && (
            <img src={qr} alt={t('card.qrAlt')} width={200} height={200} className="self-center rounded-lg bg-white" />
          )}
          {setup && (
            <p className="text-xs text-muted">
              {t('card.manualKey')} <code className="break-all select-all">{setup.secret}</code>
            </p>
          )}
          <Input
            label={t('card.confirmCode')}
            inputMode="numeric"
            autoComplete="one-time-code"
            value={code}
            onChange={(e) => setCode(e.target.value)}
            error={codeError}
          />
          <Button type="submit" loading={busy} disabled={!code.trim()}>
            {t('card.confirmEnable')}
          </Button>
        </form>
      </Dialog>

      <Dialog
        open={disabling}
        onClose={() => setDisabling(false)}
        title={t('card.disableTitle')}
        description={t('card.disableDesc')}
      >
        <form onSubmit={(e) => void confirmDisable(e)} className="flex flex-col gap-3" noValidate>
          <Input
            label={t('card.confirmCode')}
            inputMode="numeric"
            autoComplete="one-time-code"
            value={code}
            onChange={(e) => setCode(e.target.value)}
            error={codeError}
          />
          <Button type="submit" loading={busy} disabled={!code.trim()}>
            {t('card.disable')}
          </Button>
        </form>
      </Dialog>

      <Dialog
        open={!!recovery}
        onClose={() => setRecovery(null)}
        title={t('card.recoveryTitle')}
        description={t('card.recoveryDesc')}
      >
        <div className="flex flex-col gap-3">
          <ul className="grid grid-cols-2 gap-1 rounded-xl border border-border bg-surface-2 p-3 font-mono text-sm">
            {(recovery ?? []).map((c) => (
              <li key={c}>{c}</li>
            ))}
          </ul>
          <div className="flex gap-2">
            <Button variant="secondary" onClick={() => void copyCodes()}>
              {t('card.copy')}
            </Button>
            <Button variant="secondary" onClick={downloadCodes}>
              <Download className="size-4" aria-hidden />
              {t('card.download')}
            </Button>
          </div>
          <Button onClick={() => setRecovery(null)}>{t('card.savedIt')}</Button>
        </div>
      </Dialog>
    </Card>
  )
}
