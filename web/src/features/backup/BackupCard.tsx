import { Download } from 'lucide-react'
import { useState } from 'react'
import { exportApi } from '../../api/endpoints'
import { Button } from '../../components/ui/Button'
import { Card } from '../../components/ui/Card'
import { useToast } from '../../components/ui/toast-context'
import { errorMessage } from '../../lib/errors'
import { useOnline } from '../../lib/useOnline'

/** Exporta todos os dados do usuário em um arquivo JSON. */
export function BackupCard() {
  const online = useOnline()
  const toast = useToast()
  const [pending, setPending] = useState(false)

  async function download() {
    setPending(true)
    try {
      await exportApi.download()
    } catch (e) {
      toast.error(errorMessage(e))
    } finally {
      setPending(false)
    }
  }

  return (
    <Card className="flex flex-col gap-3">
      <div className="flex items-start gap-3">
        <Download className="mt-0.5 size-5 shrink-0 text-accent" aria-hidden />
        <div className="min-w-0">
          <p className="font-semibold">Exportar meus dados</p>
          <p className="text-sm text-muted">Baixe um arquivo JSON com seus hábitos, check-ins, ciclos, objetivos e XP.</p>
        </div>
      </div>
      <Button variant="secondary" loading={pending} disabled={!online} onClick={() => void download()}>
        {online ? 'Exportar dados' : 'Exportar dados (precisa de internet)'}
      </Button>
    </Card>
  )
}
