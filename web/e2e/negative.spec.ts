import { expect, test } from '@playwright/test'
import { PASSWORD, pinLocale, reachToday, uniqueEmail } from './helpers'

// Hábito negativo: Limpo / Recaída / Pulei, dias limpos e recorde vindos da sequência sem tolerância.

const HABIT = 'Sem refrigerante'

test('hábito negativo: Limpo, Recaída zera os dias limpos e tocar de novo desfaz', async ({ page }) => {
  await pinLocale(page)
  const reg = await page.request.post('/api/auth/register', {
    data: { email: uniqueEmail('negative'), password: PASSWORD, displayName: 'Pessoa E2E', locale: 'pt-BR' },
  })
  expect(reg.ok(), await reg.text()).toBeTruthy()
  const created = await page.request.post('/api/habits', { data: { name: HABIT, kind: 'Negative' } })
  expect(created.ok(), await created.text()).toBeTruthy()

  await page.goto('/')
  await reachToday(page)
  const card = page.locator('article', { hasText: HABIT })
  const clean = card.getByTestId('clean-days')

  // Sem Feito/Mínimo/Não fiz: só Limpo, Recaída e Pulei, e nada de minutos.
  await expect(card.getByRole('button', { name: 'Limpo', exact: true })).toBeVisible()
  await expect(card.getByRole('button', { name: 'Recaída', exact: true })).toBeVisible()
  await expect(card.getByRole('button', { name: 'Pulei', exact: true })).toBeVisible()
  await expect(card.getByRole('button', { name: 'Mínimo', exact: true })).toHaveCount(0)
  await expect(card.getByRole('button', { name: 'Feito', exact: true })).toHaveCount(0)
  await expect(clean).toHaveText('0 dias limpo · recorde 0')

  const limpo = card.getByRole('button', { name: 'Limpo', exact: true })
  await limpo.click()
  await expect(limpo).toHaveAttribute('aria-pressed', 'true')
  await expect(clean).toHaveText('1 dia limpo · recorde 1')
  await expect(card.getByLabel('Quantos minutos?')).toHaveCount(0)

  const recaida = card.getByRole('button', { name: 'Recaída', exact: true })
  await recaida.click()
  await expect(recaida).toHaveAttribute('aria-pressed', 'true')
  // A Recaída de hoje sobrescreve o Limpo de hoje: sem dias anteriores, não há recorde a manter.
  await expect(clean).toHaveText('0 dias limpo · recorde 0')

  // Tocar de novo desfaz o check-in.
  await recaida.click()
  await expect(recaida).toHaveAttribute('aria-pressed', 'false')
  await expect(limpo).toHaveAttribute('aria-pressed', 'false')
})
