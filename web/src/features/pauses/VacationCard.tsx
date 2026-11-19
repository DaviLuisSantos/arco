import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Palmtree } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { vacationApi } from '../../api/endpoints'
import { Button } from '../../components/ui/Button'
import { Card } from '../../components/ui/Card'
import { ConfirmDialog } from '../../components/ui/ConfirmDialog'
import { Input } from '../../components/ui/Input'
import { useToast } from '../../components/ui/toast-context'
import { addDays, formatDate, localToday } from '../../lib/dates'
import { errorMessage } from '../../lib/errors'
import { queryKeys } from '../../lib/queryKeys'
import { useOnline } from '../../lib/useOnline'

const vacationKey = ['vacation'] as const

/** Modo férias: pausa todos os hábitos ativos de uma vez. */
export function VacationCard() {
  const { t } = useTranslation('vacation')
  const toast = useToast()
  const online = useOnline()
  const qc = useQueryClient()
  const { data } = useQuery({ queryKey: vacationKey, queryFn: vacationApi.get })

  const [startDate, setStartDate] = useState(() => localToday())
  const [endDate, setEndDate] = useState(() => addDays(localToday(), 7))
  const [reason, setReason] = useState('')
  const [confirming, setConfirming] = useState(false)

  function refresh() {
    void qc.invalidateQueries({ queryKey: vacationKey })
    void qc.invalidateQueries({ queryKey: queryKeys.today })
    void qc.invalidateQueries({ queryKey: queryKeys.habits })
  }

  const start = useMutation({
    mutationFn: () => vacationApi.start({ startDate, endDate, reason: reason.trim() || null }),
    onSuccess: ({ paused }) => {
      toast.success(t('activated', { count: paused }))
      setReason('')
      refresh()
    },
    onError: (e) => toast.error(errorMessage(e)),
  })

  const end = useMutation({
    mutationFn: () => vacationApi.end(),
    onSuccess: ({ resumed }) => {
      toast.success(t('ended', { count: resumed }))
      setConfirming(false)
      refresh()
    },
    onError: (e) => toast.error(errorMessage(e)),
  })

  if (!data) return null
  const invalid = !startDate || !endDate || endDate < startDate

  return (
    <Card className="flex flex-col gap-3">
      <div className="flex items-start gap-3">
        <Palmtree className="mt-0.5 size-5 shrink-0 text-accent" aria-hidden />
        <div className="min-w-0">
          <h2 className="font-semibold">{t('title')}</h2>
          {data.active && data.endDate ? (
            <p className="text-sm text-muted">
              {data.startDate && data.startDate > localToday()
                ? t('activeFrom', { start: formatDate(data.startDate), end: formatDate(data.endDate) })
                : t('activeUntil', { date: formatDate(data.endDate) })}
            </p>
          ) : (
            <p className="text-sm text-muted">{t('description')}</p>
          )}
        </div>
      </div>

      {data.active ? (
        <>
          <Button variant="secondary" disabled={!online} onClick={() => setConfirming(true)}>
            {t('endVacation')}
          </Button>
          <ConfirmDialog
            open={confirming}
            onClose={() => setConfirming(false)}
            onConfirm={() => end.mutate()}
            title={t('confirmTitle')}
            description={t('confirmDescription')}
            confirmLabel={t('endVacation')}
            loading={end.isPending}
          />
        </>
      ) : (
        <form
          className="flex flex-col gap-3"
          onSubmit={(e) => {
            e.preventDefault()
            if (!invalid) start.mutate()
          }}
        >
          <div className="grid grid-cols-2 gap-3">
            <Input label={t('start')} type="date" value={startDate} onChange={(e) => setStartDate(e.target.value)} required />
            <Input
              label={t('end')}
              type="date"
              value={endDate}
              min={startDate}
              onChange={(e) => setEndDate(e.target.value)}
              required
            />
          </div>
          <Input label={t('reason')} value={reason} maxLength={140} onChange={(e) => setReason(e.target.value)} />
          <Button type="submit" loading={start.isPending} disabled={!online || invalid}>
            {t('activate')}
          </Button>
        </form>
      )}
    </Card>
  )
}
