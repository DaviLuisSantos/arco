import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useRef, useState, type FormEvent } from 'react'
import { Link, Navigate, useLocation, useNavigate, useSearchParams } from 'react-router'
import { authApi } from '../api/endpoints'
import { ArcoLogo } from '../components/layout/ArcoLogo'
import { Button } from '../components/ui/Button'
import { Input } from '../components/ui/Input'
import { Spinner } from '../components/ui/States'
import { errorMessage } from '../lib/errors'
import { queryKeys } from '../lib/queryKeys'
import { useAuthStatus } from '../lib/useAuthStatus'

export function LoginPage() {
  const auth = useAuthStatus()
  const qc = useQueryClient()
  const navigate = useNavigate()
  const location = useLocation()
  const [params] = useSearchParams()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [emailError, setEmailError] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
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
      setEmailError('Digite o e-mail.')
      return
    }
    if (!password) {
      setError('Digite a senha.')
      return
    }
    setSubmitting(true)
    try {
      await authApi.login(email.trim(), password)
      qc.clear()
      qc.setQueryData(queryKeys.auth, await authApi.me())
      void navigate(from, { replace: true })
    } catch (err) {
      setError(errorMessage(err))
      setSubmitting(false)
    }
  }

  return (
    <main className="grid min-h-dvh place-items-center px-4">
      <div className="w-full max-w-sm">
        <div className="mb-8 flex flex-col items-center gap-3 text-center">
          <ArcoLogo className="size-16" />
          <h1 className="text-3xl font-extrabold tracking-tight">Arco</h1>
          <p className="text-sm text-muted">Hábitos com progresso e objetivo.</p>
        </div>
        <form onSubmit={handleSubmit} className="flex flex-col gap-4" noValidate>
          <Input
            label="E-mail"
            type="email"
            autoComplete="email"
            autoFocus
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            error={emailError}
          />
          <Input
            label="Senha"
            type="password"
            autoComplete="current-password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            error={error}
          />
          <Button type="submit" size="lg" block loading={submitting} disabled={demoing}>
            Entrar
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
              Continuar com Google
            </Button>
          )}
          <Button type="button" variant="ghost" block loading={demoing} onClick={() => void startDemo()}>
            Experimentar demo
          </Button>
        </div>
        <div className="mt-6 flex flex-col items-center gap-2 text-sm text-muted">
          <Link to="/forgot-password" className="hover:text-text">
            Esqueci a senha
          </Link>
          <Link to="/register" className="hover:text-text">
            Não tem conta? Criar conta
          </Link>
        </div>
      </div>
    </main>
  )
}
