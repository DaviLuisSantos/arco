import { X } from 'lucide-react'
import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useId,
  useRef,
  useState,
  type PointerEvent as ReactPointerEvent,
  type ReactNode,
} from 'react'
import { useTranslation } from 'react-i18next'
import { cn } from '../../lib/cn'
import { Button } from './Button'

const DialogGuardContext = createContext<(() => void) | null>(null)

/**
 * Retorna a função de fechar do Dialog mais próximo, respeitando `dirty`
 * (pede confirmação antes de descartar). Use nos botões "Cancelar" internos.
 */
// eslint-disable-next-line react-refresh/only-export-components
export function useDialogGuardedClose(): () => void {
  const guarded = useContext(DialogGuardContext)
  return guarded ?? (() => {})
}

interface DialogProps {
  open: boolean
  onClose: () => void
  title: ReactNode
  description?: ReactNode
  children?: ReactNode
  footer?: ReactNode
  className?: string
  /** Com alterações não salvas: Esc, backdrop e X pedem confirmação antes de fechar. */
  dirty?: boolean
}

const DRAG_CLOSE_DISTANCE = 100

/**
 * Diálogo modal nativo (<dialog>): bottom sheet no celular, centralizado a partir de md.
 * O conteúdo só é montado enquanto aberto, então formulários sempre começam limpos.
 * Botões "Cancelar" internos devem usar `useDialogGuardedClose()` para respeitar `dirty`.
 */
export function Dialog({ open, onClose, title, description, children, footer, className, dirty = false }: DialogProps) {
  const { t } = useTranslation(['layout', 'common'])
  const ref = useRef<HTMLDialogElement>(null)
  const titleId = useId()
  const descriptionId = useId()
  const onCloseRef = useRef(onClose)
  const dirtyRef = useRef(dirty)
  const [confirming, setConfirming] = useState(false)
  const dragStart = useRef<number | null>(null)

  useEffect(() => {
    onCloseRef.current = onClose
    dirtyRef.current = dirty
  }, [onClose, dirty])

  const requestClose = useCallback(() => {
    if (dirtyRef.current) setConfirming(true)
    else onCloseRef.current()
  }, [])

  useEffect(() => {
    const dialog = ref.current
    if (!dialog) return
    if (open && !dialog.open) dialog.showModal()
    if (!open && dialog.open) dialog.close()
  }, [open])

  useEffect(() => {
    const dialog = ref.current
    if (!dialog) return
    const handleClose = () => {
      setConfirming(false)
      onCloseRef.current()
    }
    // Esc: o fechamento nativo é cancelado e passa pela guarda.
    const handleCancel = (e: Event) => {
      e.preventDefault()
      requestClose()
    }
    dialog.addEventListener('close', handleClose)
    dialog.addEventListener('cancel', handleCancel)
    return () => {
      dialog.removeEventListener('close', handleClose)
      dialog.removeEventListener('cancel', handleCancel)
    }
  }, [requestClose])

  // Arrastar a alça para baixo fecha (opcional; o X segue como alternativa).
  const onDragStart = (e: ReactPointerEvent<HTMLDivElement>) => {
    dragStart.current = e.clientY
    e.currentTarget.setPointerCapture(e.pointerId)
  }
  const onDragMove = (e: ReactPointerEvent<HTMLDivElement>) => {
    if (dragStart.current === null || !ref.current) return
    const dy = Math.max(0, e.clientY - dragStart.current)
    ref.current.style.transform = `translateY(${dy}px)`
  }
  const onDragEnd = (e: ReactPointerEvent<HTMLDivElement>) => {
    if (dragStart.current === null || !ref.current) return
    const dy = e.clientY - dragStart.current
    dragStart.current = null
    ref.current.style.transform = ''
    if (dy > DRAG_CLOSE_DISTANCE) requestClose()
  }

  return (
    <dialog
      ref={ref}
      aria-labelledby={titleId}
      aria-describedby={description ? descriptionId : undefined}
      onClick={(e) => {
        // clique no backdrop fecha
        if (e.target === e.currentTarget) requestClose()
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
        <DialogGuardContext.Provider value={requestClose}>
          <div className="relative flex max-h-[92dvh] flex-col">
            <div
              className="flex shrink-0 cursor-grab touch-none justify-center py-2.5 md:hidden"
              onPointerDown={onDragStart}
              onPointerMove={onDragMove}
              onPointerUp={onDragEnd}
              onPointerCancel={onDragEnd}
              aria-hidden
            >
              <div className="h-1 w-10 rounded-full bg-border" />
            </div>
            <header className="flex items-start gap-3 px-5 pb-3 md:pt-5">
              <div className="flex-1 pt-1.5">
                <h2 id={titleId} className="text-lg font-bold">
                  {title}
                </h2>
                {description && (
                  <p id={descriptionId} className="mt-1 text-sm text-muted">
                    {description}
                  </p>
                )}
              </div>
              <button
                type="button"
                onClick={requestClose}
                className="-mr-2 grid size-11 shrink-0 place-items-center rounded-full text-muted hover:bg-surface-2 hover:text-text"
                aria-label={t('layout:close')}
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
            {confirming && <DiscardConfirm onKeep={() => setConfirming(false)} onDiscard={() => onCloseRef.current()} />}
          </div>
        </DialogGuardContext.Provider>
      )}
    </dialog>
  )
}

function DiscardConfirm({ onKeep, onDiscard }: { onKeep: () => void; onDiscard: () => void }) {
  const { t } = useTranslation('common')
  const titleId = useId()
  return (
    <div
      role="alertdialog"
      aria-labelledby={titleId}
      className="absolute inset-0 z-10 flex items-end justify-center bg-black/50 p-4 md:items-center"
    >
      <div className="w-full max-w-sm rounded-2xl border border-border bg-surface p-5 shadow-xl">
        <h3 id={titleId} className="text-base font-bold">
          {t('discardTitle')}
        </h3>
        <p className="mt-1 text-sm text-muted">{t('discardBody')}</p>
        <div className="mt-4 flex gap-2">
          <Button variant="secondary" block autoFocus onClick={onKeep}>
            {t('keepEditing')}
          </Button>
          <Button variant="danger" block onClick={onDiscard}>
            {t('discard')}
          </Button>
        </div>
      </div>
    </div>
  )
}
