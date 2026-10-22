import { useQuery } from '@tanstack/react-query'
import { DatabaseBackup, Download } from 'lucide-react'
import { backupApi } from '../../api/endpoints'
import { Card } from '../../components/ui/Card'
import { formatDate } from '../../lib/dates'
import { useOnline } from '../../lib/useOnline'

const size = new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 1 })

function formatSize(bytes: number): string {
  return bytes < 1024 * 1024 ? `${size.format(bytes / 1024)} KB` : `${size.format(bytes / 1024 / 1024)} MB`
}

/** Backup no Perfil: estado das cópias automáticas do servidor e download de uma cópia para o aparelho. */
export function BackupCard() {
  const backups = useQuery({ queryKey: ['backups'], queryFn: backupApi.list })
  const online = useOnline()
  const latest = backups.data?.[0]

  return (
    <Card className="flex flex-col gap-3">
      <div className="flex items-start gap-3">
        <DatabaseBackup className="mt-0.5 size-5 shrink-0 text-accent" aria-hidden />
        <div className="min-w-0">
          <p className="font-semibold">Backup</p>
          <p className="text-sm text-muted">
            O servidor guarda uma cópia automática por dia (últimos 14 dias). Baixe uma cópia de vez em quando para
            guardar fora dele.
          </p>
          {backups.isSuccess && (
            <p className="mt-1 text-sm">
              {latest ? (
                <>
                  Última cópia automática: <span className="font-semibold">{formatDate(latest.date)}</span>
                  <span className="text-muted">
                    {' '}
                    · {backups.data.length} {backups.data.length === 1 ? 'cópia' : 'cópias'} · {formatSize(latest.sizeBytes)}
                  </span>
                </>
              ) : (
                <span className="text-muted">Nenhuma cópia automática ainda.</span>
              )}
            </p>
          )}
        </div>
      </div>
      {/* Link comum: o navegador baixa o arquivo com o cookie da sessão. */}
      <a
        href="/api/backup"
        download
        aria-disabled={!online}
        onClick={(e) => {
          if (!online) e.preventDefault()
        }}
        className="inline-flex h-11 items-center justify-center gap-2 rounded-xl border border-border bg-surface-2 px-4 text-sm font-semibold transition hover:border-accent/60 aria-disabled:cursor-not-allowed aria-disabled:opacity-50"
      >
        <Download className="size-4" aria-hidden />
        {online ? 'Baixar backup' : 'Baixar backup (precisa de internet)'}
      </a>
    </Card>
  )
}
