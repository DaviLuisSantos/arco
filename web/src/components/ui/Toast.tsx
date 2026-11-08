import { CircleAlert, CircleCheck, Info, Sparkles, Trophy } from 'lucide-react'
import { useCallback, useLayoutEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import i18n from 'i18next'
import { cn } from '../../lib/cn'
import { formatXp } from '../../lib/format'
import { ToastContext, type ToastApi, type ToastOptions, type ToastVariant } from './toast-context'

interface ToastItem {
  id: number
  message: string
  variant: ToastVariant
}

const DURATION: Record<ToastVariant, number> = { info: 3500, success: 3000, error: 5000, xp: 1600, achievement: 5000 }

const ICONS = { info: Info, success: CircleCheck, error: CircleAlert, xp: Sparkles, achievement: Trophy }

let nextId = 1

// Os toasts vivem na top layer (popover) para aparecer acima de <dialog> modais abertos.
// Reabrir o popover a cada mudança o coloca acima do último dialog aberto.
const POPOVER_RESET = 'm-0 h-auto w-full overflow-visible border-0 bg-transparent p-0'

function useTopLayer(count: number) {
  const ref = useRef<HTMLDivElement>(null)
  useLayoutEffect(() => {
    const el = ref.current
    if (!el || typeof el.showPopover !== 'function') return
    if (el.matches(':popover-open')) el.hidePopover()
    if (count > 0) el.showPopover()
  }, [count])

  // Um dialog aberto depois do toast (ex.: "Nível N!") iria cobri-lo: reergue o popover.
  useLayoutEffect(() => {
    if (count === 0) return
    const raise = () => {
      const el = ref.current
      if (!el?.matches(':popover-open')) return
      el.hidePopover()
      el.showPopover()
    }
    const observer = new MutationObserver((records) => {
      if (records.some((r) => r.target instanceof HTMLDialogElement && r.target.open)) raise()
    })
    observer.observe(document.body, { subtree: true, attributes: true, attributeFilter: ['open'] })
    return () => observer.disconnect()
  }, [count])

  return ref
}

export function ToastProvider({ children }: { children: ReactNode }) {
  const [items, setItems] = useState<ToastItem[]>([])

  const dismiss = useCallback((id: number) => setItems((list) => list.filter((t) => t.id !== id)), [])

  const show = useCallback(
    ({ message, variant = 'info' }: ToastOptions) => {
      const id = nextId++
      setItems((list) => [...list.slice(-3), { id, message, variant }])
      window.setTimeout(() => dismiss(id), DURATION[variant])
    },
    [dismiss],
  )

  const api = useMemo<ToastApi>(
    () => ({
      show,
      error: (message) => show({ message, variant: 'error' }),
      success: (message) => show({ message, variant: 'success' }),
      xp: (amount) => show({ message: formatXp(amount), variant: 'xp' }),
      achievement: (emoji, title) => show({ message: i18n.t('layout:achievementUnlocked', { emoji, title }), variant: 'achievement' }),
    }),
    [show],
  )

  const xpToasts = items.filter((t) => t.variant === 'xp')
  const regular = items.filter((t) => t.variant !== 'xp')
  const xpRef = useTopLayer(xpToasts.length)
  const regularRef = useTopLayer(regular.length)

  return (
    <ToastContext.Provider value={api}>
      {children}

      {/* XP flutuante, no topo */}
      <div
        ref={xpRef}
        popover="manual"
        className={cn(POPOVER_RESET, 'pointer-events-none fixed inset-x-0 bottom-auto top-[max(1rem,env(safe-area-inset-top))] z-50 flex-col items-center gap-2 [&:popover-open]:flex')}
      >
        {xpToasts.map((t) => (
          <div
            key={t.id}
            role="status"
            className={cn(
              'animate-float-up rounded-full px-4 py-1.5 text-lg font-extrabold shadow-lg',
              t.message.startsWith('+') ? 'bg-xp text-bg' : 'bg-surface-2 text-muted border border-border',
            )}
          >
            {t.message}
          </div>
        ))}
      </div>

      {/* Toasts comuns, acima da barra de navegação no celular */}
      <div
        ref={regularRef}
        popover="manual"
        aria-live="polite"
        className={cn(
          POPOVER_RESET,
          'pointer-events-none fixed inset-x-0 top-auto bottom-[calc(5.5rem+env(safe-area-inset-bottom))] z-50 flex-col items-center gap-2 px-4 md:bottom-6 [&:popover-open]:flex',
        )}
      >
        {regular.map((t) => {
          const Icon = ICONS[t.variant]
          return (
            <div
              key={t.id}
              role={t.variant === 'error' ? 'alert' : 'status'}
              onClick={() => dismiss(t.id)}
              className={cn(
                'pointer-events-auto flex w-full max-w-sm animate-pop items-start gap-2 rounded-2xl border px-4 py-3 text-sm shadow-xl',
                'bg-surface border-border text-text',
                t.variant === 'error' && 'border-danger/50',
                t.variant === 'achievement' && 'border-xp/60 font-semibold',
              )}
            >
              <Icon
                className={cn(
                  'mt-0.5 size-4 shrink-0',
                  t.variant === 'error'
                    ? 'text-danger'
                    : t.variant === 'success'
                      ? 'text-done'
                      : t.variant === 'achievement'
                        ? 'text-xp'
                        : 'text-accent',
                )}
                aria-hidden
              />
              <span>{t.message}</span>
            </div>
          )
        })}
      </div>
    </ToastContext.Provider>
  )
}
