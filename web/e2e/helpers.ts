import { expect, type Page } from '@playwright/test'

export const PASSWORD = 'senha-e2e-123'

/** Os testes rodam em pt-BR: fixa o idioma antes de qualquer script da página. */
export async function pinLocale(page: Page) {
  await page.addInitScript(() => {
    try {
      localStorage.setItem('arco-locale', 'pt-BR')
    } catch {
      // sem storage: o navegador já abre em pt-BR
    }
  })
}

export function uniqueEmail(prefix = 'e2e') {
  return `${prefix}-${Date.now()}-${Math.random().toString(36).slice(2, 8)}@example.com`
}

/** Espera o app (título "Hoje"). O "Pular" do onboarding avança um passo por clique: repete até sair. */
export async function reachToday(page: Page) {
  const skip = page.getByTestId('onboarding-skip')
  const hoje = page.getByRole('heading', { name: 'Hoje', level: 1 })
  for (let i = 0; i < 6; i++) {
    await skip.or(hoje).first().waitFor()
    if (await hoje.isVisible()) break
    // No último passo o botão fica desativado enquanto o onboarding conclui e some da tela.
    await skip.click({ timeout: 3_000 }).catch(() => {})
  }
  await expect(hoje).toBeVisible()
}

export async function register(page: Page, email = uniqueEmail()) {
  await page.goto('/register')
  await page.getByLabel('Nome (opcional)').fill('Pessoa E2E')
  await page.getByLabel('E-mail').fill(email)
  await page.getByLabel('Senha').fill(PASSWORD)
  await page.getByRole('button', { name: 'Criar conta' }).click()
  await reachToday(page)
  return email
}

export async function startDemo(page: Page) {
  const res = await page.request.post('/api/auth/demo')
  expect(res.ok()).toBeTruthy()
  await page.goto('/')
  await reachToday(page)
}
