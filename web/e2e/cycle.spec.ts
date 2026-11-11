import { expect, test, type Page } from '@playwright/test'
import { createHabit, pinLocale, register } from './helpers'

// Página /cycle: pedras do arco acessíveis, sem overflow horizontal e uma única ação de encerrar.
// A faixa de chips de semana é aria-hidden/tabIndex=-1 (só toque): contamos as pedras por role/nome.

const WEEKS = 12

async function today(page: Page) {
  const res = await page.request.get('/api/today')
  expect(res.ok()).toBeTruthy()
  return ((await res.json()) as { date: string }).date
}

async function createCycle(page: Page, weeks: number, startDate?: string) {
  const res = await page.request.post('/api/cycles', { data: { name: 'Ciclo E2E', weeks, startDate } })
  expect(res.ok(), await res.text()).toBeTruthy()
}

function minusDays(iso: string, days: number) {
  const [y, m, d] = iso.split('-').map(Number)
  return new Date(Date.UTC(y, m - 1, d - days)).toISOString().slice(0, 10)
}

test.describe('página do ciclo', () => {
  test('pedras acessíveis, sem overflow e um só encerrar (ciclo em andamento)', async ({ page }) => {
    await pinLocale(page)
    await register(page)
    await createCycle(page, WEEKS)
    await page.goto('/cycle')
    await expect(page.getByRole('heading', { level: 1, name: 'Ciclo E2E' })).toBeVisible()

    const stones = page.getByRole('button', { name: /^Semana \d+:/ })
    await expect(stones).toHaveCount(WEEKS)
    await expect(stones.first()).toHaveAccessibleName(/^Semana 1: .*(em andamento|concluída|ainda não começou)$/)
    await expect(stones.nth(WEEKS - 1)).toHaveAccessibleName(/^Semana 12: .*ainda não começou$/)

    // Uma só ação de encerrar, e o cartão de revisão ainda não existe.
    await expect(page.getByRole('button', { name: 'Encerrar ciclo' })).toHaveCount(1)
    await expect(page.getByRole('button', { name: 'Fazer a revisão do ciclo' })).toHaveCount(0)

    const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)
    expect(overflow).toBeLessThanOrEqual(0)

    // Selecionar uma pedra futura (interior clicável) marca aria-pressed.
    await stones.nth(WEEKS - 1).click()
    await expect(stones.nth(WEEKS - 1)).toHaveAttribute('aria-pressed', 'true')
  })

  test('ciclo vencido mostra só o cartão de revisão como ação de encerrar', async ({ page }) => {
    await pinLocale(page)
    await register(page)
    await createCycle(page, 2, minusDays(await today(page), 30))
    await page.goto('/cycle')
    await expect(page.getByRole('heading', { level: 1, name: 'Ciclo E2E' })).toBeVisible()

    await expect(page.getByRole('button', { name: 'Fazer a revisão do ciclo' })).toHaveCount(1)
    await expect(page.getByRole('button', { name: 'Encerrar ciclo' })).toHaveCount(0)
    await expect(page.getByRole('button', { name: /^Semana \d+:/ })).toHaveCount(2)

    const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)
    expect(overflow).toBeLessThanOrEqual(0)
  })

  test('ciclo em andamento mostra a semana e o dia corretos', async ({ page }) => {
    await pinLocale(page)
    await register(page)
    await createHabit(page, 'Hábito E2E')
    await createCycle(page, 6, minusDays(await today(page), 21))
    const habits = (await (await page.request.get('/api/habits')).json()) as Array<{ id: number }>
    const res = await page.request.post('/api/focus-sessions', { data: { habitId: habits[0].id, minutes: 30 } })
    expect(res.ok(), await res.text()).toBeTruthy()
    await page.goto('/cycle')
    await expect(page.getByRole('heading', { level: 1, name: 'Ciclo E2E' })).toBeVisible()

    await expect(page.getByText('Dia 22 de 42')).toBeVisible()
    await expect(page.locator('svg').getByText('semana 4 de 6')).toBeVisible()

    const stone = page.getByRole('button', { name: /^Semana 4:/ })
    await expect(stone).toHaveAttribute('aria-pressed', 'true')
    await expect(stone).toHaveAccessibleName(/em andamento$/)
    await expect(page.getByRole('heading', { name: 'Semana 4' })).toBeVisible()

    const days = page.getByRole('img', { name: /Sessões de foco por dia/ })
    const box = await days.boundingBox()
    expect(box).not.toBeNull()
    if (test.info().project.name === 'e2e') expect(box!.width).toBeLessThanOrEqual(400)
  })
})
