import { test, type Page } from '@playwright/test'
import { resolve } from 'node:path'
import { pinLocale, startDemo } from './helpers'

// Gera docs/images/*.png (rode com `npm run screenshots`). Usa a conta demo, que já vem com dados.
const OUT = resolve(import.meta.dirname, '..', '..', 'docs', 'images')

const SIZES = [
  { name: 'mobile', width: 390, height: 844 },
  { name: 'desktop', width: 1280, height: 800 },
]

async function shot(page: Page, name: string, size: string) {
  await page.waitForLoadState('networkidle')
  await page.screenshot({ path: resolve(OUT, `${name}-${size}.png`) })
}

for (const size of SIZES) {
  test.describe(size.name, () => {
    test.use({ viewport: { width: size.width, height: size.height } })

    test('landing', async ({ page }) => {
      await pinLocale(page)
      await page.goto('/landing/')
      await shot(page, 'landing', size.name)
    })

    test('app (demo)', async ({ page }) => {
      await pinLocale(page)
      await startDemo(page)
      await shot(page, 'hoje', size.name)

      await page.goto('/cycle')
      await page.getByRole('heading', { level: 1 }).waitFor()
      await shot(page, 'ciclo', size.name)

      await page.goto('/')
      await page.locator('article a[href^="/habits/"]').first().click()
      await page.getByRole('heading', { level: 1 }).waitFor()
      await shot(page, 'habito', size.name)

      await page.goto('/profile')
      await page.getByRole('heading', { level: 1 }).waitFor()
      await shot(page, 'perfil', size.name)
    })
  })
}
