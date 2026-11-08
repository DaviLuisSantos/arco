import { expect, request, test } from '@playwright/test'
import { mkdirSync, writeFileSync } from 'node:fs'
import { resolve } from 'node:path'

// Página pública /u/{slug} e sua imagem OG. O preview do Vite não repassa /u/..., então tudo vai
// direto à API (E2E_API_PORT). As imagens para revisão ficam em test-results/public-profile (não versionado).
const API = `http://localhost:${process.env.E2E_API_PORT ?? 5199}`
const OUT = resolve(import.meta.dirname, '..', 'test-results', 'public-profile')
const THEMES = ['indigo', 'emerald', 'amber', 'rose', 'sky'] as const

const LOCALES = [
  { locale: 'pt-BR', scheme: 'light' as const, cycle: 'Ciclo', cta: 'Criar meu Arco' },
  { locale: 'en', scheme: 'dark' as const, cycle: 'Cycle', cta: 'Create my Arco' },
]

for (const { locale, scheme, cycle, cta } of LOCALES) {
  test(`perfil público (${locale})`, async ({ browser }) => {
    mkdirSync(OUT, { recursive: true })
    const tag = locale === 'en' ? 'en' : 'pt'

    // Conta demo (Pro, com ciclo, hábitos, check-ins e sessões de foco) no idioma pedido.
    const api = await request.newContext({ baseURL: API, extraHTTPHeaders: { 'Accept-Language': locale } })
    expect((await api.post('/api/auth/demo')).ok()).toBeTruthy()
    const slug = `e2e-${tag}-${Date.now().toString(36)}`
    const enable = await api.put('/api/profile/public', { data: { enabled: true, slug, showHabits: true } })
    expect(enable.ok()).toBeTruthy()

    const context = await browser.newContext({ colorScheme: scheme, locale })
    const page = await context.newPage()

    for (const theme of THEMES) {
      expect((await api.put('/api/profile/public', { data: { theme } })).ok()).toBeTruthy()

      // OG: PNG 1200×630 gerado no servidor, um por tema.
      const og = await context.request.get(`${API}/u/${slug}/og.png`)
      expect(og.status()).toBe(200)
      expect(og.headers()['content-type']).toBe('image/png')
      const png = await og.body()
      expect(png.readUInt32BE(16)).toBe(1200)
      expect(png.readUInt32BE(20)).toBe(630)
      writeFileSync(resolve(OUT, `og-${tag}-${theme}.png`), png)

      for (const size of theme === 'indigo' ? [390, 1280] : [390]) {
        await page.setViewportSize({ width: size, height: size === 390 ? 844 : 900 })
        await page.goto(`${API}/u/${slug}?v=${theme}-${size}`) // a página é cacheada 5 min: evita ver o tema anterior
        await expect(page.getByRole('heading', { level: 1 })).toBeVisible()
        await expect(page.getByRole('heading', { name: new RegExp(`^${cycle}:`) })).toBeVisible()
        await expect(page.getByRole('link', { name: cta })).toBeVisible()
        await expect(page.locator('svg.arch path')).toHaveCount(6)

        // Sem rolagem horizontal nem texto vazando do viewport.
        const overflow = await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth)
        expect(overflow).toBeLessThanOrEqual(0)

        await page.screenshot({ path: resolve(OUT, `page-${tag}-${theme}-${size}.png`), fullPage: true })
      }
    }

    await context.close()
    await api.dispose()
  })
}
