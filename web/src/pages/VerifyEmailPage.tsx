import { useQueryClient } from '@tanstack/react-query'
import { useEffect, useRef, useState } from 'react'
import { Link, useSearchParams } from 'react-router'
import { authApi } from '../api/endpoints'
import { ArcoLogo } from '../components/layout/ArcoLogo'
import { Spinner } from '../components/ui/States'
import { errorMessage } from '../lib/errors'
import { queryKeys } from '../lib/queryKeys'

type State = { status: 'loading' } | { status: 'ok' } | { status: 'error'; message: string }

export function VerifyEmailPage() {
  const [params] = useSearchParams()
  const token = params.get('token')
  const qc = useQueryClient()
  const started = useRef(false)
  const [state, setState] = useState<State>(
    token ? { status: 'loading' } : { status: 'error', message: 'Link inválido.' },
  )

  useEffect(() => {
    if (!token || started.current) return
    started.current = true
    authApi
      .verifyEmail(token)
      .then(async () => {
        // Com sessão ativa, atualiza o Me (emailVerified); sem sessão, devolve null.
        qc.setQueryData(queryKeys.auth, await authApi.me())
        setState({ status: 'ok' })
      })
      .catch((err: unknown) => setState({ status: 'error', message: errorMessage(err) }))
  }, [token, qc])

  return (
    <main className="grid min-h-dvh place-items-center px-4">
      <div className="flex w-full max-w-sm flex-col items-center gap-4 text-center">
        <ArcoLogo className="size-16" />
        <h1 className="text-2xl font-extrabold tracking-tight">Confirmação de e-mail</h1>
        {state.status === 'loading' && <Spinner />}
        {state.status === 'ok' && <p className="text-sm text-muted">E-mail confirmado. Tudo certo!</p>}
        {state.status === 'error' && <p className="text-sm text-danger">{state.message}</p>}
        <Link to="/" className="text-sm text-muted hover:text-text">
          Continuar para o Arco
        </Link>
      </div>
    </main>
  )
}
