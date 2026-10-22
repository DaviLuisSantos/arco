import { cn } from '../../lib/cn'

interface ProgressBarProps {
  /** 0..1 */
  value: number
  label?: string
  tone?: 'accent' | 'xp' | 'done'
  size?: 'sm' | 'md'
  className?: string
}

const TONES = {
  accent: 'bg-accent',
  xp: 'bg-gradient-to-r from-accent-strong to-xp',
  done: 'bg-done',
}

export function ProgressBar({ value, label, tone = 'accent', size = 'md', className }: ProgressBarProps) {
  const pct = Math.round(Math.min(1, Math.max(0, value)) * 100)
  return (
    <div
      role="progressbar"
      aria-valuemin={0}
      aria-valuemax={100}
      aria-valuenow={pct}
      aria-label={label}
      className={cn('w-full overflow-hidden rounded-full bg-surface-2', size === 'sm' ? 'h-1.5' : 'h-2.5', className)}
    >
      <div className={cn('h-full rounded-full transition-[width] duration-500', TONES[tone])} style={{ width: `${pct}%` }} />
    </div>
  )
}
