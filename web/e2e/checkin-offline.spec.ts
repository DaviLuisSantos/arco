import { expect, test, type Page } from '@playwright/test'
import { createHabit, PASSWORD, pinLocale, reachToday, uniqueEmail } from './helpers'

// Check-in feito sem rede: a mutação pausa, o Desfazer entra na mesma fila e tudo é enviado, na ordem, ao religar.
// Usa `setOffline` (e não `route.abort`) para exercitar o caminho real do onlineManager do TanStack Query.

const HABIT = 'Caminhar offline'

interface ApiCheckIn {
  status: 'Done' | 'Minimum' | 'Missed'
  minutes: number | null
}

async function setup(page: Page) {
  await pinLocale(page)
  // Conta e hábito criados pela API antes de abrir o app: o cache persistido (staleTime 30 s) não pode
  // guardar uma lista vazia de hábitos.
  const reg = await page.request.post('/api/auth/register', {
    data: { email: uniqueEmail('offline'), password: PASSWORD, displayName: 'Pessoa E2E', locale: 'pt-BR' },
  })
  expect(reg.ok(), await reg.text()).toBeTruthy()
  await createHabit(page, HABIT)
  await page.goto('/')
  await reachToday(page)
  const group = page.getByRole('group', { name: `Check-in de ${HABIT}` })
  await expect(group).toBeVisible()
  const today = (await (await page.request.get('/api/today')).json()) as { date: string }
  const habits = (await (await page.request.get('/api/habits')).json()) as Array<{ id: number; name: string }>
  const habit = habits.find((h) => h.name === HABIT)
  expect(habit).toBeTruthy()
  return { group, date: today.date, habitId: habit!.id }
}

async function totalXp(page: Page) {
  const res = await page.request.get('/api/player')
  expect(res.ok()).toBeTruthy()
  return ((await res.json()) as { totalXp: number }).totalXp
}

async function checkInsOf(page: Page, habitId: number, date: string) {
  const res = await page.request.get(`/api/checkins?from=${date}&to=${date}&habitId=${habitId}`)
  expect(res.ok()).toBeTruthy()
  return (await res.json()) as ApiCheckIn[]
}

/** Registra, antes de religar, a espera por `count` respostas de PUT/DELETE em /api/checkins. */
function expectCheckInResponses(page: Page, count: number) {
  let seen = 0
  return page.waitForResponse((r) => {
    const m = r.request().method()
    if (!r.url().includes('/api/checkins') || (m !== 'PUT' && m !== 'DELETE')) return false
    seen += 1
    return seen >= count
  })
}

test.describe('check-in offline', () => {
  test('marcar offline e religar grava um único registro', async ({ page }) => {
    const { group, date, habitId } = await setup(page)
    const xpBefore = await totalXp(page)

    await page.context().setOffline(true)
    await group.getByRole('button', { name: 'Feito' }).click()
    await expect(group.getByRole('button', { name: 'Feito' })).toHaveAttribute('aria-pressed', 'true')

    const done = expectCheckInResponses(page, 1)
    await page.context().setOffline(false)
    const res = await done
    expect(res.ok()).toBeTruthy()

    await expect(group.getByRole('button', { name: 'Feito' })).toHaveAttribute('aria-pressed', 'true')
    const list = await checkInsOf(page, habitId, date)
    expect(list).toHaveLength(1)
    expect(list[0].status).toBe('Done')
    expect(await totalXp(page)).toBeGreaterThan(xpBefore)
  })

  test('marcar e desfazer offline não deixa registro nem XP', async ({ page }) => {
    const { group, date, habitId } = await setup(page)
    const xpBefore = await totalXp(page)

    await page.context().setOffline(true)
    await group.getByRole('button', { name: 'Feito' }).click()
    await expect(group.getByRole('button', { name: 'Feito' })).toHaveAttribute('aria-pressed', 'true')
    await page.getByRole('button', { name: 'Desfazer' }).click()
    await expect(group.getByRole('button', { name: 'Feito' })).toHaveAttribute('aria-pressed', 'false')

    // A fila tem 2 envios (PUT e DELETE), na ordem: espera o último.
    const done = expectCheckInResponses(page, 2)
    await page.context().setOffline(false)
    await done

    for (const name of ['Feito', 'Mínimo', 'Não fiz']) {
      await expect(group.getByRole('button', { name })).toHaveAttribute('aria-pressed', 'false')
    }
    expect(await checkInsOf(page, habitId, date)).toHaveLength(0)
    expect(await totalXp(page)).toBe(xpBefore)
  })

  test('minutos digitados offline sobrevivem à troca para Mínimo', async ({ page }) => {
    const { group, date, habitId } = await setup(page)

    await page.context().setOffline(true)
    await group.getByRole('button', { name: 'Feito' }).click()
    await expect(group.getByRole('button', { name: 'Feito' })).toHaveAttribute('aria-pressed', 'true')

    const minutes = page.getByLabel('Quantos minutos?')
    await minutes.fill('25')
    // Clique fora (elemento não focável) dispara o blur que salva os minutos.
    await page.getByRole('heading', { name: 'Hoje', level: 1 }).click()
    await expect(minutes).toHaveValue('25')

    await group.getByRole('button', { name: 'Mínimo' }).click()
    await expect(group.getByRole('button', { name: 'Mínimo' })).toHaveAttribute('aria-pressed', 'true')
    await expect(minutes).toHaveValue('25')

    // Fila: Feito, minutos, Mínimo.
    const done = expectCheckInResponses(page, 3)
    await page.context().setOffline(false)
    await done

    await expect(group.getByRole('button', { name: 'Mínimo' })).toHaveAttribute('aria-pressed', 'true')
    const list = await checkInsOf(page, habitId, date)
    expect(list).toHaveLength(1)
    expect(list[0]).toMatchObject({ status: 'Minimum', minutes: 25 })
  })
})
