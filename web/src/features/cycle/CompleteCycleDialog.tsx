import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
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
  const { t } = useTranslation(['cycle', 'common'])
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
      successMessage: t('complete.success'),
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
      setError(t('complete.notesTooLong', { max: MAX_NOTES }))
      return
    }
    setError(null)
    complete.mutate()
  }

  return (
    <Dialog
      open={open}
      onClose={close}
      title={step === 'numbers' ? t('complete.reviewTitle', { name: cycle.name }) : t('complete.endCycle')}
      description={
        step === 'numbers' ? (
          t('complete.numbersDescription')
        ) : (
          <>
            {t('complete.questionsDescription')} <span className="font-semibold text-xp">+100 XP</span>
          </>
        )
      }
      footer={
        step === 'numbers' ? (
          <>
            <Button variant="secondary" block onClick={close}>
              {t('common:cancel')}
            </Button>
            <Button block onClick={() => setStep('questions')}>
              {t('complete.continue')}
            </Button>
          </>
        ) : (
          <>
            <Button variant="secondary" block onClick={() => setStep('numbers')}>
              {t('common:back')}
            </Button>
            <Button block loading={complete.isPending} onClick={submit}>
              {t('complete.endCycle')}
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
            label={t('complete.notes')}
            value={notes}
            onChange={(e) => setNotes(e.target.value)}
            rows={3}
            maxLength={MAX_NOTES}
            hint={t('complete.optional')}
            error={error}
            className="min-h-20"
          />
        </div>
      )}
    </Dialog>
  )
}

function CycleNumbers({ summary }: { summary: CycleSummary }) {
  const { t } = useTranslation(['cycle', 'common'])
  const completedGoals = summary.goals.filter((g) => g.completedAt).length
  return (
    <div className="flex flex-col gap-4">
      {summary.goals.length > 0 && (
        <div className="flex flex-col gap-3">
          <p className="text-xs font-semibold uppercase tracking-wide text-muted">
            {t('complete.goalsDone', { done: completedGoals, total: summary.goals.length })}
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
        <p className="text-xs font-semibold uppercase tracking-wide text-muted">{t('complete.habits')}</p>
        <PeriodStats xp={summary.xp} checkIns={summary.checkIns} habits={summary.habits} />
      </div>
      <p className="text-sm text-muted">
        {summary.weeklyReviews.length === 0
          ? t('complete.weeklyReviewsNone')
          : t('complete.weeklyReviews', { count: summary.weeklyReviews.length })}
      </p>
    </div>
  )
}
