import { expect, test, type Page } from '@playwright/test'
import { PASSWORD, pinLocale, reachToday, uniqueEmail } from './helpers'

async function createHabit(page: Page, name: string, extra: Record<string, unknown> = {}) {
  const res = await page.request.post('/api/habits', {
    data: {
      name,
      frequencyType: 'Daily',
      levelDescription: '20 min',
      minimumDescription: '5 min',
      ...extra,
    },
  })
  expect(res.ok(), await res.text()).toBeTruthy()
  return (await res.json()) as { id: number }
}

test.describe('ordem do dia', () => {
  test('grupos por período, encadeamento e reordenação no Organizar', async ({ page }) => {
    await pinLocale(page)
    // Conta e hábitos pela API antes de abrir o app (o cache persistido não pode guardar a lista vazia).
    const reg = await page.request.post('/api/auth/register', {
      data: { email: uniqueEmail('order'), password: PASSWORD, displayName: 'Pessoa E2E', locale: 'pt-BR' },
    })
    expect(reg.ok(), await reg.text()).toBeTruthy()
    const meditar = await createHabit(page, 'Meditar', { timeOfDay: 'Morning' })
    await createHabit(page, 'Beber água', { timeOfDay: 'Morning' })
    await createHabit(page, 'Alongar', { timeOfDay: 'Morning', anchorHabitId: meditar.id })
    await createHabit(page, 'Ler livro', { timeOfDay: 'Evening' })
    await page.goto('/')
    await reachToday(page)

    // Grupos por período, só os não vazios.
    await expect(page.getByRole('heading', { name: 'Manhã', level: 3 })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Noite', level: 3 })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Tarde', level: 3 })).toHaveCount(0)

    const morning = page.locator('section[data-period="Morning"] article')
    const evening = page.locator('section[data-period="Evening"] article')
    // O encadeado vem logo depois da âncora, antes do hábito que tem ordem manual menor.
    await expect(morning).toHaveText([/Meditar/, /Alongar/, /Beber água/])
    await expect(evening).toHaveText([/Ler livro/])
    const alongar = page.getByRole('article').filter({ hasText: 'Alongar' })
    await expect(alongar.getByText('Depois de Meditar')).toBeVisible()
    await expect(alongar.getByText('Sua vez')).toHaveCount(0)

    // Feito na âncora: o encadeado ganha o destaque "Sua vez".
    // (o card do Alongar também cita "Meditar": o grupo de check-in é o alvo exato)
    const meditarFeito = page.getByRole('group', { name: 'Check-in de Meditar' }).getByRole('button', { name: 'Feito' })
    await meditarFeito.click()
    await expect(meditarFeito).toHaveAttribute('aria-pressed', 'true')
    await expect(alongar.getByText('Sua vez')).toBeVisible()

    // Organizar: sobe "Beber água" para o começo da manhã.
    await page.getByRole('button', { name: 'Organizar' }).click()
    const dialog = page.getByRole('dialog', { name: 'Organizar o dia' })
    await expect(dialog).toBeVisible()
    const subir = dialog.getByRole('button', { name: 'Subir Beber água' })
    await subir.click()
    // Só mudou a ordem manual: o botão do primeiro item fica desativado e o foco passa para "Descer".
    await expect(subir).toBeDisabled()
    await expect(dialog.getByRole('button', { name: 'Descer Beber água' })).toBeFocused()
    await dialog.getByRole('button', { name: 'Salvar ordem' }).click()
    await expect(dialog).toBeHidden()

    await expect(morning).toHaveText([/Beber água/, /Meditar/, /Alongar/])

    // A ordem persiste no servidor (conferida pela API: o cache persistido do app tem staleTime).
    const today = (await (await page.request.get('/api/today')).json()) as { habits: { name: string }[] }
    expect(today.habits.map((h) => h.name)).toEqual(['Beber água', 'Meditar', 'Alongar', 'Ler livro'])
  })
})
