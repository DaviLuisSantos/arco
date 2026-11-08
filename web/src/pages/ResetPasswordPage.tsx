import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useSearchParams } from 'react-router'
import { authApi } from '../api/endpoints'
import { ArcoLogo } from '../components/layout/ArcoLogo'
import { Button } from '../components/ui/Button'
import { Input } from '../components/ui/Input'
import { errorMessage } from '../lib/errors'

export function ResetPasswordPage() {
  const { t } = useTranslation('auth')
  const [params] = useSearchParams()
  const token = params.get('token')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [done, setDone] = useState(false)
  const [submitting, setSubmitting] = useState(false)

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    if (!token) return
    if (password.length < 8) {
      setError(t('passwordTooShort'))
      return
    }
    setSubmitting(true)
    setError(null)
    try {
      await authApi.resetPassword(token, password)
      setDone(true)
    } catch (err) {
      setError(errorMessage(err))
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <main className="grid min-h-dvh place-items-center px-4">
      <div className="w-full max-w-sm">
        <div className="mb-8 flex flex-col items-center gap-3 text-center">
          <ArcoLogo className="size-16" />
          <h1 className="text-3xl font-extrabold tracking-tight">{t('reset.title')}</h1>
        </div>
        {!token ? (
          <p className="text-center text-sm text-muted">{t('reset.invalidLink')}</p>
        ) : done ? (
          <p className="text-center text-sm text-muted">{t('reset.done')}</p>
        ) : (
          <form onSubmit={handleSubmit} className="flex flex-col gap-4" noValidate>
            <Input
              label={t('reset.newPassword')}
              type="password"
              autoComplete="new-password"
              autoFocus
              hint={t('minPassword')}
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              error={error}
            />
            <Button type="submit" size="lg" block loading={submitting}>
              {t('reset.submit')}
            </Button>
          </form>
        )}
        <p className="mt-6 text-center text-sm text-muted">
          <Link to="/login" className="hover:text-text">
            {t('reset.toLogin')}
          </Link>
        </p>
      </div>
    </main>
  )
}
