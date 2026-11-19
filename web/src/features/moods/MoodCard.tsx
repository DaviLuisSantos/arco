import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { moodsApi } from '../../api/endpoints'
import type { Mood } from '../../api/types'
import { Card } from '../../components/ui/Card'
import { useToast } from '../../components/ui/toast-context'
import { cn } from '../../lib/cn'
import { errorMessage } from '../../lib/errors'

const VALUES = [1, 2, 3, 4, 5] as const

/** Humor do dia (1 a 5), uma vez por dia. Tocar no valor atual remove. */
export function MoodCard({ date }: { date: string }) {
  const { t } = useTranslation('moods')
  const qc = useQueryClient()
  const toast = useToast()
  const key = ['moods', date] as const
  const q = useQuery({ queryKey: key, queryFn: () => moodsApi.list(date, date) })

  const save = useMutation<Mood | void, Error, number | null, { previous: Mood[] | undefined }>({
    networkMode: 'always',
    mutationFn: (value) => (value === null ? moodsApi.remove(date) : moodsApi.upsert({ date, value })),
    onMutate: async (value) => {
      await qc.cancelQueries({ queryKey: key })
      const previous = qc.getQueryData<Mood[]>(key)
      qc.setQueryData<Mood[]>(key, value === null ? [] : [{ date, value }])
      return { previous }
    },
    onError: (error, _value, ctx) => {
      if (ctx) qc.setQueryData(key, ctx.previous)
      toast.error(errorMessage(error))
    },
    onSettled: () => {
      void qc.invalidateQueries({ queryKey: key })
      void qc.invalidateQueries({ queryKey: ['reviews'] })
    },
  })

  if (q.isPending || q.isLoadingError) return null
  const current = q.data.find((m) => m.date === date)?.value ?? null

  return (
    <Card>
      <section aria-labelledby="mood-title">
        <h2 id="mood-title" className="text-sm font-semibold uppercase tracking-wide text-muted">
          {t('title')}
        </h2>
        <div className="mt-2 grid grid-cols-5 gap-1.5" role="group" aria-labelledby="mood-title">
          {VALUES.map((value) => {
            const selected = current === value
            const label = t(`values.v${value}`)
            return (
              <button
                key={value}
                type="button"
                aria-pressed={selected}
                title={label}
                onClick={() => save.mutate(selected ? null : value)}
                className={cn(
                  'flex min-h-11 min-w-11 flex-col items-center justify-center rounded-xl border px-1 py-1 text-sm font-semibold transition active:scale-95',
                  selected ? 'border-accent bg-accent/15 text-accent' : 'border-border bg-surface-2 text-muted hover:text-text',
                )}
              >
                {/* The accessible name comes from the visible text ("1: Muito ruim"), so it always contains it (axe label-content-name-mismatch). */}
                <span className="text-base tabular-nums">{value}</span>
                <span className="sr-only">: </span>
                <span className="w-full truncate text-center text-[10px] font-medium leading-tight">{label}</span>
              </button>
            )
          })}
        </div>
      </section>
    </Card>
  )
}
