import { LoaderCircle } from 'lucide-react'
import type { ButtonHTMLAttributes } from 'react'
import { cn } from '../../lib/cn'

type Variant = 'primary' | 'secondary' | 'ghost' | 'danger'
type Size = 'sm' | 'md' | 'lg'

const VARIANTS: Record<Variant, string> = {
  primary: 'bg-accent-strong text-on-accent hover:brightness-110',
  secondary: 'bg-surface-2 text-text border border-border hover:border-accent',
  ghost: 'text-muted hover:text-text hover:bg-surface-2',
  danger: 'bg-danger/15 text-danger border border-danger/40 hover:bg-danger/25',
}

const SIZES: Record<Size, string> = {
  // h-9 visual; a área de toque chega a 46px pelo pseudo-elemento
  sm: "relative h-9 px-3 text-sm gap-1.5 before:absolute before:inset-x-0 before:-inset-y-[5px] before:content-['']",
  md: 'h-11 px-4 text-sm gap-2',
  lg: 'h-12 px-5 text-base gap-2',
}

export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: Variant
  size?: Size
  loading?: boolean
  block?: boolean
}

export function Button({
  variant = 'primary',
  size = 'md',
  loading = false,
  block = false,
  className,
  disabled,
  children,
  type = 'button',
  ...rest
}: ButtonProps) {
  return (
    <button
      type={type}
      disabled={disabled || loading}
      className={cn(
        'inline-flex items-center justify-center rounded-xl font-semibold transition select-none',
        'disabled:opacity-50 disabled:pointer-events-none active:scale-[0.98]',
        VARIANTS[variant],
        SIZES[size],
        block && 'w-full',
        className,
      )}
      {...rest}
    >
      {loading && <LoaderCircle className="size-4 animate-spin" aria-hidden />}
      {children}
    </button>
  )
}
