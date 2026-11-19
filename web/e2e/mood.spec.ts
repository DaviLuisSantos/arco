import { expect, test } from '@playwright/test'
import { createHabit, PASSWORD, pinLocale, reachToday, uniqueEmail } from './helpers'

test.describe('nota e humor', () => {
  test('registrar humor e nota no Hoje e ver os dois na revisão semanal', async ({ page }) => {
    await pinLocale(page)
    // Conta e hábito pela API antes de abrir o app (o cache persistido não pode guardar a lista vazia).
    const reg = await page.request.post('/api/auth/register', {
      data: { email: uniqueEmail('mood'), password: PASSWORD, displayName: 'Pessoa E2E', locale: 'pt-BR' },
    })
    expect(reg.ok(), await reg.text()).toBeTruthy()
    await createHabit(page, 'Ler com nota')
    await page.goto('/')
    await reachToday(page)

    // Humor do dia: 4 = Bom.
    const bom = page.getByRole('button', { name: '4: Bom' })
    await bom.click()
    await expect(bom).toHaveAttribute('aria-pressed', 'true')

    // Check-in com nota.
    const card = page.getByRole('article').filter({ hasText: 'Ler com nota' })
    await card.getByRole('button', { name: 'Feito' }).click()
    await expect(card.getByRole('button', { name: 'Feito' })).toHaveAttribute('aria-pressed', 'true')
    await card.getByRole('button', { name: 'Adicionar nota' }).click()
    const note = card.getByLabel('Nota (opcional)')
    await note.fill('Li no ônibus')
    await note.press('Enter')
    await expect(card.getByRole('button', { name: 'Editar nota' }).or(note)).toBeVisible()

    // A revisão mostra a semana anterior (exceto aos domingos): registra humor e nota no último dia dela, pela API.
    const week = await (await page.request.get('/api/reviews/weekly/current')).json()
    const habits = (await (await page.request.get('/api/habits')).json()) as { id: number; name: string }[]
    const habitId = habits.find((h) => h.name === 'Ler com nota')!.id
    const m = await page.request.put('/api/moods', { data: { date: week.weekEnd, value: 4 } })
    expect(m.ok(), await m.text()).toBeTruthy()
    const c = await page.request.put('/api/checkins', {
      data: { habitId, date: week.weekEnd, status: 'Done', note: 'Li no ônibus' },
    })
    expect(c.ok(), await c.text()).toBeTruthy()

    await page.goto('/review')
    await expect(page.getByRole('heading', { name: 'Revisão da semana', level: 1 })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Humor da semana' })).toBeVisible()
    await expect(page.getByTestId('mood-average')).toHaveText('4.0')
    await expect(page.getByRole('heading', { name: 'Notas da semana' })).toBeVisible()
    await expect(page.getByText('Li no ônibus')).toBeVisible()
  })
})
