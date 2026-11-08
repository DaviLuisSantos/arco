import { expect, test } from '@playwright/test'
import { createHabit, pinLocale, register } from './helpers'

test('6º hábito no Free mostra o aviso com link para o Plano', async ({ page }) => {
  await pinLocale(page)
  await register(page)
  for (let i = 1; i <= 5; i++) await createHabit(page, `Hábito ${i}`)

  await page.goto('/habits?new=1')
  const dialog = page.getByRole('dialog', { name: 'Novo hábito' })
  await dialog.getByLabel('Nome').fill('Sexto hábito')
  await dialog.getByLabel('Nível 1 — dose').fill('20 min')
  await dialog.getByLabel('Versão mínima').fill('5 min')
  await dialog.getByRole('button', { name: 'Criar hábito' }).click()

  const notice = dialog.getByRole('alert')
  await expect(notice).toContainText('Seu plano atingiu esse limite')
  await notice.getByRole('link', { name: 'Ver plano' }).click()
  await expect(page).toHaveURL(/\/profile$/)
  await expect(page.getByText(/Plano atual/)).toBeVisible()
})
