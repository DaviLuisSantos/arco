import { useQuery } from '@tanstack/react-query'
import { ArrowLeft, CircleCheck, NotebookPen } from 'lucide-react'
import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { reviewsApi } from '../api/endpoints'
import type { WeeklyReview, WeeklySummary } from '../api/types'
import { Button } from '../components/ui/Button'
import { Card } from '../components/ui/Card'
import { PageHeader } from '../components/ui/PageHeader'
import { ProgressBar } from '../components/ui/ProgressBar'
import { Skeleton } from '../components/ui/Skeleton'
import { ErrorState, Spinner } from '../components/ui/States'
import { PeriodStats } from '../features/reviews/PeriodStats'
import { cleanAnswers, EMPTY_ANSWERS, WEEK_PROMPTS, type Answers } from '../features/reviews/review-answers'
import { ReviewAnswersView, ReviewQuestions } from '../features/reviews/ReviewQuestions'
import { formatDate } from '../lib/dates'
import { goalProgressText } from '../lib/format'
import { queryKeys } from '../lib/queryKeys'
import { useAuthStatus } from '../lib/useAuthStatus'
import { useOnline } from '../lib/useOnline'
import { useAppMutation } from '../lib/useAppMutation'

const WEEKLY_REVIEW_XP = 30

function weekLabel(weekStart: string, weekEnd: string) {
  return `${formatDate(weekStart)} – ${formatDate(weekEnd)}`
}

