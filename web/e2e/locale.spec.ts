import { expect, test } from '@playwright/test'
import { gotoSettings, register } from './helpers'

test('trocar o idioma nas Configurações mostra os textos em inglês', async ({ page }) => {
  await register(page)
  await gotoSettings(page)

  await page.getByLabel('Idioma').selectOption('en')
  await expect(page.getByRole('heading', { name: 'Settings', level: 1 })).toBeVisible()
  await expect(page.getByRole('link', { name: 'Habits' }).first()).toBeAttached()
  await expect(page.getByLabel('Language')).toHaveValue('en')
  await expect(page.getByText('Two-step verification').first()).toBeVisible()

  // A escolha persiste ao recarregar.
  await page.reload()
  await expect(page.getByRole('heading', { name: 'Settings', level: 1 })).toBeVisible()
})
