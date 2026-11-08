// Datas de calendário ("YYYY-MM-DD") são tratadas como dias puros, em UTC,
// para nunca sofrer deslocamento de fuso. Nunca use new Date("YYYY-MM-DD").

import i18n, { currentLocale } from '../i18n'

function toUtc(date: string): Date {
  const [y, m, d] = date.split('-').map(Number)
  return new Date(Date.UTC(y, m - 1, d))
}

function fromUtc(d: Date): string {
  return d.toISOString().slice(0, 10)
}

export function isDateOnly(value: string): boolean {
  return /^\d{4}-\d{2}-\d{2}$/.test(value)
}

export function addDays(date: string, days: number): string {
  const d = toUtc(date)
  d.setUTCDate(d.getUTCDate() + days)
  return fromUtc(d)
}

/** Diferença em dias (b − a). */
export function diffDays(a: string, b: string): number {
  return Math.round((toUtc(b).getTime() - toUtc(a).getTime()) / 86_400_000)
}

/** 0 = domingo … 6 = sábado. */
export function dayOfWeek(date: string): number {
  return toUtc(date).getUTCDay()
}

/** Hoje no fuso do navegador (só como fallback; o servidor manda o "hoje" oficial). */
export function localToday(): string {
  const now = new Date()
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`
}

export function formatDate(
  date: string,
  options: Intl.DateTimeFormatOptions = { day: 'numeric', month: 'short' },
): string {
  return new Intl.DateTimeFormat(currentLocale(), { ...options, timeZone: 'UTC' }).format(toUtc(date))
}

export function formatLongDate(date: string): string {
  return formatDate(date, { weekday: 'long', day: 'numeric', month: 'long' })
}

export function formatFullDate(date: string): string {
  return formatDate(date, { day: 'numeric', month: 'short', year: 'numeric' })
}

/** Formata um instante ISO (ou uma data de calendário) como data curta. */
export function formatInstant(
  value: string,
  options: Intl.DateTimeFormatOptions = { day: 'numeric', month: 'short', year: 'numeric' },
): string {
  if (isDateOnly(value)) return formatDate(value, options)
  return new Intl.DateTimeFormat(currentLocale(), options).format(new Date(value))
}

const DAY_KEYS = ['d0', 'd1', 'd2', 'd3', 'd4', 'd5', 'd6'] as const

/** Array que lê o idioma atual a cada acesso (as constantes são usadas fora de componentes). */
function lazyList(build: () => string[]): string[] {
  // Getters por índice: o array tem os 7 itens de verdade (map/forEach/spread funcionam) e o texto vem do idioma atual.
  const list: string[] = []
  DAY_KEYS.forEach((_, i) => Object.defineProperty(list, i, { enumerable: true, get: () => build()[i] }))
  return list
}

export const WEEKDAY_INITIALS = lazyList(() => DAY_KEYS.map((k) => i18n.t(`lib:weekdayInitials.${k}`)))
export const WEEKDAY_NAMES = lazyList(() => DAY_KEYS.map((k) => i18n.t(`lib:weekdayNames.${k}`)))
export const WEEKDAY_SHORT = lazyList(() => DAY_KEYS.map((k) => i18n.t(`lib:weekdayShort.${k}`)))

/** Primeiro dia do mês de uma data ("YYYY-MM-01"). */
export function monthStart(date: string): string {
  return `${date.slice(0, 7)}-01`
}

/** Primeiro dia do mês, `months` meses depois (ou antes, se negativo). */
export function addMonths(date: string, months: number): string {
  const d = toUtc(monthStart(date))
  d.setUTCMonth(d.getUTCMonth() + months)
  return fromUtc(d)
}

/** Último dia do mês de uma data. */
export function monthEnd(date: string): string {
  return addDays(addMonths(date, 1), -1)
}

/** "outubro de 2026" (ou "October 2026"). */
export function formatMonth(date: string): string {
  return formatDate(monthStart(date), { month: 'long', year: 'numeric' })
}

/** Data local (fuso do navegador) de um instante ISO, como "YYYY-MM-DD". */
export function localDateOf(instant: string): string {
  const d = new Date(instant)
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
}
