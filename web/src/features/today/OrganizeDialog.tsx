import { useMutation, useQueryClient } from '@tanstack/react-query'
import { ArrowDown, ArrowUp, ArrowUpDown } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { habitsApi } from '../../api/endpoints'
import type { TimeOfDay, TodayHabit } from '../../api/types'
import { Button } from '../../components/ui/Button'
import { Dialog, useDialogGuardedClose } from '../../components/ui/Dialog'
import { useToast } from '../../components/ui/toast-context'
import { errorMessage } from '../../lib/errors'
import { queryKeys } from '../../lib/queryKeys'
import { PERIODS } from './PeriodGroup'

type Orders = Record<TimeOfDay, number[]>

/** Ordem manual de cada período (SortOrder, depois nome), sem o encadeamento do dia. */
function buildOrders(habits: TodayHabit[]): Orders {
  const orders: Orders = { Anytime: [], Morning: [], Afternoon: [], Evening: [] }
  const sorted = [...habits].sort(
    (a, b) =>
      a.sortOrder - b.sortOrder ||
      a.name.localeCompare(b.name, undefined, { sensitivity: 'base' }) ||
      a.id - b.id,
  )
  for (const h of sorted) orders[h.timeOfDay].push(h.id)
  return orders
}

/** Botão "Organizar" do Hoje: guarda o estado do diálogo e o recria limpo a cada abertura. */
export function OrganizeButton({ habits }: { habits: TodayHabit[] }) {
  const { t } = useTranslation('pages', { keyPrefix: 'today.organize' })
  const [open, setOpen] = useState(false)
  const [session, setSession] = useState(0)

  return (
    <>
      <Button
        variant="ghost"
        size="sm"
        onClick={() => {
          setSession((s) => s + 1)
          setOpen(true)
        }}
      >
        <ArrowUpDown className="size-4" aria-hidden />
        {t('open')}
      </Button>
      <OrganizeDialog key={session} open={open} onClose={() => setOpen(false)} habits={habits} />
    </>
  )
}

function CancelButton() {
  const { t } = useTranslation('common')
  const close = useDialogGuardedClose()
  return (
    <Button variant="secondary" block onClick={close}>
      {t('cancel')}
    </Button>
  )
}

function OrganizeDialog({ open, onClose, habits }: { open: boolean; onClose: () => void; habits: TodayHabit[] }) {
  const { t } = useTranslation(['pages', 'habits', 'common'])
  const toast = useToast()
  const qc = useQueryClient()
  const [initial] = useState(() => buildOrders(habits))
  const [orders, setOrders] = useState<Orders>(initial)
  const [announcement, setAnnouncement] = useState('')
  const listRef = useRef<HTMLDivElement>(null)
  // Botão que deve receber o foco depois de mover (a lista se reordena e o nó pode perder o foco).
  const focusTarget = useRef<{ id: number; dir: 'up' | 'down' } | null>(null)
  const names = new Map(habits.map((h) => [h.id, h.name]))

  const changedPeriods = PERIODS.filter((p) => orders[p].join() !== initial[p].join())
  const dirty = changedPeriods.length > 0

  useEffect(() => {
    const target = focusTarget.current
    if (!target) return
    focusTarget.current = null
    const find = (dir: 'up' | 'down') =>
      listRef.current?.querySelector<HTMLButtonElement>(`[data-move="${target.id}-${dir}"]`) ?? null
    const preferred = find(target.dir)
    // No limite da lista o botão fica desativado: o foco vai para o oposto.
    const button = preferred && !preferred.disabled ? preferred : find(target.dir === 'up' ? 'down' : 'up')
    button?.focus()
  }, [orders])

  function move(period: TimeOfDay, id: number, dir: 'up' | 'down') {
    const ids = [...orders[period]]
    const from = ids.indexOf(id)
    const to = dir === 'up' ? from - 1 : from + 1
    if (from < 0 || to < 0 || to >= ids.length) return
    ;[ids[from], ids[to]] = [ids[to], ids[from]]
    focusTarget.current = { id, dir }
    setOrders({ ...orders, [period]: ids })
    setAnnouncement(t('pages:today.organize.moved', { name: names.get(id), position: to + 1, total: ids.length }))
  }

  const save = useMutation({
    mutationFn: async () => {
      for (const p of changedPeriods) await habitsApi.reorder(p, orders[p])
    },
    onSuccess: () => {
      toast.success(t('pages:today.organize.saved'))
      onClose()
    },
    onError: (e) => toast.error(errorMessage(e)),
    // Mesmo com falha parcial, o servidor pode ter gravado alguns períodos.
    onSettled: () => {
      void qc.invalidateQueries({ queryKey: queryKeys.today })
      void qc.invalidateQueries({ queryKey: queryKeys.habits })
    },
  })

  return (
    <Dialog
      open={open}
      onClose={onClose}
      dirty={dirty}
      title={t('pages:today.organize.title')}
      description={t('pages:today.organize.description')}
      footer={
        <>
          <CancelButton />
          <Button block disabled={!dirty} loading={save.isPending} onClick={() => save.mutate()}>
            {t('pages:today.organize.save')}
          </Button>
        </>
      }
    >
      <div ref={listRef} className="flex flex-col gap-5">
        {PERIODS.filter((p) => orders[p].length > 0).map((p) => (
          <section key={p} aria-labelledby={`organize-${p}`}>
            <h3 id={`organize-${p}`} className="mb-2 text-sm font-semibold text-muted">
              {t(`habits:form.period.${p}`)}
            </h3>
            <ul className="flex flex-col gap-2">
              {orders[p].map((id, index) => {
                const name = names.get(id) ?? ''
                const habit = habits.find((h) => h.id === id)
                return (
                  <li key={id} className="flex items-center gap-2 rounded-xl border border-border bg-surface-2 px-3 py-1">
                    <span aria-hidden className="text-xl">
                      {habit?.emoji || '•'}
                    </span>
                    <span className="min-w-0 flex-1 truncate font-medium">{name}</span>
                    <button
                      type="button"
                      data-move={`${id}-up`}
                      aria-label={t('pages:today.organize.moveUp', { name })}
                      disabled={index === 0}
                      onClick={() => move(p, id, 'up')}
                      className="grid size-11 shrink-0 place-items-center rounded-full text-muted hover:bg-surface hover:text-text disabled:opacity-30 disabled:pointer-events-none"
                    >
                      <ArrowUp className="size-5" aria-hidden />
                    </button>
                    <button
                      type="button"
                      data-move={`${id}-down`}
                      aria-label={t('pages:today.organize.moveDown', { name })}
                      disabled={index === orders[p].length - 1}
                      onClick={() => move(p, id, 'down')}
                      className="grid size-11 shrink-0 place-items-center rounded-full text-muted hover:bg-surface hover:text-text disabled:opacity-30 disabled:pointer-events-none"
                    >
                      <ArrowDown className="size-5" aria-hidden />
                    </button>
                  </li>
                )
              })}
            </ul>
          </section>
        ))}
      </div>
      <p role="status" aria-live="polite" className="sr-only">
        {announcement}
      </p>
    </Dialog>
  )
}
