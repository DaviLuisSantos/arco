import { expect, test } from '@playwright/test'
import { gotoProfile, pinLocale, register } from './helpers'

test('parceiro: convite criado, aceito por outra conta e resumo aberto', async ({ page, browser }) => {
  await pinLocale(page)
  await register(page)
  await gotoProfile(page)

  const card = page.getByTestId('partners-card')
  await card.getByRole('button', { name: 'Gerar convite' }).click()
  const linkText = await card.getByText(/\/invite\//).innerText()
  const path = /\/invite\/[^\s]+/.exec(linkText)?.[0]
  expect(path).toBeTruthy()
  await expect(card.getByText('Convites pendentes')).toBeVisible()

  // Outra conta aceita o convite, passando antes pelos primeiros passos.
  const guest = await browser.newContext({ baseURL: test.info().project.use.baseURL })
  const guestPage = await guest.newPage()
  await pinLocale(guestPage)
  await register(guestPage)
  await guestPage.goto(path!)
  await guestPage.getByRole('button', { name: 'Aceitar convite' }).click()
  await expect(guestPage).toHaveURL(/\/partners\/\d+$/)
  await expect(guestPage.getByRole('heading', { name: 'Resumo de Pessoa E2E', level: 1 })).toBeVisible()
  await expect(guestPage.getByRole('region', { name: 'Semana atual' })).toBeVisible()

  // O dono passa a compartilhar com o convidado (a lista na tela só atualiza após o staleTime: confere pela API).
  const res = await page.request.get('/api/partners')
  const partners = (await res.json()) as { sharingWith: unknown[]; pendingInvites: unknown[] }
  expect(partners.sharingWith).toHaveLength(1)
  expect(partners.pendingInvites).toHaveLength(0)
  await guest.close()
})
