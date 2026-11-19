import { expect, test } from '@playwright/test'
import { createHabit, PASSWORD, pinLocale, reachToday, uniqueEmail } from './helpers'

test.describe('pausa de hábito', () => {
  test('pausar, ver em Pausados e retomar', async ({ page }) => {
    await pinLocale(page)
    // Conta e hábito pela API antes de abrir o app: o cache persistido (staleTime 30 s) não pode guardar a lista vazia.
    const reg = await page.request.post('/api/auth/register', {
      data: { email: uniqueEmail('pause'), password: PASSWORD, displayName: 'Pessoa E2E', locale: 'pt-BR' },
    })
    expect(reg.ok(), await reg.text()).toBeTruthy()
    await createHabit(page, 'Ler pausado')
    await page.goto('/')
    await reachToday(page)
    await expect(page.getByRole('link', { name: 'Ler pausado' })).toBeVisible()

    await page.getByRole('link', { name: 'Ler pausado' }).click()
    await expect(page.getByRole('heading', { name: 'Ler pausado', level: 1 })).toBeVisible()
    await page.getByRole('button', { name: 'Pausar hábito' }).click()
    const dialog = page.getByRole('dialog', { name: 'Pausar hábito' })
    await dialog.getByLabel('Motivo (opcional)').fill('Viagem')
    await dialog.getByRole('button', { name: 'Pausar', exact: true }).click()
    await expect(dialog).toBeHidden()
    await expect(page.getByText('Em curso')).toBeVisible()
    await expect(page.getByRole('button', { name: 'Retomar agora' })).toBeVisible()

    // Navegação interna: o goto recarregaria o cache persistido, ainda anterior à pausa.
    await page.getByRole('link', { name: 'Hoje' }).first().click()
    const pausados = page.locator('details', { hasText: 'Pausados (1)' })
    await expect(pausados).toBeVisible()
    await expect(page.getByRole('article').filter({ hasText: 'Ler pausado' })).toHaveCount(0)
    await pausados.locator('summary').click()
    await expect(pausados.getByText('Ler pausado')).toBeVisible()
    await expect(pausados.getByText('até retomar')).toBeVisible()

    await pausados.getByRole('button', { name: 'Retomar Ler pausado' }).click()
    await expect(page.locator('details', { hasText: 'Pausados' })).toHaveCount(0)
    await expect(page.getByRole('link', { name: 'Ler pausado' })).toBeVisible()
  })
})
