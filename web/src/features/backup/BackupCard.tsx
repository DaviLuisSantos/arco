import { Download } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { exportApi } from '../../api/endpoints'
import { Button } from '../../components/ui/Button'
import { Card } from '../../components/ui/Card'
import { useToast } from '../../components/ui/toast-context'
import { errorMessage } from '../../lib/errors'
import { useOnline } from '../../lib/useOnline'

/** Exporta todos os dados do usuário em um arquivo JSON. */
export function BackupCard() {
  const { t } = useTranslation('backup')
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
          <p className="font-semibold">{t('title')}</p>
          <p className="text-sm text-muted">{t('description')}</p>
        </div>
      </div>
      <Button variant="secondary" loading={pending} disabled={!online} onClick={() => void download()}>
        {online ? t('export') : t('exportOffline')}
      </Button>
    </Card>
  )
}
