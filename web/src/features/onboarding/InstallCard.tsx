import { Download } from 'lucide-react'
import { useState, useSyncExternalStore } from 'react'
import { useTranslation } from 'react-i18next'
import { Button } from '../../components/ui/Button'
import { Card } from '../../components/ui/Card'
import { isIos, isStandalone } from '../../lib/push'
import { canPromptInstall, promptInstall, subscribePwaInstall } from '../../lib/pwaInstall'

const DISMISSED_KEY = 'arco-install-dismissed'

function readDismissed(): boolean {
  try {
    return localStorage.getItem(DISMISSED_KEY) === '1'
  } catch {
    return false
  }
}

/**
 * Convite para instalar o app: botão nativo onde o navegador oferece (Chrome/Edge/Android) e, no iOS,
 * a instrução manual (Compartilhar → Adicionar à Tela de Início), que também é o que habilita o push lá.
 */
export function InstallCard() {
  const { t } = useTranslation('onboarding', { keyPrefix: 'install' })
  const canPrompt = useSyncExternalStore(subscribePwaInstall, canPromptInstall)
  const [dismissed, setDismissed] = useState(readDismissed)

  const ios = isIos()
  if (dismissed || isStandalone() || !(canPrompt || ios)) return null

  function dismiss() {
    try {
      localStorage.setItem(DISMISSED_KEY, '1')
    } catch {
      // sem storage: some só nesta sessão
    }
    setDismissed(true)
  }

  async function install() {
    const outcome = await promptInstall()
    if (outcome !== 'unavailable') dismiss()
  }

  return (
    <Card className="flex flex-col gap-3 p-4!">
      <div className="flex items-start gap-3">
        <span className="grid size-10 shrink-0 place-items-center rounded-xl bg-surface-2 text-accent">
          <Download className="size-5" aria-hidden />
        </span>
        <div>
          <h2 className="text-sm font-semibold">{t('title')}</h2>
          <p className="mt-1 text-sm text-muted">{canPrompt ? t('description') : t('descriptionIos')}</p>
        </div>
      </div>
      <div className="flex justify-end gap-2">
        <Button variant="ghost" size="sm" onClick={dismiss}>
          {t('later')}
        </Button>
        {canPrompt && (
          <Button size="sm" onClick={() => void install()}>
            {t('action')}
          </Button>
        )}
      </div>
    </Card>
  )
}
