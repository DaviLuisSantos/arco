// Datas de calendário ("YYYY-MM-DD") são tratadas como dias puros, em UTC,
// para nunca sofrer deslocamento de fuso. Nunca use new Date("YYYY-MM-DD").

const LOCALE = 'pt-BR'

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
  return new Intl.DateTimeFormat(LOCALE, { ...options, timeZone: 'UTC' }).format(toUtc(date))
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
  return new Intl.DateTimeFormat(LOCALE, options).format(new Date(value))
}

export const WEEKDAY_INITIALS = ['D', 'S', 'T', 'Q', 'Q', 'S', 'S']
export const WEEKDAY_NAMES = ['Domingo', 'Segunda', 'Terça', 'Quarta', 'Quinta', 'Sexta', 'Sábado']
export const WEEKDAY_SHORT = ['dom', 'seg', 'ter', 'qua', 'qui', 'sex', 'sáb']

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

/** "outubro de 2026". */
export function formatMonth(date: string): string {
  return formatDate(monthStart(date), { month: 'long', year: 'numeric' })
}

/** Data local (fuso do navegador) de um instante ISO, como "YYYY-MM-DD". */
export function localDateOf(instant: string): string {
  const d = new Date(instant)
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
}
