import { useQuery } from '@tanstack/react-query'
import { ArrowLeft, CircleCheck, NotebookPen } from 'lucide-react'
import { useState } from 'react'
import { Link } from 'react-router'
import { reviewsApi } from '../api/endpoints'
import type { WeeklyReview, WeeklySummary } from '../api/types'
import { Button } from '../components/ui/Button'
import { Card } from '../components/ui/Card'
import { PageHeader } from '../components/ui/PageHeader'
import { ProgressBar } from '../components/ui/ProgressBar'
import { ErrorState, Spinner } from '../components/ui/States'
import { PeriodStats } from '../features/reviews/PeriodStats'
import { cleanAnswers, EMPTY_ANSWERS, WEEK_PROMPTS, type Answers } from '../features/reviews/review-answers'
import { ReviewAnswersView, ReviewQuestions } from '../features/reviews/ReviewQuestions'
import { formatDate } from '../lib/dates'
import { goalProgressText } from '../lib/format'
import { queryKeys } from '../lib/queryKeys'
import { useAppMutation } from '../lib/useAppMutation'

const WEEKLY_REVIEW_XP = 30

function weekLabel(weekStart: string, weekEnd: string) {
  return `${formatDate(weekStart)} – ${formatDate(weekEnd)}`
}

export function ReviewPage() {
  const current = useQuery({ queryKey: queryKeys.weeklyReview, queryFn: reviewsApi.current })
  const past = useQuery({ queryKey: queryKeys.weeklyReviews, queryFn: reviewsApi.list })

  const back = (
    <Link to="/" className="mb-4 inline-flex items-center gap-1 text-sm text-muted hover:text-text">
      <ArrowLeft className="size-4" aria-hidden />
      Hoje
    </Link>
  )

  if (current.isPending) return <Spinner />
  if (current.isLoadingError) return <ErrorState error={current.error} onRetry={() => void current.refetch()} />

  const s = current.data
  const older = (past.data ?? []).filter((r) => r.weekStart !== s.weekStart)

  return (
    <div className="flex flex-col gap-5">
      <div>
        {back}
        <PageHeader title="Revisão da semana" subtitle={weekLabel(s.weekStart, s.weekEnd)} />
      </div>

      {s.review ? <DoneReview review={s.review} summary={s} /> : <GuidedReview summary={s} />}

      {older.length > 0 && (
        <section aria-labelledby="past-reviews-title">
          <h2 id="past-reviews-title" className="mb-2 text-sm font-semibold uppercase tracking-wide text-muted">
            Revisões anteriores
          </h2>
          <ul className="flex flex-col gap-2">
            {older.map((r) => (
              <li key={r.id}>
                <details className="group rounded-2xl border border-border bg-surface p-4">
                  <summary className="cursor-pointer list-none font-semibold [&::-webkit-details-marker]:hidden">
                    Semana de {weekLabel(r.weekStart, r.weekEnd)}
                  </summary>
                  <div className="mt-3">
                    <ReviewAnswersView prompts={WEEK_PROMPTS} answers={r} />
                  </div>
                </details>
              </li>
            ))}
          </ul>
        </section>
      )}
    </div>
  )
}

function WeekNumbers({ summary }: { summary: WeeklySummary }) {
  return (
    <section aria-labelledby="week-numbers-title" className="flex flex-col gap-3">
      <h2 id="week-numbers-title" className="text-sm font-semibold uppercase tracking-wide text-muted">
        A semana em números
      </h2>
      <PeriodStats xp={summary.xp} checkIns={summary.checkIns} habits={summary.habits} />
      {summary.goals.length > 0 && (
        <Card className="flex flex-col gap-3">
          <p className="text-xs font-semibold uppercase tracking-wide text-muted">
            Objetivos{summary.cycle ? ` · ${summary.cycle.name}` : ''}
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
        </Card>
      )}
    </section>
  )
}

function GuidedReview({ summary }: { summary: WeeklySummary }) {
  const [step, setStep] = useState<'numbers' | 'questions'>('numbers')
  const [answers, setAnswers] = useState<Answers>(EMPTY_ANSWERS)
  const [error, setError] = useState<string | null>(null)
  const save = useAppMutation(
    () => reviewsApi.create({ weekStart: summary.weekStart, ...cleanAnswers(answers) }),
    { successMessage: 'Revisão registrada. Boa semana!' },
  )

  function submit() {
    const clean = cleanAnswers(answers)
    if (!clean.wins && !clean.obstacles && !clean.next) {
      setError('Responda ao menos uma pergunta.')
      return
    }
    setError(null)
    save.mutate()
  }

  if (step === 'numbers') {
    return (
      <>
        <WeekNumbers summary={summary} />
        <Button size="lg" onClick={() => setStep('questions')}>
          <NotebookPen className="size-4" aria-hidden />
          Responder a revisão
        </Button>
      </>
    )
  }

  return (
    <>
      <Card className="flex flex-col gap-4">
        <p className="text-sm text-muted">
          Três perguntas rápidas. Responda o que fizer sentido.{' '}
          <span className="font-semibold text-xp">+{WEEKLY_REVIEW_XP} XP</span>
        </p>
        <ReviewQuestions prompts={WEEK_PROMPTS} value={answers} onChange={setAnswers} error={error} />
      </Card>
      <div className="flex gap-2">
        <Button variant="secondary" block onClick={() => setStep('numbers')}>
          Voltar aos números
        </Button>
        <Button block loading={save.isPending} onClick={submit}>
          Concluir revisão
        </Button>
      </div>
    </>
  )
}

function DoneReview({ review, summary }: { review: WeeklyReview; summary: WeeklySummary }) {
  return (
    <>
      <Card className="flex flex-col gap-3">
        <p className="flex items-center gap-2 font-semibold text-done">
          <CircleCheck className="size-5" aria-hidden />
          Revisão feita
        </p>
        <ReviewAnswersView prompts={WEEK_PROMPTS} answers={review} />
      </Card>
      <WeekNumbers summary={summary} />
    </>
  )
}
