import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useRef, useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, Navigate, useLocation, useNavigate, useSearchParams } from 'react-router'
import { authApi } from '../api/endpoints'
import { ArcoLogo } from '../components/layout/ArcoLogo'
import { Button } from '../components/ui/Button'
import { Input } from '../components/ui/Input'
import { Spinner } from '../components/ui/States'
import { normalizeLocale, setLocale } from '../i18n'
import { errorMessage } from '../lib/errors'
import { queryKeys } from '../lib/queryKeys'
import { useAuthStatus } from '../lib/useAuthStatus'

export function LoginPage() {
  const auth = useAuthStatus()
  const qc = useQueryClient()
  const navigate = useNavigate()
  const location = useLocation()
  const { t } = useTranslation('security')
  const [params] = useSearchParams()
  const [ticket, setTicket] = useState<string | null>(() => params.get('twofactor'))
  const [code, setCode] = useState('')
  const [useRecovery, setUseRecovery] = useState(false)
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [emailError, setEmailError] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(() =>
    params.get('error') === 'google'
      ? `${t('login.googleError')}${params.get('reason') ? t('login.googleErrorCode', { code: params.get('reason') }) : ''}`
      : null,
  )
  const [submitting, setSubmitting] = useState(false)
  const [demoing, setDemoing] = useState(false)
  const demoStarted = useRef(false)

  const from = (location.state as { from?: string } | null)?.from ?? '/'
  const providers = useQuery({
    queryKey: ['auth', 'providers'],
    queryFn: authApi.providers,
    staleTime: Infinity,
    retry: false,
  })
  const wantsDemo = params.get('demo') === '1'
  const wantsRegister = params.get('mode') === 'register'
  const loggedIn = !!auth.data
  const langParam = normalizeLocale(params.get('lang'))

  useEffect(() => {
    if (langParam) void setLocale(langParam, false)
  }, [langParam])

  async function finishLogin() {
    qc.clear()
    qc.setQueryData(queryKeys.auth, await authApi.me())
    void navigate(from, { replace: true })
  }

  async function handleCode(e: FormEvent) {
    e.preventDefault()
    setError(null)
    if (!code.trim()) {
      setError(t('twoFactor.typeCode'))
      return
    }
    setSubmitting(true)
    try {
      await authApi.login2fa(ticket!, code.trim())
      await finishLogin()
    } catch (err) {
      setError(errorMessage(err))
      setSubmitting(false)
    }
  }

  async function startDemo() {
    setDemoing(true)
    setError(null)
    try {
      await authApi.demo()
      qc.clear()
      qc.setQueryData(queryKeys.auth, await authApi.me())
      void navigate('/', { replace: true })
    } catch (err) {
      setError(errorMessage(err))
      setDemoing(false)
    }
  }

  useEffect(() => {
    if (wantsDemo && !wantsRegister && !auth.isPending && !loggedIn && !demoStarted.current) {
      demoStarted.current = true
      void startDemo()
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [wantsDemo, wantsRegister, auth.isPending, loggedIn])

  if (auth.isPending) {
    return (
      <div className="grid min-h-dvh place-items-center">
        <Spinner />
      </div>
    )
  }
  if (auth.data) return <Navigate to={from} replace />
  if (wantsRegister) return <Navigate to="/register" replace />

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    setEmailError(null)
    setError(null)
    if (!email.trim()) {
      setEmailError(t('login.typeEmail'))
      return
    }
    if (!password) {
      setError(t('login.typePassword'))
      return
    }
    setSubmitting(true)
    try {
      const res = await authApi.login(email.trim(), password)
      if (res && res.twoFactorRequired) {
        setTicket(res.ticket)
        setSubmitting(false)
        return
      }
      await finishLogin()
    } catch (err) {
      setError(errorMessage(err))
      setSubmitting(false)
    }
  }

  if (ticket) {
    return (
      <main className="grid min-h-dvh place-items-center px-4">
        <div className="w-full max-w-sm">
          <div className="mb-8 flex flex-col items-center gap-3 text-center">
            <ArcoLogo className="size-16" />
            <h1 className="text-2xl font-extrabold tracking-tight">{t('twoFactor.title')}</h1>
            <p className="text-sm text-muted">{useRecovery ? t('twoFactor.recoveryHint') : t('twoFactor.codeHint')}</p>
          </div>
          <form onSubmit={(e) => void handleCode(e)} className="flex flex-col gap-4" noValidate>
            <Input
              label={useRecovery ? t('twoFactor.recoveryLabel') : t('twoFactor.codeLabel')}
              inputMode={useRecovery ? 'text' : 'numeric'}
              autoComplete="one-time-code"
              autoFocus
              value={code}
              onChange={(e) => setCode(e.target.value)}
              error={error}
            />
            <Button type="submit" size="lg" block loading={submitting}>
              {t('twoFactor.verify')}
            </Button>
          </form>
          <div className="mt-6 flex flex-col items-center gap-2 text-sm text-muted">
            <button
              type="button"
              className="hover:text-text"
              onClick={() => {
                setUseRecovery((v) => !v)
                setCode('')
                setError(null)
              }}
            >
              {useRecovery ? t('twoFactor.useApp') : t('twoFactor.useRecovery')}
            </button>
            <button
              type="button"
              className="hover:text-text"
              onClick={() => {
                setTicket(null)
                setCode('')
                setError(null)
                setUseRecovery(false)
              }}
            >
              {t('twoFactor.back')}
            </button>
          </div>
        </div>
      </main>
    )
  }

  return (
    <main className="grid min-h-dvh place-items-center px-4">
      <div className="w-full max-w-sm">
        <div className="mb-8 flex flex-col items-center gap-3 text-center">
          <ArcoLogo className="size-16" />
          <h1 className="text-3xl font-extrabold tracking-tight">Arco</h1>
          <p className="text-sm text-muted">{t('login.tagline')}</p>
        </div>
        <form onSubmit={handleSubmit} className="flex flex-col gap-4" noValidate>
          <Input
            label={t('login.email')}
            type="email"
            autoComplete="email"
            autoFocus
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            error={emailError}
          />
          <Input
            label={t('login.password')}
            type="password"
            autoComplete="current-password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            error={error}
          />
          <Button type="submit" size="lg" block loading={submitting} disabled={demoing}>
            {t('login.submit')}
          </Button>
        </form>
        <div className="mt-4 flex flex-col gap-3">
          {providers.data?.google && (
            <Button
              type="button"
              variant="secondary"
              size="lg"
              block
              onClick={() => {
                window.location.href = `/api/auth/google?returnUrl=${encodeURIComponent(from)}`
              }}
            >
              {t('login.google')}
            </Button>
          )}
          <Button type="button" variant="ghost" block loading={demoing} onClick={() => void startDemo()}>
            {t('login.demo')}
          </Button>
        </div>
        <div className="mt-6 flex flex-col items-center gap-2 text-sm text-muted">
          <Link to="/forgot-password" className="hover:text-text">
            {t('login.forgot')}
          </Link>
          <Link to="/register" className="hover:text-text">
            {t('login.register')}
          </Link>
        </div>
      </div>
    </main>
  )
}
