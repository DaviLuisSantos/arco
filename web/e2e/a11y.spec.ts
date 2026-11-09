import AxeBuilder from '@axe-core/playwright'
import { expect, test } from '@playwright/test'
import { pinLocale, register } from './helpers'

// Telas principais com o axe (WCAG 2.x A/AA). Só violações críticas e graves reprovam;
// as demais saem no log para acompanhamento.
const ROTAS = ['/', '/habits', '/progress', '/settings', '/profile']

test('axe: telas principais sem violações críticas', async ({ page }) => {
  await pinLocale(page)
  await register(page)

  const achados: string[] = []
  for (const rota of ROTAS) {
    await page.goto(rota)
    await page.waitForLoadState('networkidle')
    const { violations } = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'])
      .analyze()
    for (const v of violations) {
      const linha = `${rota} [${v.impact}] ${v.id}: ${v.nodes.map((n) => n.target.join(' ')).slice(0, 3).join(' | ')}`
      console.log(linha)
      if (v.impact === 'critical' || v.impact === 'serious') achados.push(linha)
    }
  }
  expect(achados, achados.join('\n')).toEqual([])
})
