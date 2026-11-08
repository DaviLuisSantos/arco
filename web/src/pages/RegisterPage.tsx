import { useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { Link, Navigate, useNavigate } from 'react-router'
import { authApi } from '../api/endpoints'
import { ArcoLogo } from '../components/layout/ArcoLogo'
import { Button } from '../components/ui/Button'
import { Input } from '../components/ui/Input'
import { Spinner } from '../components/ui/States'
import { errorMessage } from '../lib/errors'
import { queryKeys } from '../lib/queryKeys'
import { useAuthStatus } from '../lib/useAuthStatus'

export function RegisterPage() {
  const auth = useAuthStatus()
  const qc = useQueryClient()
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
    if (!/^\S+@\S+\.\S+$/.test(email.trim())) next.email = 'Digite um e-mail válido.'
    if (password.length < 8) next.password = 'A senha precisa ter ao menos 8 caracteres.'
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
          <h1 className="text-3xl font-extrabold tracking-tight">Criar conta</h1>
          <p className="text-sm text-muted">Comece seu primeiro ciclo no Arco.</p>
        </div>
        <form onSubmit={handleSubmit} className="flex flex-col gap-4" noValidate>
          <Input
            label="Nome (opcional)"
            autoComplete="name"
            value={displayName}
            onChange={(e) => setDisplayName(e.target.value)}
          />
          <Input
            label="E-mail"
            type="email"
            autoComplete="email"
            autoFocus
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            error={errors.email}
          />
          <Input
            label="Senha"
            type="password"
            autoComplete="new-password"
            hint="Mínimo de 8 caracteres."
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            error={errors.password ?? errors.form}
          />
          <Button type="submit" size="lg" block loading={submitting}>
            Criar conta
          </Button>
        </form>
        <p className="mt-6 text-center text-sm text-muted">
          Já tem conta?{' '}
          <Link to="/login" className="text-text hover:underline">
            Entrar
          </Link>
        </p>
      </div>
    </main>
  )
}
