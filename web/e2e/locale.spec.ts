import { expect, test } from '@playwright/test'
import { gotoProfile, register } from './helpers'

test('trocar o idioma no Perfil mostra os textos em inglês', async ({ page }) => {
  await register(page)
  await gotoProfile(page)

  await page.getByLabel('Idioma').selectOption('en')
  await expect(page.getByRole('heading', { name: 'Profile', level: 1 })).toBeVisible()
  await expect(page.getByRole('link', { name: 'Habits' }).first()).toBeAttached()
  await expect(page.getByLabel('Language')).toHaveValue('en')
  await expect(page.getByText('Two-step verification').first()).toBeVisible()

  // A escolha persiste ao recarregar.
  await page.reload()
  await expect(page.getByRole('heading', { name: 'Profile', level: 1 })).toBeVisible()
})
