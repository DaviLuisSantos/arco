import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, Navigate, useNavigate } from 'react-router'
import { authApi } from '../api/endpoints'
import { ArcoLogo } from '../components/layout/ArcoLogo'
import { Button } from '../components/ui/Button'
import { Input } from '../components/ui/Input'
import { Spinner } from '../components/ui/States'
import { currentLocale } from '../i18n'
import { errorMessage } from '../lib/errors'
import { queryKeys } from '../lib/queryKeys'
import { useAuthStatus } from '../lib/useAuthStatus'

export function RegisterPage() {
  const { t } = useTranslation('auth')
  const auth = useAuthStatus()
  const qc = useQueryClient()
  // Mesma consulta do login (cache compartilhado): o botão do Google só aparece se o servidor tiver o provedor.
  const providers = useQuery({
    queryKey: ['auth', 'providers'],
    queryFn: authApi.providers,
    staleTime: Infinity,
    retry: false,
  })
  const navigate = useNavigate()
  const [displayName, setDisplayName] = useState('')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [errors, setErrors] = useState<{ email?: string; password?: string; form?: string }>({})
  const [submitting, setSubmitting] = useState(false)

  if (auth.isPending) {
    return (
      <div className="grid min-h-dvh place-items-center">
        <Spinner />
      </div>
    )
  }
  if (auth.data) return <Navigate to="/" replace />

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    const next: typeof errors = {}
    if (!/^\S+@\S+\.\S+$/.test(email.trim())) next.email = t('register.invalidEmail')
    if (password.length < 8) next.password = t('passwordTooShort')
    setErrors(next)
    if (next.email || next.password) return
    setSubmitting(true)
    try {
      await authApi.register({
        email: email.trim(),
        password,
        displayName: displayName.trim() || undefined,
      })
      qc.clear()
      qc.setQueryData(queryKeys.auth, await authApi.me())
      void navigate('/', { replace: true })
    } catch (err) {
      setErrors({ form: errorMessage(err) })
      setSubmitting(false)
    }
  }

  return (
    <main className="grid min-h-dvh place-items-center px-4">
      <div className="w-full max-w-sm">
        <div className="mb-8 flex flex-col items-center gap-3 text-center">
          <ArcoLogo className="size-16" />
          <h1 className="text-3xl font-extrabold tracking-tight">{t('register.title')}</h1>
          <p className="text-sm text-muted">{t('register.subtitle')}</p>
        </div>
        <form onSubmit={handleSubmit} className="flex flex-col gap-4" noValidate>
          <Input
            label={t('register.name')}
            autoComplete="name"
            value={displayName}
            onChange={(e) => setDisplayName(e.target.value)}
          />
          <Input
            label={t('email')}
            type="email"
            autoComplete="email"
            autoFocus
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            error={errors.email}
          />
          <Input
            label={t('password')}
            type="password"
            autoComplete="new-password"
            hint={t('minPassword')}
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            error={errors.password ?? errors.form}
          />
          <Button type="submit" size="lg" block loading={submitting}>
            {t('register.submit')}
          </Button>
        </form>
        {providers.data?.google && (
          <Button
            type="button"
            variant="secondary"
            size="lg"
            block
            className="mt-3"
            onClick={() => {
              window.location.href = `/api/auth/google?returnUrl=${encodeURIComponent('/')}`
            }}
          >
            {t('register.google')}
          </Button>
        )}
        <p className="mt-4 text-center text-xs text-muted">
          {t('register.privacyPrefix')}{' '}
          {/* Página estática fora do SPA: <a> comum, não <Link>. */}
          <a href={currentLocale() === 'en' ? '/landing/en/privacy/' : '/landing/privacidade/'} className="text-text underline">
            {t('register.privacyLink')}
          </a>
          .
        </p>
        <p className="mt-6 text-center text-sm text-muted">
          {t('register.haveAccount')}{' '}
          <Link to="/login" className="text-text hover:underline">
            {t('register.login')}
          </Link>
        </p>
      </div>
    </main>
  )
}
