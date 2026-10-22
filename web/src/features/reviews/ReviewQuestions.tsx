import type { ReviewAnswers } from '../../api/types'
import { Textarea } from '../../components/ui/Input'
import { MAX_ANSWER, type Answers, type ReviewPrompts } from './review-answers'

interface ReviewQuestionsProps {
  prompts: ReviewPrompts
  value: Answers
  onChange: (value: Answers) => void
  error?: string | null
}

export function ReviewQuestions({ prompts, value, onChange, error }: ReviewQuestionsProps) {
  const keys: Array<keyof Answers> = ['wins', 'obstacles', 'next']
  return (
    <div className="flex flex-col gap-4">
      {keys.map((key, i) => (
        <Textarea
          key={key}
          label={prompts[key]}
          value={value[key]}
          onChange={(e) => onChange({ ...value, [key]: e.target.value })}
          rows={3}
          maxLength={MAX_ANSWER}
          autoFocus={i === 0}
          className="min-h-20"
        />
      ))}
      {error && <p className="text-sm text-danger">{error}</p>}
    </div>
  )
}

/** Mostra as respostas já registradas, pulando as vazias. */
export function ReviewAnswersView({ prompts, answers }: { prompts: ReviewPrompts; answers: ReviewAnswers }) {
  const items = (['wins', 'obstacles', 'next'] as const).filter((k) => answers[k])
  if (items.length === 0) return null
  return (
    <dl className="flex flex-col gap-3">
      {items.map((k) => (
        <div key={k}>
          <dt className="text-xs font-semibold uppercase tracking-wide text-muted">{prompts[k]}</dt>
          <dd className="mt-0.5 whitespace-pre-line text-sm">{answers[k]}</dd>
        </div>
      ))}
    </dl>
  )
}
