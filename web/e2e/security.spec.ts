import { expect, test } from '@playwright/test'
import { PASSWORD, gotoProfile, gotoSettings, pinLocale, reachToday, register } from './helpers'
import { totp } from './totp'

test.beforeEach(async ({ page }) => {
  await pinLocale(page)
})

async function fillLogin(page: import('@playwright/test').Page, email: string) {
  await page.getByLabel('E-mail').fill(email)
  await page.getByLabel('Senha').fill(PASSWORD)
  await page.getByRole('button', { name: 'Entrar' }).click()
}

async function login(page: import('@playwright/test').Page, email: string) {
  await page.goto('/login')
  await fillLogin(page, email)
}

test('2FA: ativa com QR, sai, entra com código e com código de recuperação', async ({ page }) => {
  const email = await register(page)
  await gotoSettings(page)

  await page.getByRole('button', { name: 'Ativar' }).click()
  const setup = page.getByRole('dialog', { name: 'Verificação em duas etapas' })
  await expect(setup.getByRole('img', { name: /QR code/ })).toBeVisible()
  const secret = (await setup.locator('code').innerText()).trim()
  expect(secret).toMatch(/^[A-Z2-7]{16,}$/)

  await setup.getByLabel('Código de 6 dígitos').fill(totp(secret))
  await setup.getByRole('button', { name: 'Confirmar e ativar' }).click()

  const recoveryDialog = page.getByRole('dialog', { name: 'Códigos de recuperação' })
  await expect(recoveryDialog.locator('li')).toHaveCount(10)
  const codes = await recoveryDialog.locator('li').allInnerTexts()
  await recoveryDialog.getByRole('button', { name: 'Já guardei os códigos' }).click()
  await expect(page.getByText(/^Ativada\./)).toBeVisible()

  // Sai (o botão fica em Perfil) e entra com o código do app.
  await page.getByRole('link', { name: 'Voltar' }).click()
  await page.getByRole('button', { name: 'Sair' }).click()
  await expect(page).toHaveURL(/\/login$/)
  await fillLogin(page, email)
  await expect(page.getByRole('heading', { name: 'Verificação em duas etapas' })).toBeVisible()
  await page.getByLabel('Código', { exact: true }).fill(totp(secret))
  await page.getByRole('button', { name: 'Verificar' }).click()
  await reachToday(page)

  // Sai de novo e entra com um código de recuperação.
  await gotoProfile(page)
  await page.getByRole('button', { name: 'Sair' }).click()
  await expect(page).toHaveURL(/\/login$/)
  await fillLogin(page, email)
  await page.getByRole('button', { name: 'Usar código de recuperação' }).click()
  await page.getByLabel('Código de recuperação').fill(codes[0].trim())
  await page.getByRole('button', { name: 'Verificar' }).click()
  await reachToday(page)
})

test('sessões: encerra a sessão de outro dispositivo', async ({ page, browser }) => {
  const email = await register(page)

  const other = await browser.newContext({ baseURL: test.info().project.use.baseURL })
  const otherPage = await other.newPage()
  await pinLocale(otherPage)
  await login(otherPage, email)
  await reachToday(otherPage)

  await gotoSettings(page)
  const revoke = page.getByRole('button', { name: /^Encerrar sessão/ })
  await expect(revoke).toHaveCount(1)
  await revoke.click()
  await expect(revoke).toHaveCount(0)

  // A outra sessão deixa de valer.
  const me = await otherPage.request.get('/api/auth/me')
  expect(me.status()).toBe(401)
  await other.close()
})
