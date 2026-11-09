// Instalação do PWA. O Chrome/Edge disparam `beforeinstallprompt` uma vez, cedo: capturamos no boot
// (main.tsx) e guardamos o evento para o cartão de instalação usar quando fizer sentido.

interface BeforeInstallPromptEvent extends Event {
  prompt: () => Promise<void>
  userChoice: Promise<{ outcome: 'accepted' | 'dismissed' }>
}

let deferred: BeforeInstallPromptEvent | null = null
const listeners = new Set<() => void>()

function emit() {
  listeners.forEach((l) => l())
}

/** Chamado uma vez no boot, antes da primeira pintura. */
export function initPwaInstall() {
  window.addEventListener('beforeinstallprompt', (event) => {
    event.preventDefault() // some o mini-infobar: o convite é nosso
    deferred = event as BeforeInstallPromptEvent
    emit()
  })
  window.addEventListener('appinstalled', () => {
    deferred = null
    emit()
  })
}

/** Para `useSyncExternalStore`. */
export function subscribePwaInstall(listener: () => void) {
  listeners.add(listener)
  return () => {
    listeners.delete(listener)
  }
}

/** Verdadeiro quando o navegador ofereceu a instalação nativa. */
export function canPromptInstall(): boolean {
  return deferred !== null
}

export async function promptInstall(): Promise<'accepted' | 'dismissed' | 'unavailable'> {
  if (!deferred) return 'unavailable'
  const event = deferred
  deferred = null
  emit()
  await event.prompt()
  return (await event.userChoice).outcome
}
