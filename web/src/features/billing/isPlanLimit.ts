import { ApiError } from '../../api/client'

/** Verdadeiro quando o erro é 403 `plan_limit` (limite do plano Free). */
export function isPlanLimit(err: unknown): boolean {
  return err instanceof ApiError && err.code === 'plan_limit'
}
