import { expect, test, type Page } from '@playwright/test'
import { PASSWORD, pinLocale, reachToday, uniqueEmail } from './helpers'

// Hábito quantitativo: o status vem do total do dia (4 = Mínimo, 8 = Feito). Online e offline (fila do check-in).

const HABIT = 'Beber água'

interface ApiCheckIn {
  status: 'Done' | 'Minimum' | 'Missed' | 'Skipped'
}

async function setup(page: Page) {
  await pinLocale(page)
  const reg = await page.request.post('/api/auth/register', {
    data: { email: uniqueEmail('quantity'), password: PASSWORD, displayName: 'Pessoa E2E', locale: 'pt-BR' },
  })
  expect(reg.ok(), await reg.text()).toBeTruthy()
  const created = await page.request.post('/api/habits', {
    data: { name: HABIT, frequencyType: 'Daily', unit: 'copos', levelTarget: 8, levelMinimumTarget: 4 },
  })
  expect(created.ok(), await created.text()).toBeTruthy()
  const habitId = ((await created.json()) as { id: number }).id
  const today = (await (await page.request.get('/api/today')).json()) as { date: string }

  await page.goto('/')
  await reachToday(page)
  const card = page.locator('article', { hasText: HABIT })
  await expect(card.getByTestId('qty-progress')).toHaveText('0 / 8 copos')
  return { card, habitId, date: today.date }
}

async function checkInsOf(page: Page, habitId: number, date: string) {
  const res = await page.request.get(`/api/checkins?from=${date}&to=${date}&habitId=${habitId}`)
  expect(res.ok()).toBeTruthy()
  return (await res.json()) as ApiCheckIn[]
}

async function totalOf(page: Page, habitId: number, date: string) {
  const res = await page.request.get(`/api/quantities?from=${date}&to=${date}&habitId=${habitId}`)
  expect(res.ok()).toBeTruthy()
  const rows = (await res.json()) as Array<{ total: number }>
  return rows[0]?.total ?? 0
}

/** Registra, antes de religar, a espera por `count` respostas de PUT em /api/quantities. */
function expectQuantityResponses(page: Page, count: number) {
  let seen = 0
  return page.waitForResponse((r) => {
    if (!r.url().includes('/api/quantities') || r.request().method() !== 'PUT') return false
    seen += 1
    return seen >= count
  })
}

test.describe('hábito quantitativo', () => {
  test('+1 até o mínimo, + valor até a meta e corrigir para baixo', async ({ page }) => {
    const { card, habitId, date } = await setup(page)
    const progress = card.getByTestId('qty-progress')
    const badge = (name: string) => card.getByText(name, { exact: true })

    // Feito/Mínimo não são botões: o status vem do total.
    await expect(card.getByRole('button', { name: 'Feito', exact: true })).toHaveCount(0)
    await expect(badge('Pendente')).toBeVisible()

    for (let n = 1; n <= 3; n++) {
      await card.getByRole('button', { name: '+1', exact: true }).click()
      await expect(progress).toHaveText(`${n} / 8 copos`)
    }
    await expect(badge('Pendente')).toBeVisible()
    expect(await checkInsOf(page, habitId, date)).toHaveLength(0)

    await card.getByRole('button', { name: '+1', exact: true }).click()
    await expect(progress).toHaveText('4 / 8 copos')
    await expect(badge('Mínimo')).toBeVisible()
    await expect.poll(async () => (await checkInsOf(page, habitId, date))[0]?.status).toBe('Minimum')

    // + valor soma ao total do dia, com vírgula decimal.
    await card.getByRole('button', { name: '+ valor', exact: true }).click()
    await card.getByLabel('Quanto somar?').fill('4')
    await card.getByRole('button', { name: 'Somar', exact: true }).click()
    await expect(progress).toHaveText('8 / 8 copos')
    await expect(badge('Feito')).toBeVisible()
    await expect.poll(async () => (await checkInsOf(page, habitId, date))[0]?.status).toBe('Done')
    expect(await totalOf(page, habitId, date)).toBe(8)

    // Corrigir o total para baixo do mínimo remove o check-in derivado.
    await card.getByRole('button', { name: 'Corrigir total', exact: true }).click()
    await card.getByLabel('Total do dia').fill('2,5')
    await card.getByRole('button', { name: 'Salvar total', exact: true }).click()
    await expect(progress).toHaveText('2,5 / 8 copos')
    await expect(badge('Pendente')).toBeVisible()
    await expect.poll(async () => (await checkInsOf(page, habitId, date)).length).toBe(0)
    expect(await totalOf(page, habitId, date)).toBe(2.5)
  })

  test('valor inválido não envia', async ({ page }) => {
    const { card } = await setup(page)
    await card.getByRole('button', { name: '+ valor', exact: true }).click()
    await card.getByLabel('Quanto somar?').fill('1,25')
    await card.getByRole('button', { name: 'Somar', exact: true }).click()
    await expect(card.getByRole('alert')).toBeVisible()
    await expect(card.getByTestId('qty-progress')).toHaveText('0 / 8 copos')
  })

  test('registrar offline entra na fila e vira Mínimo ao religar', async ({ page }) => {
    const { card, habitId, date } = await setup(page)
    const progress = card.getByTestId('qty-progress')

    await page.context().setOffline(true)
    for (let n = 1; n <= 4; n++) {
      await card.getByRole('button', { name: '+1', exact: true }).click()
      await expect(progress).toHaveText(`${n} / 8 copos`)
    }
    // O card deriva o status na hora, sem servidor.
    await expect(card.getByText('Mínimo', { exact: true })).toBeVisible()

    const done = expectQuantityResponses(page, 4)
    await page.context().setOffline(false)
    const res = await done
    expect(res.ok()).toBeTruthy()

    await expect(progress).toHaveText('4 / 8 copos')
    expect(await totalOf(page, habitId, date)).toBe(4)
    const list = await checkInsOf(page, habitId, date)
    expect(list).toHaveLength(1)
    expect(list[0].status).toBe('Minimum')
  })
})
