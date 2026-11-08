import { useState, type FormEvent } from 'react'
import { Link } from 'react-router'
import { authApi } from '../api/endpoints'
import { ArcoLogo } from '../components/layout/ArcoLogo'
import { Button } from '../components/ui/Button'
import { Input } from '../components/ui/Input'
import { errorMessage } from '../lib/errors'

export function ForgotPasswordPage() {
  const [email, setEmail] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [sent, setSent] = useState(false)
  const [submitting, setSubmitting] = useState(false)

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    if (!email.trim()) {
      setError('Digite o e-mail.')
      return
    }
    setSubmitting(true)
    setError(null)
    try {
      await authApi.forgotPassword(email.trim())
      setSent(true)
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
          <h1 className="text-3xl font-extrabold tracking-tight">Esqueci a senha</h1>
        </div>
        {sent ? (
          <p className="text-center text-sm text-muted">
            Se o e-mail estiver cadastrado e verificado, enviamos um link para redefinir a senha. Ele vale por 1 hora.
          </p>
        ) : (
          <form onSubmit={handleSubmit} className="flex flex-col gap-4" noValidate>
            <Input
              label="E-mail"
              type="email"
              autoComplete="email"
              autoFocus
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              error={error}
            />
            <Button type="submit" size="lg" block loading={submitting}>
              Enviar link
            </Button>
          </form>
        )}
        <p className="mt-6 text-center text-sm text-muted">
          <Link to="/login" className="hover:text-text">
            Voltar para o login
          </Link>
        </p>
      </div>
    </main>
  )
}
