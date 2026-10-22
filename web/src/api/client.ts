import type { ApiErrorBody } from './types'

export class ApiError extends Error {
  readonly status: number
  readonly code: string

  constructor(status: number, code: string, message: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.code = code
  }
}

/** Rotas cujo 401 é resposta esperada e não deve redirecionar para o login. */
const UNAUTHORIZED_EXEMPT = ['/auth/me', '/auth/login', '/auth/logout']

let onUnauthorized: (() => void) | null = null

export function setUnauthorizedHandler(handler: () => void) {
  onUnauthorized = handler
}

type Method = 'GET' | 'POST' | 'PUT' | 'DELETE'

const FALLBACK_CODES: Record<number, string> = {
  400: 'validation',
  401: 'unauthorized',
  404: 'not_found',
  409: 'conflict',
  429: 'rate_limited',
  502: 'unavailable',
  503: 'unavailable',
  504: 'unavailable',
}

async function readError(res: Response): Promise<ApiError> {
  let body: Partial<ApiErrorBody> = {}
  try {
    body = (await res.json()) as Partial<ApiErrorBody>
  } catch {
    // corpo vazio ou não-JSON
  }
  const code = body.code ?? FALLBACK_CODES[res.status] ?? 'internal_error'
  return new ApiError(res.status, code, body.error ?? res.statusText)
}

export async function request<T>(method: Method, path: string, body?: unknown): Promise<T> {
  let res: Response
  try {
    res = await fetch(`/api${path}`, {
      method,
      credentials: 'same-origin',
      headers: {
        Accept: 'application/json',
        ...(body !== undefined ? { 'Content-Type': 'application/json' } : {}),
      },
      body: body !== undefined ? JSON.stringify(body) : undefined,
    })
  } catch {
    throw new ApiError(0, 'network', 'Falha de rede')
  }

  if (!res.ok) {
    const error = await readError(res)
    if (res.status === 401 && !UNAUTHORIZED_EXEMPT.some((p) => path.startsWith(p))) {
      onUnauthorized?.()
    }
    throw error
  }

  if (res.status === 204) return undefined as T
  const text = await res.text()
  return (text ? JSON.parse(text) : undefined) as T
}

export const api = {
  get: <T>(path: string) => request<T>('GET', path),
  post: <T>(path: string, body?: unknown) => request<T>('POST', path, body ?? {}),
  put: <T>(path: string, body: unknown) => request<T>('PUT', path, body),
  delete: <T>(path: string) => request<T>('DELETE', path),
}
