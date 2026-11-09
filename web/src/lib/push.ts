import { pushApi } from '../api/endpoints'

// Web Push no navegador. No iPhone/iPad só funciona com o app instalado na tela de início (iOS 16.4+).

export type PushSupport = 'supported' | 'needs-install' | 'unsupported'

export function isIos(): boolean {
  return /iPad|iPhone|iPod/.test(navigator.userAgent) || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1)
}

export function isStandalone(): boolean {
  return window.matchMedia('(display-mode: standalone)').matches || (navigator as { standalone?: boolean }).standalone === true
}

export function pushSupport(): PushSupport {
  const apis = 'serviceWorker' in navigator && 'PushManager' in window && 'Notification' in window
  if (apis) return 'supported'
  return isIos() && !isStandalone() ? 'needs-install' : 'unsupported'
}

/** Registro do service worker, ou null (ex.: modo de desenvolvimento, sem SW). */
async function registration(): Promise<ServiceWorkerRegistration | null> {
  if (!('serviceWorker' in navigator)) return null
  const reg = await navigator.serviceWorker.getRegistration()
  if (!reg) return null
  return reg.active ? reg : navigator.serviceWorker.ready
}

export async function hasServiceWorker(): Promise<boolean> {
  return (await registration()) !== null
}

export async function currentSubscription(): Promise<PushSubscription | null> {
  const reg = await registration()
  return reg ? reg.pushManager.getSubscription() : null
}

function base64UrlToBytes(value: string): Uint8Array<ArrayBuffer> {
  const base64 = (value + '='.repeat((4 - (value.length % 4)) % 4)).replace(/-/g, '+').replace(/_/g, '/')
  const raw = atob(base64)
  const bytes = new Uint8Array(new ArrayBuffer(raw.length))
  for (let i = 0; i < raw.length; i++) bytes[i] = raw.charCodeAt(i)
  return bytes
}

function sameKey(a: ArrayBuffer | null, b: Uint8Array): boolean {
  if (!a) return false
  const x = new Uint8Array(a)
  return x.length === b.length && x.every((v, i) => v === b[i])
}

export class PushPermissionError extends Error {}

/** Pede permissão, inscreve este dispositivo e registra a inscrição no servidor. */
export async function enablePush(): Promise<void> {
  const reg = await registration()
  if (!reg) throw new Error('Service worker indisponível. Use o app publicado (não o modo de desenvolvimento).')

  const permission = await Notification.requestPermission()
  if (permission !== 'granted') {
    throw new PushPermissionError('Permissão de notificações negada. Libere nas configurações do navegador.')
  }

  const key = base64UrlToBytes((await pushApi.config()).publicKey)
  let subscription = await reg.pushManager.getSubscription()
  if (subscription && !sameKey(subscription.options.applicationServerKey, key)) {
    await subscription.unsubscribe()
    subscription = null
  }
  subscription ??= await reg.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: key })
  await pushApi.subscribe(subscription.toJSON())
}

/** Cancela a inscrição deste dispositivo (no navegador e no servidor). */
export async function disablePush(): Promise<void> {
  const subscription = await currentSubscription()
  if (!subscription) return
  await pushApi.unsubscribe(subscription.endpoint)
  await subscription.unsubscribe()
}

/**
 * Vale mostrar o cartão de priming? Só com push suportado, service worker ativo, permissão ainda não
 * decidida e nenhuma inscrição neste dispositivo. O prompt do navegador nunca aparece antes do priming.
 */
export async function canPrimePush(): Promise<boolean> {
  try {
    if (pushSupport() !== 'supported' || Notification.permission !== 'default') return false
    if (!(await hasServiceWorker())) return false
    return (await currentSubscription()) === null
  } catch {
    return false
  }
}

/** Reenvia a inscrição existente ao servidor (ex.: banco recriado). Silencioso em caso de erro. */
export async function syncPushSubscription(): Promise<void> {
  try {
    if (pushSupport() !== 'supported' || Notification.permission !== 'granted') return
    const subscription = await currentSubscription()
    if (subscription) await pushApi.subscribe(subscription.toJSON())
  } catch {
    // melhor esforço
  }
}
