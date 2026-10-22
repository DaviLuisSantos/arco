import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Bell, BellOff, Send } from 'lucide-react'
import { useEffect, useState } from 'react'
import { pushApi, settingsApi } from '../../api/endpoints'
import type { Settings, WeeklyReviewReminder } from '../../api/types'
import { Button } from '../../components/ui/Button'
import { Card } from '../../components/ui/Card'
import { Input, Select } from '../../components/ui/Input'
import { Spinner } from '../../components/ui/States'
import { useToast } from '../../components/ui/toast-context'
import { WEEKDAY_NAMES } from '../../lib/dates'
import { errorMessage } from '../../lib/errors'
import { currentSubscription, disablePush, enablePush, hasServiceWorker, pushSupport } from '../../lib/push'
import { queryKeys } from '../../lib/queryKeys'
import { useAppMutation } from '../../lib/useAppMutation'

type DeviceState = 'loading' | 'on' | 'off' | 'denied' | 'no-sw' | 'needs-install' | 'unsupported'

async function readDeviceState(): Promise<DeviceState> {
  const support = pushSupport()
  if (support !== 'supported') return support
  if (!(await hasServiceWorker())) return 'no-sw'
  if (Notification.permission === 'denied') return 'denied'
  return (await currentSubscription()) && Notification.permission === 'granted' ? 'on' : 'off'
}

const STATE_TEXT: Record<Exclude<DeviceState, 'loading'>, string> = {
  on: 'Ativadas neste dispositivo.',
  off: 'Desativadas neste dispositivo.',
  denied: 'Bloqueadas pelo navegador. Libere as notificações do Arco nas configurações do navegador.',
  'no-sw': 'Disponíveis só no app publicado (o modo de desenvolvimento não registra o service worker).',
  'needs-install': 'No iPhone/iPad, instale o Arco na tela de início (Compartilhar → Adicionar à Tela de Início) e abra por lá.',
  unsupported: 'Este navegador não suporta notificações push.',
}

export function NotificationsCard({ settings }: { settings: Settings }) {
  const qc = useQueryClient()
  const toast = useToast()
  const [state, setState] = useState<DeviceState>('loading')
  const [busy, setBusy] = useState(false)
  const config = useQuery({ queryKey: queryKeys.pushConfig, queryFn: pushApi.config })

  useEffect(() => {
    let alive = true
    void readDeviceState().then((s) => alive && setState(s))
    return () => {
      alive = false
    }
  }, [])

  async function toggle() {
    setBusy(true)
    try {
      if (state === 'on') {
        await disablePush()
        toast.success('Notificações desativadas neste dispositivo.')
      } else {
        await enablePush()
        toast.success('Notificações ativadas.')
      }
    } catch (e) {
      toast.error(errorMessage(e))
    } finally {
      setState(await readDeviceState())
      void qc.invalidateQueries({ queryKey: queryKeys.pushConfig })
      setBusy(false)
    }
  }

  const test = useAppMutation(() => pushApi.test(), {
    onSuccess: ({ sent }) =>
      sent > 0 ? toast.success(`Teste enviado para ${sent} dispositivo${sent > 1 ? 's' : ''}.`) : toast.error('Nenhum dispositivo recebeu o teste.'),
  })

  const devices = config.data?.subscriptions ?? 0
  const canToggle = state === 'on' || state === 'off'

  return (
    <Card className="flex flex-col gap-4">
      <div className="flex flex-col gap-3">
        {state === 'loading' ? (
          <Spinner />
        ) : (
          <p className="text-sm text-muted">
            {STATE_TEXT[state]}
            {config.isSuccess && ` ${devices} dispositivo${devices === 1 ? '' : 's'} inscrito${devices === 1 ? '' : 's'}.`}
          </p>
        )}
        <div className="flex gap-2">
          {canToggle && (
            <Button variant={state === 'on' ? 'secondary' : 'primary'} block loading={busy} onClick={() => void toggle()}>
              {state === 'on' ? <BellOff className="size-4" aria-hidden /> : <Bell className="size-4" aria-hidden />}
              {state === 'on' ? 'Desativar' : 'Ativar notificações'}
            </Button>
          )}
          {devices > 0 && (
            <Button variant="secondary" block loading={test.isPending} onClick={() => test.mutate()}>
              <Send className="size-4" aria-hidden />
              Enviar teste
            </Button>
          )}
        </div>
        <p className="text-xs text-muted">O horário do lembrete de cada hábito é definido na edição do hábito.</p>
      </div>

      <WeeklyReminderForm key={JSON.stringify(settings.weeklyReviewReminder)} reminder={settings.weeklyReviewReminder} />
    </Card>
  )
}

function WeeklyReminderForm({ reminder }: { reminder: WeeklyReviewReminder }) {
  const [draft, setDraft] = useState(reminder)
  const save = useAppMutation(() => settingsApi.update({ weeklyReviewReminder: draft }), {
    successMessage: 'Lembrete da revisão salvo.',
  })
  const changed = draft.enabled !== reminder.enabled || draft.day !== reminder.day || draft.time !== reminder.time
  const validTime = /^([01]\d|2[0-3]):[0-5]\d$/.test(draft.time)

  return (
    <div className="flex flex-col gap-3 border-t border-border pt-4">
      <label className="flex items-center gap-3 text-sm font-medium">
        <input
          type="checkbox"
          checked={draft.enabled}
          onChange={(e) => setDraft({ ...draft, enabled: e.target.checked })}
          className="size-5 accent-[var(--accent-strong)]"
        />
        Lembrete da revisão semanal
      </label>
      {draft.enabled && (
        <div className="grid grid-cols-2 gap-3">
          <Select label="Dia" value={draft.day} onChange={(e) => setDraft({ ...draft, day: Number(e.target.value) })}>
            {WEEKDAY_NAMES.map((name, day) => (
              <option key={day} value={day}>
                {name}
              </option>
            ))}
          </Select>
          <Input
            label="Horário"
            type="time"
            value={draft.time}
            onChange={(e) => setDraft({ ...draft, time: e.target.value })}
            error={validTime ? null : 'Use HH:mm.'}
          />
        </div>
      )}
      <Button variant="secondary" disabled={!changed || !validTime} loading={save.isPending} onClick={() => save.mutate()}>
        Salvar lembrete
      </Button>
    </div>
  )
}
