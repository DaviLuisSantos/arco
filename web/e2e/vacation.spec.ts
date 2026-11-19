import { expect, test } from '@playwright/test'
import { createHabit, gotoProfile, pinLocale, register } from './helpers'

test('modo férias: ativa pelo Perfil, pausa o hábito e encerra', async ({ page }) => {
  await pinLocale(page)
  await register(page)
  await createHabit(page, 'Ler')

  await gotoProfile(page)
  await page.getByRole('button', { name: 'Ativar modo férias' }).click()
  await expect(page.getByText(/Em férias até/)).toBeVisible()

  // O hábito saiu do dia: a API do Hoje o marca como pausado.
  const today = await (await page.request.get('/api/today')).json()
  const habit = (today.habits as { name: string; isPaused: boolean; isDueToday: boolean }[]).find((h) => h.name === 'Ler')
  expect(habit?.isPaused).toBe(true)
  expect(habit?.isDueToday).toBe(false)

  await page.getByRole('button', { name: 'Encerrar férias' }).click()
  await page.getByRole('dialog', { name: 'Encerrar as férias?' }).getByRole('button', { name: 'Encerrar férias' }).click()
  await expect(page.getByRole('button', { name: 'Ativar modo férias' })).toBeVisible()

  const after = await (await page.request.get('/api/today')).json()
  const resumed = (after.habits as { name: string; isPaused: boolean }[]).find((h) => h.name === 'Ler')
  expect(resumed?.isPaused).toBe(false)
})
