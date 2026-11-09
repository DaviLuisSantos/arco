import { cn } from '../../lib/cn'

/** Bloco de carregamento. Sem animação quando o usuário prefere menos movimento. */
export function Skeleton({ className }: { className?: string }) {
  return <div aria-hidden className={cn('animate-pulse rounded-lg bg-surface-2 motion-reduce:animate-none', className)} />
}
