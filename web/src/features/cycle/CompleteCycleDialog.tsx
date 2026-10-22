import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { cyclesApi } from '../../api/endpoints'
import type { Cycle, CycleSummary } from '../../api/types'
import { Button } from '../../components/ui/Button'
import { Dialog } from '../../components/ui/Dialog'
import { Textarea } from '../../components/ui/Input'
import { ProgressBar } from '../../components/ui/ProgressBar'
import { ErrorState, Spinner } from '../../components/ui/States'
import { goalProgressText } from '../../lib/format'
import { queryKeys } from '../../lib/queryKeys'
import { useAppMutation } from '../../lib/useAppMutation'
import { PeriodStats } from '../reviews/PeriodStats'
import { cleanAnswers, CYCLE_PROMPTS, EMPTY_ANSWERS, type Answers } from '../reviews/review-answers'
import { ReviewQuestions } from '../reviews/ReviewQuestions'

const MAX_NOTES = 4000

interface CompleteCycleDialogProps {
  cycle: Cycle
  open: boolean
  onClose: () => void
}

/** Revisão guiada de fim de ciclo: números do ciclo → três perguntas (+ notas livres) → encerrar. */
export function CompleteCycleDialog({ cycle, open, onClose }: CompleteCycleDialogProps) {
  const [step, setStep] = useState<'numbers' | 'questions'>('numbers')
  const [answers, setAnswers] = useState<Answers>(EMPTY_ANSWERS)
  const [notes, setNotes] = useState('')
  const [error, setError] = useState<string | null>(null)
  const summary = useQuery({
    queryKey: queryKeys.cycleSummary(cycle.id),
    queryFn: () => cyclesApi.summary(cycle.id),
    enabled: open,
  })
  const complete = useAppMutation(
    () => cyclesApi.complete(cycle.id, { ...cleanAnswers(answers), reviewNotes: notes.trim() || null }),
    {
      successMessage: 'Ciclo encerrado. Hora de planejar o próximo!',
      onSuccess: () => {
        setAnswers(EMPTY_ANSWERS)
        setNotes('')
        close()
      },
    },
  )

  function close() {
    setStep('numbers')
    onClose()
  }

  function submit() {
    if (notes.length > MAX_NOTES) {
      setError(`Use até ${MAX_NOTES} caracteres nas notas.`)
      return
    }
    setError(null)
    complete.mutate()
  }

  return (
    <Dialog
      open={open}
      onClose={close}
      title={step === 'numbers' ? `Revisão: ${cycle.name}` : 'Encerrar ciclo'}
      description={
        step === 'numbers' ? (
          'Como foi o ciclo, em números.'
        ) : (
          <>
            Responda o que fizer sentido e encerre. <span className="font-semibold text-xp">+100 XP</span>
          </>
        )
      }
      footer={
        step === 'numbers' ? (
          <>
            <Button variant="secondary" block onClick={close}>
              Cancelar
            </Button>
            <Button block onClick={() => setStep('questions')}>
              Continuar
            </Button>
          </>
        ) : (
          <>
            <Button variant="secondary" block onClick={() => setStep('numbers')}>
              Voltar
            </Button>
            <Button block loading={complete.isPending} onClick={submit}>
              Encerrar ciclo
            </Button>
          </>
        )
      }
    >
      {step === 'numbers' ? (
        summary.isPending ? (
          <Spinner />
        ) : summary.isLoadingError ? (
          <ErrorState error={summary.error} onRetry={() => void summary.refetch()} />
        ) : (
          <CycleNumbers summary={summary.data} />
        )
      ) : (
        <div className="flex flex-col gap-4">
          <ReviewQuestions prompts={CYCLE_PROMPTS} value={answers} onChange={setAnswers} />
          <Textarea
            label="Notas livres"
            value={notes}
            onChange={(e) => setNotes(e.target.value)}
            rows={3}
            maxLength={MAX_NOTES}
            hint="Opcional."
            error={error}
            className="min-h-20"
          />
        </div>
      )}
    </Dialog>
  )
}

function CycleNumbers({ summary }: { summary: CycleSummary }) {
  const completedGoals = summary.goals.filter((g) => g.completedAt).length
  return (
    <div className="flex flex-col gap-4">
      {summary.goals.length > 0 && (
        <div className="flex flex-col gap-3">
          <p className="text-xs font-semibold uppercase tracking-wide text-muted">
            Objetivos · {completedGoals} de {summary.goals.length} concluídos
          </p>
          {summary.goals.map((g) => (
            <div key={g.id} className="flex flex-col gap-1">
              <div className="flex justify-between gap-2 text-sm">
                <span className="truncate font-medium">{g.title}</span>
                <span className="shrink-0 text-muted">{goalProgressText(g)}</span>
              </div>
              <ProgressBar value={g.progress} size="sm" tone={g.completedAt ? 'done' : 'accent'} label={g.title} />
            </div>
          ))}
        </div>
      )}
      <div className="flex flex-col gap-3">
        <p className="text-xs font-semibold uppercase tracking-wide text-muted">Hábitos</p>
        <PeriodStats xp={summary.xp} checkIns={summary.checkIns} habits={summary.habits} />
      </div>
      <p className="text-sm text-muted">
        {summary.weeklyReviews.length === 0
          ? 'Nenhuma revisão semanal neste ciclo.'
          : `${summary.weeklyReviews.length} revis${summary.weeklyReviews.length > 1 ? 'ões semanais' : 'ão semanal'} neste ciclo.`}
      </p>
    </div>
  )
}