export function ReviewPage({ embedded = false }: { embedded?: boolean }) {
  const { t } = useTranslation('pages', { keyPrefix: 'review' })
  const { t: tp } = useTranslation('progress')
  const online = useOnline()
  const current = useQuery({ queryKey: queryKeys.weeklyReview, queryFn: reviewsApi.current })
  const past = useQuery({ queryKey: queryKeys.weeklyReviews, queryFn: reviewsApi.list })

  const back = (
    <Link to="/" className="mb-4 inline-flex items-center gap-1 text-sm text-muted hover:text-text">
      <ArrowLeft className="size-4" aria-hidden />
      {t('back')}
    </Link>
  )

  if (current.isPending) {
    return online ? (
      <div className="flex flex-col gap-3" role="status" aria-label={tp('title')}>
        <Skeleton className="h-8 w-48" />
        <Skeleton className="h-32 w-full" />
        <Skeleton className="h-12 w-full" />
      </div>
    ) : (
      <Spinner />
    )
  }
  if (current.isLoadingError) return <ErrorState error={current.error} onRetry={() => void current.refetch()} />

  const s = current.data
  const older = (past.data ?? []).filter((r) => r.weekStart !== s.weekStart)

  return (
    <div className="flex flex-col gap-5">
      {embedded ? (
        <p className="text-sm text-muted">{weekLabel(s.weekStart, s.weekEnd)}</p>
      ) : (
        <div>
          {back}
          <PageHeader title={t('title')} subtitle={weekLabel(s.weekStart, s.weekEnd)} />
        </div>
      )}

      {s.review ? <DoneReview review={s.review} summary={s} /> : <GuidedReview summary={s} />}

      {past.isLoadingError && (
        <div role="alert" className="flex items-center justify-between gap-2 rounded-2xl border border-danger/40 bg-danger/10 p-3 text-sm">
          <span>{tp('secondaryError', { what: tp('what.pastReviews') })}</span>
          <Button size="sm" variant="secondary" onClick={() => void past.refetch()}>
            {tp('retry')}
          </Button>
        </div>
      )}

      {older.length > 0 && (
        <section aria-labelledby="past-reviews-title">
          <h2 id="past-reviews-title" className="mb-2 text-sm font-semibold uppercase tracking-wide text-muted">
            {t('previous')}
          </h2>
          <ul className="flex flex-col gap-2">
            {older.map((r) => (
              <li key={r.id}>
                <details className="group rounded-2xl border border-border bg-surface p-4">
                  <summary className="cursor-pointer list-none font-semibold [&::-webkit-details-marker]:hidden">
                    {t('weekOf', { range: weekLabel(r.weekStart, r.weekEnd) })}
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

function readDraft(key: string): Answers {
  try {
    const raw = sessionStorage.getItem(key)
    if (!raw) return EMPTY_ANSWERS
    const d = JSON.parse(raw) as Partial<Answers>
    return {
      wins: typeof d.wins === 'string' ? d.wins : '',
      obstacles: typeof d.obstacles === 'string' ? d.obstacles : '',
      next: typeof d.next === 'string' ? d.next : '',
    }
  } catch {
    return EMPTY_ANSWERS
  }
}

function WeekNumbers({ summary }: { summary: WeeklySummary }) {
  const { t } = useTranslation('pages', { keyPrefix: 'review' })
  return (
    <section aria-labelledby="week-numbers-title" className="flex flex-col gap-3">
      <h2 id="week-numbers-title" className="text-sm font-semibold uppercase tracking-wide text-muted">
        {t('weekNumbers')}
      </h2>
      <PeriodStats xp={summary.xp} checkIns={summary.checkIns} habits={summary.habits} />
      {summary.goals.length > 0 && (
        <Card className="flex flex-col gap-3">
          <p className="text-xs font-semibold uppercase tracking-wide text-muted">
            {t('goals')}{summary.cycle ? ` · ${summary.cycle.name}` : ''}
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
  const { t } = useTranslation('pages', { keyPrefix: 'review' })
  const [step, setStep] = useState<'numbers' | 'questions'>('numbers')
  const me = useAuthStatus()
  const draftKey = `arco-review-draft:${me.data?.id ?? 'anon'}:${summary.weekStart}`
  const [answers, setAnswers] = useState<Answers>(() => readDraft(draftKey))
  const [error, setError] = useState<string | null>(null)
  const save = useAppMutation(
    () => reviewsApi.create({ weekStart: summary.weekStart, ...cleanAnswers(answers) }),
    {
      successMessage: t('saved'),
      onSuccess: () => {
        try {
          sessionStorage.removeItem(draftKey)
        } catch {
          // sessionStorage indisponivel: nada a limpar.
        }
      },
    },
  )

  useEffect(() => {
    try {
      if (answers.wins || answers.obstacles || answers.next) sessionStorage.setItem(draftKey, JSON.stringify(answers))
      else sessionStorage.removeItem(draftKey)
    } catch {
      // Sem sessionStorage o rascunho so vive na tela.
    }
  }, [answers, draftKey])

  function submit() {
    const clean = cleanAnswers(answers)
    if (!clean.wins && !clean.obstacles && !clean.next) {
      setError(t('answerAtLeastOne'))
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
          {t('answer')}
        </Button>
      </>
    )
  }

  return (
    <>
      <Card className="flex flex-col gap-4">
        <p className="text-sm text-muted">
          {t('intro')}{' '}
          <span className="font-semibold text-xp">+{WEEKLY_REVIEW_XP} XP</span>
        </p>
        <ReviewQuestions prompts={WEEK_PROMPTS} value={answers} onChange={setAnswers} error={error} />
      </Card>
      <div className="flex gap-2">
        <Button variant="secondary" block onClick={() => setStep('numbers')}>
          {t('backToNumbers')}
        </Button>
        <Button block loading={save.isPending} onClick={submit}>
          {t('finish')}
        </Button>
      </div>
    </>
  )
}

function DoneReview({ review, summary }: { review: WeeklyReview; summary: WeeklySummary }) {
  const { t } = useTranslation('pages', { keyPrefix: 'review' })
  return (
    <>
      <Card className="flex flex-col gap-3">
        <p className="flex items-center gap-2 font-semibold text-done">
          <CircleCheck className="size-5" aria-hidden />
          {t('done')}
        </p>
        <ReviewAnswersView prompts={WEEK_PROMPTS} answers={review} />
      </Card>
      <WeekNumbers summary={summary} />
    </>
  )
}
