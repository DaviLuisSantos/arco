import { useQuery, useQueryClient } from '@tanstack/react-query'
import { ChevronRight, LogOut, NotebookPen, Sparkles } from 'lucide-react'
import { useState } from 'react'
import { Link, useNavigate } from 'react-router'
import { authApi, playerApi, settingsApi } from '../api/endpoints'
import type { SettingsInput, XpEvent } from '../api/types'
import { Button } from '../components/ui/Button'
import { Card } from '../components/ui/Card'
import { Select } from '../components/ui/Input'
import { PageHeader } from '../components/ui/PageHeader'
import { EmptyState, ErrorState, Spinner } from '../components/ui/States'
import { useToast } from '../components/ui/toast-context'
import { PlayerProgress } from '../features/player/PlayerProgress'
import { NotificationsCard } from '../features/push/NotificationsCard'
import { formatInstant } from '../lib/dates'
import { errorMessage } from '../lib/errors'
import { formatNumber, formatXp, XP_SOURCE_LABELS } from '../lib/format'
import { queryKeys } from '../lib/queryKeys'
import { COMMON_TIMEZONES } from '../lib/timezones'
import { useAppMutation } from '../lib/useAppMutation'

export function ProfilePage() {
  const player = useQuery({ queryKey: queryKeys.player, queryFn: playerApi.get })

  return (
    <div className="flex flex-col gap-5">
      <PageHeader title="Perfil" />

      {player.isPending ? (
        <Spinner />
      ) : player.isError ? (
        <ErrorState error={player.error} onRetry={() => void player.refetch()} />
      ) : (
        <>
          <Card className="flex flex-col gap-4">
            <PlayerProgress player={player.data} />
            <p className="text-sm text-muted">
              Total acumulado: <span className="font-semibold text-text">{formatNumber(player.data.totalXp)} XP</span>
            </p>
          </Card>
          <RecentEvents events={player.data.recentEvents} />
        </>
      )}

      <Link
        to="/review"
        className="flex items-center gap-3 rounded-2xl border border-border bg-surface p-4 transition hover:border-accent/60"
      >
        <NotebookPen className="size-5 text-accent" aria-hidden />
        <span className="flex-1 font-semibold">Revisões semanais</span>
        <ChevronRight className="size-5 text-muted" aria-hidden />
      </Link>
      <NotificationsSection />
      <SettingsCard />
      <LogoutButton />
    </div>
  )
}

function RecentEvents({ events }: { events: XpEvent[] }) {
  return (
    <section aria-labelledby="events-title">
      <h2 id="events-title" className="mb-2 text-sm font-semibold uppercase tracking-wide text-muted">
        XP recente
      </h2>
      {events.length === 0 ? (
        <EmptyState
          icon={<Sparkles className="size-6" />}
          title="Nenhum XP ainda"
          description="Faça o primeiro check-in na tela Hoje para começar a subir de nível."
        />
      ) : (
        <Card className="p-0">
          <ul className="divide-y divide-border">
            {events.map((e, i) => (
              <li key={`${e.date}-${i}`} className="flex items-center gap-3 px-4 py-3">
                <div className="min-w-0 flex-1">
                  <p className="truncate text-sm font-medium">{XP_SOURCE_LABELS[e.source] ?? e.source}</p>
                  <p className="truncate text-xs text-muted">
                    {e.description} · {formatInstant(e.date, { day: 'numeric', month: 'short' })}
                  </p>
                </div>
                <span className={e.amount >= 0 ? 'font-bold text-xp' : 'font-bold text-muted'}>{formatXp(e.amount)}</span>
              </li>
            ))}
          </ul>
        </Card>
      )}
    </section>
  )
}

function NotificationsSection() {
  const settings = useQuery({ queryKey: queryKeys.settings, queryFn: settingsApi.get })
  return (
    <section aria-labelledby="notifications-title">
      <h2 id="notifications-title" className="mb-2 text-sm font-semibold uppercase tracking-wide text-muted">
        Notificações
      </h2>
      {settings.isPending ? (
        <Spinner />
      ) : settings.isError ? (
        <ErrorState error={settings.error} onRetry={() => void settings.refetch()} />
      ) : (
        <NotificationsCard settings={settings.data} />
      )}
    </section>
  )
}

function SettingsCard() {
  const settings = useQuery({ queryKey: queryKeys.settings, queryFn: settingsApi.get })
  const [selected, setSelected] = useState<string | null>(null)
  const save = useAppMutation((input: SettingsInput) => settingsApi.update(input), {
    successMessage: 'Fuso horário salvo.',
    onSuccess: () => setSelected(null),
  })

  const currentTz = settings.data?.timezone ?? ''
  const value = selected ?? currentTz
  const options = COMMON_TIMEZONES.some((t) => t.id === currentTz) || !currentTz
    ? COMMON_TIMEZONES
    : [{ id: currentTz, label: currentTz }, ...COMMON_TIMEZONES]

  return (
    <section aria-labelledby="settings-title">
      <h2 id="settings-title" className="mb-2 text-sm font-semibold uppercase tracking-wide text-muted">
        Configurações
      </h2>
      <Card className="flex flex-col gap-3">
        {settings.isPending ? (
          <Spinner />
        ) : settings.isError ? (
          <ErrorState error={settings.error} onRetry={() => void settings.refetch()} />
        ) : (
          <>
            <Select
              label="Fuso horário"
              value={value}
              onChange={(e) => setSelected(e.target.value)}
              hint={`${value}. Define quando o “hoje” vira.`}
            >
              {options.map((t) => (
                <option key={t.id} value={t.id}>
                  {t.label}
                </option>
              ))}
            </Select>
            <Button
              variant="secondary"
              disabled={value === currentTz}
              loading={save.isPending}
              onClick={() => save.mutate({ timezone: value })}
            >
              Salvar fuso
            </Button>
          </>
        )}
      </Card>
    </section>
  )
}

function LogoutButton() {
  const qc = useQueryClient()
  const navigate = useNavigate()
  const toast = useToast()
  const [pending, setPending] = useState(false)

  async function logout() {
    setPending(true)
    try {
      await authApi.logout()
      qc.clear()
      qc.setQueryData(queryKeys.auth, false)
      void navigate('/login', { replace: true })
    } catch (e) {
      toast.error(errorMessage(e))
      setPending(false)
    }
  }

  return (
    <Button variant="danger" loading={pending} onClick={() => void logout()}>
      <LogOut className="size-4" aria-hidden />
      Sair
    </Button>
  )
}
