import { useRef, type KeyboardEvent } from 'react'
import { cn } from '../../lib/cn'

interface SegmentedProps<T extends string> {
  value: T
  onChange: (value: T) => void
  options: Array<{ value: T; label: string }>
  label: string
  className?: string
}

/** Grupo de opções exclusivas (abas ou escolha de tipo). Setas movem e selecionam; só a opção ativa entra na ordem de Tab. */
export function Segmented<T extends string>({ value, onChange, options, label, className }: SegmentedProps<T>) {
  const refs = useRef<Array<HTMLButtonElement | null>>([])
  const activeIndex = Math.max(
    0,
    options.findIndex((o) => o.value === value),
  )

  const move = (index: number) => {
    const next = (index + options.length) % options.length
    onChange(options[next].value)
    refs.current[next]?.focus()
  }

  const onKeyDown = (e: KeyboardEvent<HTMLButtonElement>, index: number) => {
    switch (e.key) {
      case 'ArrowRight':
      case 'ArrowDown':
        e.preventDefault()
        move(index + 1)
        break
      case 'ArrowLeft':
      case 'ArrowUp':
        e.preventDefault()
        move(index - 1)
        break
      case 'Home':
        e.preventDefault()
        move(0)
        break
      case 'End':
        e.preventDefault()
        move(options.length - 1)
        break
    }
  }

  return (
    <div role="radiogroup" aria-label={label} className={cn('flex rounded-xl bg-surface-2 p-1', className)}>
      {options.map((o, i) => (
        <button
          key={o.value}
          ref={(el) => {
            refs.current[i] = el
          }}
          type="button"
          role="radio"
          aria-checked={value === o.value}
          tabIndex={i === activeIndex ? 0 : -1}
          onClick={() => onChange(o.value)}
          onKeyDown={(e) => onKeyDown(e, i)}
          className={cn(
            'min-h-11 flex-1 rounded-lg px-2 py-2 text-sm font-medium transition',
            value === o.value ? 'bg-surface text-text shadow' : 'text-muted hover:text-text',
          )}
        >
          {o.label}
        </button>
      ))}
    </div>
  )
}
