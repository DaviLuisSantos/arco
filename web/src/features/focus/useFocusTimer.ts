import { useMutation } from '@tanstack/react-query'
import { useCallback, useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { focusApi } from '../../api/endpoints'
import type { FocusSession } from '../../api/types'
import { useToast } from '../../components/ui/toast-context'
import { localDateOf } from '../../lib/dates'
import { errorMessage } from '../../lib/errors'
import { useInvalidateData } from '../../lib/useInvalidateData'

const MAX_MINUTES = 480

/** Timer em andamento. Só guarda o instante de início: o tempo decorrido sai do relógio. */
export interface RunningTimer {
  habitId: number
  startedAt: string
  /** Minutos da contagem regressiva; null = cronômetro livre. */
  targetMinutes: number | null
}

/** Sessão que terminou mas ainda não foi gravada (rede caiu ou o servidor recusou). */
export interface PendingSession {
  habitId: number
  minutes: number
  date: string
  startedAt: string
}

const runningKey = (habitId: number) => `arco:focus:running:${habitId}`
const pendingKey = (habitId: number) => `arco:focus:pending:${habitId}`

function read<T>(key: string, valid: (v: unknown) => v is T): T | null {
  try {
    const raw = localStorage.getItem(key)
    if (!raw) return null
    const value: unknown = JSON.parse(raw)
    return valid(value) ? value : null
  } catch {
    return null
  }
}

function write(key: string, value: unknown) {
  try {
    if (value === null) localStorage.removeItem(key)
    else localStorage.setItem(key, JSON.stringify(value))
  } catch {
    // Sem armazenamento (modo privado/cheio): o timer segue funcionando só nesta aba.
  }
}

const isObject = (v: unknown): v is Record<string, unknown> => typeof v === 'object' && v !== null

function isRunning(v: unknown): v is RunningTimer {
  return (
    isObject(v) &&
    typeof v.habitId === 'number' &&
    typeof v.startedAt === 'string' &&
    !Number.isNaN(Date.parse(v.startedAt)) &&
    (v.targetMinutes === null || typeof v.targetMinutes === 'number')
  )
}

function isPending(v: unknown): v is PendingSession {
  return (
    isObject(v) &&
    typeof v.habitId === 'number' &&
    typeof v.minutes === 'number' &&
    typeof v.date === 'string' &&
    typeof v.startedAt === 'string'
  )
}

/** Segundos decorridos; negativo (relógio mudou) vira 0. */
function elapsedSeconds(timer: RunningTimer, now: number): number {
  return Math.max(0, Math.floor((now - Date.parse(timer.startedAt)) / 1000))
}

/**
 * Timer de foco de um hábito. Calcula o tempo por timestamp (sobrevive a recarregar
 * e a trocar de aba) e nunca perde uma sessão: se não der para gravar, fica pendente.
 */
export function useFocusTimer(habitId: number) {
  const toast = useToast()
  const { t } = useTranslation('focus')
  const invalidate = useInvalidateData()
  const [running, setRunning] = useState<RunningTimer | null>(() =>
    read(runningKey(habitId), isRunning),
  )
  const [pending, setPending] = useState<PendingSession | null>(() =>
    read(pendingKey(habitId), isPending),
  )
  const [saved, setSaved] = useState<FocusSession | null>(null)
  const [now, setNow] = useState(() => Date.now())

  const updateRunning = useCallback(
    (value: RunningTimer | null) => {
      write(runningKey(habitId), value)
      setRunning(value)
    },
    [habitId],
  )
  const updatePending = useCallback(
    (value: PendingSession | null) => {
      write(pendingKey(habitId), value)
      setPending(value)
    },
    [habitId],
  )

  // Outra aba mexeu no armazenamento. (Quem usa o hook remonta por hábito: use key={habitId}.)
  useEffect(() => {
    const sync = () => {
      setRunning(read(runningKey(habitId), isRunning))
      setPending(read(pendingKey(habitId), isPending))
    }
    window.addEventListener('storage', sync)
    return () => window.removeEventListener('storage', sync)
  }, [habitId])

  const save = useMutation({
    networkMode: 'always',
    mutationFn: (s: PendingSession) =>
      focusApi.create({ habitId: s.habitId, minutes: s.minutes, date: s.date, startedAt: s.startedAt }),
    onSuccess: ({ session }) => {
      updatePending(null)
      setSaved(session)
    },
    onError: (error) => toast.error(errorMessage(error)),
    onSettled: () => void invalidate(),
  })
  const { mutate: saveSession } = save

  const start = useCallback(
    (targetMinutes: number | null) => {
      if (running || pending) return
      setSaved(null)
      setNow(Date.now())
      updateRunning({ habitId, startedAt: new Date().toISOString(), targetMinutes })
    },
    [habitId, pending, running, updateRunning],
  )

  /** Encerra e grava os minutos decorridos (≥ 1). Menos que isso é descartado sem punição. */
  const finish = useCallback(() => {
    if (!running) return
    const cap = Math.min(running.targetMinutes ?? MAX_MINUTES, MAX_MINUTES)
    const minutes = Math.min(Math.floor(elapsedSeconds(running, Date.now()) / 60), cap)
    updateRunning(null)
    if (minutes < 1) {
      toast.show({ message: t('lessThanMinute'), variant: 'info' })
      return
    }
    const session: PendingSession = {
      habitId,
      minutes,
      date: localDateOf(running.startedAt),
      startedAt: running.startedAt,
    }
    updatePending(session)
    saveSession(session)
  }, [habitId, running, saveSession, t, toast, updatePending, updateRunning])

  const cancel = useCallback(() => updateRunning(null), [updateRunning])
  const retry = useCallback(() => {
    if (pending) saveSession(pending)
  }, [pending, saveSession])
  const discardPending = useCallback(() => updatePending(null), [updatePending])
  const dismissSaved = useCallback(() => setSaved(null), [])

  // Relógio de tela: só dispara renderização; o valor vem do timestamp.
  const isRunningNow = running !== null
  useEffect(() => {
    if (!isRunningNow) return
    const id = window.setInterval(() => setNow(Date.now()), 1000)
    const onVisible = () => setNow(Date.now())
    document.addEventListener('visibilitychange', onVisible)
    return () => {
      window.clearInterval(id)
      document.removeEventListener('visibilitychange', onVisible)
    }
  }, [isRunningNow])

  const elapsed = running ? elapsedSeconds(running, now) : 0
  const remaining = running?.targetMinutes != null ? Math.max(0, running.targetMinutes * 60 - elapsed) : null

  // Contagem regressiva chegou a zero (inclusive se o app ficou fechado): grava sozinho.
  useEffect(() => {
    if (!running || running.targetMinutes === null) return
    const endsAt = Date.parse(running.startedAt) + running.targetMinutes * 60_000
    const id = window.setTimeout(finish, Math.max(0, endsAt - Date.now()))
    return () => window.clearTimeout(id)
  }, [running, finish])

  return {
    running,
    pending,
    saved,
    elapsed,
    remaining,
    saving: save.isPending,
    start,
    finish,
    cancel,
    retry,
    discardPending,
    dismissSaved,
  }
}
