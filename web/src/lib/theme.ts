export type Theme = 'system' | 'light' | 'dark'

const KEY = 'arco-theme'
const COLORS = { light: '#f5f6fa', dark: '#0f1117' }

export function getTheme(): Theme {
  try {
    const v = localStorage.getItem(KEY)
    if (v === 'light' || v === 'dark') return v
  } catch {
    // armazenamento indisponível: segue o sistema
  }
  return 'system'
}

function applyThemeColor(theme: Theme) {
  const metas = document.querySelectorAll<HTMLMetaElement>('meta[name="theme-color"]')
  metas.forEach((m, i) => {
    if (theme === 'system') {
      m.content = i === 0 ? COLORS.dark : COLORS.light
      m.media = i === 0 ? '(prefers-color-scheme: dark)' : '(prefers-color-scheme: light)'
    } else {
      m.content = COLORS[theme]
      m.removeAttribute('media')
    }
  })
}

export function setTheme(theme: Theme) {
  try {
    if (theme === 'system') localStorage.removeItem(KEY)
    else localStorage.setItem(KEY, theme)
  } catch {
    // sem persistência: vale só nesta sessão
  }
  const root = document.documentElement
  if (theme === 'system') root.removeAttribute('data-theme')
  else root.setAttribute('data-theme', theme)
  applyThemeColor(theme)
}
