import { Trophy } from 'lucide-react'
import { useMemo, useState, type ReactNode } from 'react'
import { Button } from '../../components/ui/Button'
import { Dialog } from '../../components/ui/Dialog'
import { XpFeedbackContext, type XpFeedbackApi } from './xp-context'

export function XpFeedbackProvider({ children }: { children: ReactNode }) {
  const [level, setLevel] = useState<number | null>(null)
  const api = useMemo<XpFeedbackApi>(() => ({ celebrateLevel: setLevel }), [])
  const close = () => setLevel(null)

  return (
    <XpFeedbackContext.Provider value={api}>
      {children}
      <Dialog
        open={level !== null}
        onClose={close}
        title="Subiu de nível!"
        footer={
          <Button block size="lg" onClick={close}>
            Continuar
          </Button>
        }
      >
        <div className="flex flex-col items-center gap-3 py-6 text-center">
          <div className="grid size-24 animate-pop place-items-center rounded-full bg-gradient-to-br from-accent-strong to-xp shadow-lg">
            <Trophy className="size-11 text-bg" aria-hidden />
          </div>
          <p className="text-4xl font-extrabold tracking-tight">Nível {level}!</p>
          <p className="max-w-xs text-muted">Cada check-in conta. Continue puxando o arco.</p>
        </div>
      </Dialog>
    </XpFeedbackContext.Provider>
  )
}
