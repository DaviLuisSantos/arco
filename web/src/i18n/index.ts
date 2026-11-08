import i18n from 'i18next'
import { initReactI18next, useTranslation } from 'react-i18next'

export type Locale = 'pt-BR' | 'en'
export const LOCALES: Locale[] = ['pt-BR', 'en']
export const DEFAULT_LOCALE: Locale = 'pt-BR'
const STORAGE_KEY = 'arco-locale'

type Module = { default: Record<string, unknown> }

function toResources(files: Record<string, Module>): Record<string, Record<string, unknown>> {
  const out: Record<string, Record<string, unknown>> = {}
  for (const [path, mod] of Object.entries(files)) {
    out[path.split('/').pop()!.replace(/\.ts$/, '')] = mod.default
  }
  return out
}

export const resources = {
  'pt-BR': toResources(import.meta.glob<Module>('./locales/pt-BR/*.ts', { eager: true })),
  en: toResources(import.meta.glob<Module>('./locales/en/*.ts', { eager: true })),
}

export function normalizeLocale(value: string | null | undefined): Locale | null {
  if (!value) return null
  const v = value.toLowerCase()
  if (v.startsWith('pt')) return 'pt-BR'
  if (v.startsWith('en')) return 'en'
  return null
}

function storedLocale(): Locale | null {
  try {
    return normalizeLocale(localStorage.getItem(STORAGE_KEY))
  } catch {
    return null
  }
}

function browserLocale(): Locale {
  const nav = typeof navigator !== 'undefined' ? navigator.language : ''
  return nav.toLowerCase().startsWith('pt') ? 'pt-BR' : 'en'
}

/** Detecção: localStorage → navegador. (`me.locale` entra via `applyUserLocale` quando há sessão.) */
export function detectLocale(): Locale {
  return storedLocale() ?? browserLocale()
}

void i18n.use(initReactI18next).init({
  resources,
  lng: detectLocale(),
  fallbackLng: DEFAULT_LOCALE,
  defaultNS: 'common',
  ns: Object.keys(resources[DEFAULT_LOCALE]),
  interpolation: { escapeValue: false },
})

function syncDocument(locale: string) {
  if (typeof document !== 'undefined') document.documentElement.lang = locale
}
syncDocument(i18n.language)
i18n.on('languageChanged', syncDocument)

/** Idioma atual (uso fora de componentes). */
export function currentLocale(): Locale {
  return normalizeLocale(i18n.language) ?? DEFAULT_LOCALE
}

/** Aplica o idioma salvo no perfil (`me.locale`), a menos que o usuário já tenha escolhido um neste aparelho. */
export function applyUserLocale(meLocale: string | null | undefined) {
  if (storedLocale()) return
  const l = normalizeLocale(meLocale)
  if (l && l !== currentLocale()) void i18n.changeLanguage(l)
}

/** Troca o idioma, grava localmente e, havendo sessão, no servidor (`PUT /api/auth/locale`). */
export async function setLocale(locale: Locale, signedIn = true): Promise<void> {
  try {
    localStorage.setItem(STORAGE_KEY, locale)
  } catch {
    /* sem storage: vale só nesta sessão */
  }
  await i18n.changeLanguage(locale)
  if (!signedIn) return
  try {
    await fetch('/api/auth/locale', {
      method: 'PUT',
      credentials: 'same-origin',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ locale }),
    })
  } catch {
    /* offline: o idioma local já vale */
  }
}

/** Hook: idioma atual (reativo) e `setLocale`. */
export function useLocale() {
  const { i18n: instance } = useTranslation()
  return { locale: normalizeLocale(instance.language) ?? DEFAULT_LOCALE, setLocale }
}

/**
 * Namespaces tipados (a fonte é o pt-BR). Um namespace novo pode se registrar no próprio arquivo pt-BR,
 * sem editar este:
 *   const ns = { ... }; export default ns
 *   declare module '../../index' { interface ArcoResources { meuNs: typeof ns } }
 */
export interface ArcoResources {
  common: (typeof import('./locales/pt-BR/common'))['default']
  errors: (typeof import('./locales/pt-BR/errors'))['default']
  billing: (typeof import('./locales/pt-BR/billing'))['default']
  publicProfile: (typeof import('./locales/pt-BR/publicProfile'))['default']
  security: (typeof import('./locales/pt-BR/security'))['default']
  onboarding: (typeof import('./locales/pt-BR/onboarding'))['default']
  partners: (typeof import('./locales/pt-BR/partners'))['default']
  admin: (typeof import('./locales/pt-BR/admin'))['default']
}

declare module 'i18next' {
  interface CustomTypeOptions {
    defaultNS: 'common'
    resources: ArcoResources
  }
}

export default i18n
