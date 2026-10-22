import { X } from 'lucide-react'
import { useEffect, useId, useRef, type ReactNode } from 'react'
import { cn } from '../../lib/cn'

interface DialogProps {
  open: boolean
  onClose: () => void
  title: ReactNode
  description?: ReactNode
  children?: ReactNode
  footer?: ReactNode
  className?: string
}

/**
 * Diálogo modal nativo (<dialog>): bottom sheet no celular, centralizado a partir de md.
 * O conteúdo só é montado enquanto aberto, então formulários sempre começam limpos.
 */
export function Dialog({ open, onClose, title, description, children, footer, className }: DialogProps) {
  const ref = useRef<HTMLDialogElement>(null)
  const titleId = useId()
  const onCloseRef = useRef(onClose)

  useEffect(() => {
    onCloseRef.current = onClose
  }, [onClose])

  useEffect(() => {
    const dialog = ref.current
    if (!dialog) return
    if (open && !dialog.open) dialog.showModal()
    if (!open && dialog.open) dialog.close()
  }, [open])

  useEffect(() => {
    const dialog = ref.current
    if (!dialog) return
    const handleClose = () => onCloseRef.current()
    dialog.addEventListener('close', handleClose)
    return () => dialog.removeEventListener('close', handleClose)
  }, [])

  return (
    <dialog
      ref={ref}
      aria-labelledby={titleId}
      onClick={(e) => {
        // clique no backdrop fecha
        if (e.target === e.currentTarget) onClose()
      }}
      className={cn(
        'bg-surface text-text border border-border shadow-2xl backdrop:bg-black/60 backdrop:backdrop-blur-[2px]',
        'mx-0 mb-0 mt-auto w-full max-w-none max-h-[92dvh] rounded-t-3xl',
        'md:m-auto md:w-[min(32rem,calc(100%-2rem))] md:rounded-3xl',
        'open:animate-pop',
        className,
      )}
    >
      {open && (
        <div className="flex max-h-[92dvh] flex-col">
          <div className="mx-auto mt-2.5 h-1 w-10 shrink-0 rounded-full bg-border md:hidden" aria-hidden />
          <header className="flex items-start gap-3 px-5 pt-3 pb-3 md:pt-5">
            <div className="flex-1">
              <h2 id={titleId} className="text-lg font-bold">
                {title}
              </h2>
              {description && <p className="mt-1 text-sm text-muted">{description}</p>}
            </div>
            <button
              type="button"
              onClick={onClose}
              className="-mr-1 rounded-full p-2 text-muted hover:bg-surface-2 hover:text-text"
              aria-label="Fechar"
            >
              <X className="size-5" />
            </button>
          </header>
          <div className="flex-1 overflow-y-auto px-5 pb-4">{children}</div>
          {footer && (
            <footer className="flex gap-2 border-t border-border px-5 pt-3 pb-[max(1rem,env(safe-area-inset-bottom))]">
              {footer}
            </footer>
          )}
        </div>
      )}
    </dialog>
  )
}
