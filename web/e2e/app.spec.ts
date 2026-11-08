import { expect, test } from '@playwright/test'
import { PASSWORD, pinLocale, reachToday, register, startDemo } from './helpers'

test.beforeEach(async ({ page }) => {
  await pinLocale(page)
})

test('cadastro → onboarding (pular) → criar hábito → check-in → XP aparece', async ({ page }) => {
  await register(page)
  await expect(page.getByText('Nenhum hábito ainda')).toBeVisible()
  await expect(page.getByText('0 XP hoje')).toBeVisible()

  await page.getByRole('link', { name: 'Criar hábito' }).click()
  const dialog = page.getByRole('dialog', { name: 'Novo hábito' })
  await dialog.getByLabel('Nome').fill('Caminhar')
  await dialog.getByLabel('Nível 1 — dose').fill('20 min')
  await dialog.getByLabel('Versão mínima').fill('5 min')
  await dialog.getByRole('button', { name: 'Criar hábito' }).click()
  await expect(dialog).toBeHidden()

  await page.getByRole('link', { name: 'Hoje' }).click()
  const checkIn = page.getByRole('group', { name: 'Check-in de Caminhar' })
  await checkIn.getByRole('button', { name: 'Feito' }).click()
  await expect(checkIn.getByRole('button', { name: 'Feito' })).toHaveAttribute('aria-pressed', 'true')
  await expect(page.getByText(/\+\d+ XP hoje/)).toBeVisible()
})

test('demo abre com dados', async ({ page }) => {
  await startDemo(page)
  await expect(page.getByText(/Conta de demonstração/)).toBeVisible()
  await expect(page.getByRole('heading', { name: 'Hábitos de hoje' })).toBeVisible()
  await expect(page.locator('article').first()).toBeVisible()
  await expect(page.getByText('Nenhum hábito ainda')).toHaveCount(0)
})

test('login e logout', async ({ page, browser }) => {
  // Cria a conta num contexto à parte, para entrar depois só com e-mail e senha.
  const setup = await browser.newContext({ baseURL: test.info().project.use.baseURL })
  const setupPage = await setup.newPage()
  await pinLocale(setupPage)
  const email = await register(setupPage)
  await setup.close()

  await page.goto('/login')
  await page.getByLabel('E-mail').fill(email)
  await page.getByLabel('Senha').fill(PASSWORD)
  await page.getByRole('button', { name: 'Entrar' }).click()
  await reachToday(page)

  await page.getByRole('link', { name: 'Perfil' }).click()
  await page.getByRole('button', { name: 'Sair' }).click()
  await expect(page).toHaveURL(/\/login$/)
  await expect(page.getByRole('button', { name: 'Entrar' })).toBeVisible()
})

test('visitante na raiz vê a landing', async ({ page }) => {
  await page.goto('/')
  await expect(page).toHaveURL(/\/landing\/?$/)
  await expect(page.getByRole('heading', { level: 1 })).toContainText('Cada dia alimenta')
})
