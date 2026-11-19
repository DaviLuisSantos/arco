import { Check, Flag, Minus, SkipForward, X } from 'lucide-react'
import { useRef, useState } from 'react'
import { Link } from 'react-router'
import { useTranslation } from 'react-i18next'
import type { CheckInStatus, TodayHabit } from '../../api/types'
import { cn } from '../../lib/cn'
import { formatDuration, streakText } from '../../lib/format'
import { statusAfterTotal } from '../../lib/quantity'
import { QuantityControls } from './QuantityControls'
import { useCheckIn } from './useCheckIn'

const OPTIONS: Array<{ status: CheckInStatus; icon: typeof Check; active: string }> = [
  { status: 'Done', icon: Check, active: 'bg-done text-bg border-done' },
  { status: 'Minimum', icon: Minus, active: 'bg-minimum text-bg border-minimum' },
  { status: 'Missed', icon: X, active: 'bg-missed/15 text-missed border-missed/60' },
  { status: 'Skipped', icon: SkipForward, active: 'bg-surface-2 text-text border-text/40' },
]

export function HabitCheckInCard({ habit, date }: { habit: TodayHabit; date: string }) {
  const { t } = useTranslation('checkins')
  const checkIn = useCheckIn()
  const current = habit.checkIn?.status ?? null
  const savedMinutes = habit.checkIn?.minutes ?? null
  // null = acompanha o valor salvo; string = rascunho em edição
  const [draft, setDraft] = useState<string | null>(null)
  const text = draft ?? (savedMinutes ? String(savedMinutes) : '')
  const parsed = text.trim() === '' ? 0 : Number(text)
  const validMinutes = Number.isInteger(parsed) && parsed >= 0 && parsed <= 480
  const changed = validMinutes && parsed !== (savedMinutes ?? 0)
  const savedNote = habit.checkIn?.note ?? ''
  // null = acompanha a nota salva; string = rascunho em edição
  const [noteDraft, setNoteDraft] = useState<string | null>(null)
  const noteText = noteDraft ?? savedNote
  const noteChanged = noteText.trim() !== savedNote
  const [noteOpen, setNoteOpen] = useState(false)
  // Hábito quantitativo: Feito/Mínimo vêm do total do dia (não são botões). Nota e minutos reenviam o status
  // derivado, que o servidor aceita; a fila manda o total antes, então o status já confere quando eles chegam.
  const quantitative = Boolean(habit.unit) && habit.target !== null && habit.minimumTarget !== null
  const showNoteField = current === 'Skipped' || savedNote !== '' || noteOpen
  const statusGroup = useRef<HTMLDivElement>(null)
  const options = quantitative ? OPTIONS.filter((o) => o.status === 'Missed' || o.status === 'Skipped') : OPTIONS
  const busy = checkIn.isPending && !checkIn.isPaused

  function setTotal(total: number) {
    // O total absoluto vai pela mesma fila do check-in; o status esperado só serve ao otimismo.
    const status = statusAfterTotal(current, total, habit.target ?? 0, habit.minimumTarget ?? 0)
    checkIn.mutate({ habit, date, status, total })
  }

  function handleTap(status: CheckInStatus) {
    // tocar no status atual desfaz
    const next = current === status ? null : status
    // Minutos digitados vão junto com a troca de status: uma só mutação, sem disputa com o blur.
    const withMinutes = (next === 'Done' || next === 'Minimum') && changed ? { minutes: parsed } : {}
    setDraft(null)
    // Sem `note`: o servidor mantém a nota salva ao trocar de status.
    checkIn.mutate({ habit, date, status: next, ...withMinutes })
  }

  function saveMinutes() {
    if (!current || !changed) return
    checkIn.mutate({ habit, date, status: current, minutes: parsed }, { onSuccess: () => setDraft(null) })
  }

  function saveNote() {
    if (!current || !noteChanged) return
    checkIn.mutate({ habit, date, status: current, note: noteText.trim() }, { onSuccess: () => setNoteDraft(null) })
  }

  function handleBlur(e: React.FocusEvent<HTMLInputElement>) {
    // Indo para um botão de status, o toque dele já leva os minutos.
    if (e.relatedTarget instanceof Node && statusGroup.current?.contains(e.relatedTarget)) return
    saveMinutes()
  }

  return (
    <article
      className={cn(
        'rounded-2xl border bg-surface p-4',
        // Sua vez: a âncora foi feita e este hábito ainda está sem check-in.
        habit.anchorDone ? 'border-accent/60' : 'border-border',
      )}
    >
      <div className="flex items-start gap-3">
        <span className="grid size-11 shrink-0 place-items-center rounded-xl bg-surface-2 text-2xl" aria-hidden>
          {habit.emoji || '•'}
        </span>
        <div className="min-w-0 flex-1">
          <Link to={`/habits/${habit.id}`} className="line-clamp-2 font-semibold break-words hover:underline">
            {habit.name}
          </Link>
          {(habit.anchorName || habit.anchorDone) && (
            <p className="flex flex-wrap items-center gap-x-2 gap-y-1 text-xs text-muted">
              {habit.anchorName && <span className="break-words">{t('card.after', { name: habit.anchorName })}</span>}
              {habit.anchorDone && (
                <span className="rounded-full bg-accent/15 px-2 py-0.5 font-semibold text-accent">{t('card.yourTurn')}</span>
              )}
            </p>
          )}
          <p className="truncate text-sm text-muted">
            {t('card.level', { level: habit.level })} · {habit.levelDescription}
          </p>
          <p className="truncate text-xs text-muted/90">{t('card.minimum', { value: habit.minimumDescription })}</p>
          <div className="mt-1.5 flex flex-wrap gap-1.5">
            {habit.streak.current > 0 && (
              <span
                className="rounded-full bg-surface-2 px-2 py-0.5 text-xs font-medium text-text"
                title={t('card.streakTitle', {
                  current: streakText(habit.streak),
                  best: streakText({ ...habit.streak, current: habit.streak.best }),
                })}
              >
                <span aria-hidden>🔥 </span>
                {habit.streak.current}
                <span className="sr-only"> {t('card.streakSr')}</span>
              </span>
            )}
            {habit.weekTarget !== null && (
              <span
                className={cn(
                  'rounded-full px-2 py-0.5 text-xs font-medium',
                  habit.weekCount >= habit.weekTarget ? 'bg-done/15 text-done' : 'bg-surface-2 text-muted',
                )}
              >
                {t('card.weekProgress', { done: habit.weekCount, target: habit.weekTarget })}
              </span>
            )}
            {habit.goalTitle && (
              <span className="inline-flex max-w-full items-center gap-1 truncate rounded-full bg-accent/15 px-2 py-0.5 text-xs font-medium text-accent">
                <Flag className="size-3 shrink-0" aria-hidden />
                <span className="truncate">{habit.goalTitle}</span>
              </span>
            )}
          </div>
        </div>
      </div>

      {quantitative && <QuantityControls habit={habit} onSet={setTotal} disabled={busy} />}

      <div
        ref={statusGroup}
        className={cn('mt-3 grid gap-1.5', quantitative ? 'grid-cols-2' : 'grid-cols-4')}
        role="group"
        aria-label={t('card.checkInOf', { name: habit.name })}
      >
        {options.map(({ status, icon: Icon, active }) => {
          const selected = current === status
          return (
            <button
              key={status}
              type="button"
              aria-pressed={selected}
              // Pausado (offline, ou esperando a fila): deixa trocar de novo.
              disabled={busy}
              onClick={() => handleTap(status)}
              className={cn(
                'flex h-12 min-w-0 items-center justify-center gap-1.5 rounded-xl border px-1 text-sm font-semibold transition active:scale-95',
                'disabled:cursor-wait',
                selected ? active : 'border-border bg-surface-2 text-muted hover:text-text',
              )}
            >
              <Icon className="size-4 shrink-0" aria-hidden />
              {t(`status.${status}`)}
            </button>
          )
        })}
      </div>

      {current !== null &&!showNoteField && (
        <button
          type="button"
          onClick={() => setNoteOpen(true)}
          className="mt-2 inline-flex min-h-11 items-center rounded-xl px-1 text-sm font-medium text-muted hover:text-text"
        >
          {t('card.addNote')}
        </button>
      )}

      {current !== null &&showNoteField && (
        <form
          onSubmit={(e) => {
            e.preventDefault()
            saveNote()
          }}
          className="mt-2 flex items-center gap-2"
        >
          <label htmlFor={`note-${habit.id}`} className="sr-only">
            {current === 'Skipped' ? t('card.noteLabel') : t('card.noteOptional')}
          </label>
          <input
            id={`note-${habit.id}`}
            type="text"
            enterKeyHint="done"
            maxLength={280}
            autoFocus={noteOpen && savedNote === '' && current !== 'Skipped'}
            placeholder={current === 'Skipped' ? t('card.noteLabel') : t('card.noteOptional')}
            value={noteText}
            onChange={(e) => setNoteDraft(e.target.value)}
            onBlur={saveNote}
            className="h-10 min-w-0 flex-1 rounded-xl border border-border bg-surface-2 px-3 text-sm"
          />
        </form>
      )}

      {(current === 'Done' || current === 'Minimum') && (
        <form
          onSubmit={(e) => {
            e.preventDefault()
            saveMinutes()
          }}
          className="mt-2 flex items-center gap-2"
        >
          <label htmlFor={`minutes-${habit.id}`} className="shrink-0 text-sm text-muted">
            {t('card.minutesQuestion')}
          </label>
          <input
            id={`minutes-${habit.id}`}
            type="number"
            inputMode="numeric"
            enterKeyHint="done"
            min={0}
            max={480}
            step={1}
            placeholder={t('card.optional')}
            value={text}
            onChange={(e) => setDraft(e.target.value)}
            onBlur={handleBlur}
            aria-invalid={!validMinutes}
            aria-describedby={`minutes-hint-${habit.id}`}
            className="h-10 min-w-0 flex-1 rounded-xl border border-border bg-surface-2 px-3 text-sm"
          />
          <span id={`minutes-hint-${habit.id}`} className="sr-only">
            {t('card.minutesHint')}
          </span>
          {!changed && savedMinutes ? (
            <span className="shrink-0 text-xs text-muted">{formatDuration(savedMinutes)}</span>
          ) : null}
        </form>
      )}
    </article>
  )
}
