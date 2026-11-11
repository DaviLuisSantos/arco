import AxeBuilder from '@axe-core/playwright'
import { expect, test, type Page } from '@playwright/test'
import { createHabit, PASSWORD, pinLocale, register, uniqueEmail } from './helpers'

// Telas com o axe (WCAG 2.x A/AA). Só violações críticas e graves reprovam;
// as demais saem no log para acompanhamento.
const TAGS = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa']

/** Roda o axe na tela atual (ou só em `include`) e devolve as violações críticas/graves. */
type Analisar = (rotulo: string, include?: string) => Promise<void>

interface Cenario {
  nome: string
  /** deslogado: sem conta · recemCriado: conta sem onboarding concluído · logado: conta já no app. */
  contexto: 'deslogado' | 'recemCriado' | 'logado'
  /** Idioma do navegador: 'en' não fixa o locale do app (usa o do navegador). */
  idioma?: 'pt' | 'en'
  escuro?: boolean
  passos: (page: Page, analisar: Analisar) => Promise<void>
  /** Issue aberta para uma violação conhecida: o cenário vira `test.fixme`. */
  fixme?: string
  /** O `fixme` vale só no projeto mobile (a violação não ocorre no desktop). */
  fixmeSoMobile?: boolean
}

const ROTAS_LOGADAS = ['/', '/habits', '/progress', '/settings', '/profile', '/cycle']

async function rotas(page: Page, analisar: Analisar) {
  // /cycle só tem conteúdo relevante (arco, metas) com um ciclo ativo.
  const res = await page.request.post('/api/cycles', { data: { name: 'Ciclo a11y', weeks: 12 } })
  expect(res.ok(), await res.text()).toBeTruthy()
  for (const rota of ROTAS_LOGADAS) {
    await page.goto(rota)
    await page.waitForLoadState('networkidle')
    await analisar(rota)
  }
}

async function abrirDetalheDeHabito(page: Page) {
  await createHabit(page, 'Hábito a11y')
  const res = await page.request.get('/api/habits')
  expect(res.ok()).toBeTruthy()
  const habitos = (await res.json()) as { id: number; name: string }[]
  const habito = habitos.find((h) => h.name === 'Hábito a11y')
  expect(habito).toBeTruthy()
  await page.goto(`/habits/${habito!.id}`)
  await expect(page.getByRole('heading', { name: 'Hábito a11y', level: 1 })).toBeVisible()
}

const CENARIOS: Cenario[] = [
  // Páginas públicas (HTML estático em public/landing).
  {
    nome: 'landing pt-BR',
    contexto: 'deslogado',
    fixme: 'issue #22: label-content-name-mismatch no seletor de idioma',
    passos: async (page, analisar) => {
      await page.goto('/landing/')
      await page.waitForLoadState('networkidle')
      await analisar('/landing/')
    },
  },
  {
    nome: 'landing en',
    contexto: 'deslogado',
    idioma: 'en',
    fixme: 'issue #22: label-content-name-mismatch no seletor de idioma',
    passos: async (page, analisar) => {
      await page.goto('/landing/en/')
      await page.waitForLoadState('networkidle')
      await analisar('/landing/en/')
    },
  },
  {
    nome: 'login',
    contexto: 'deslogado',
    passos: async (page, analisar) => {
      await page.goto('/login')
      await page.waitForLoadState('networkidle')
      await analisar('/login')
    },
  },
  {
    nome: 'login en',
    contexto: 'deslogado',
    idioma: 'en',
    passos: async (page, analisar) => {
      await page.goto('/login')
      await page.waitForLoadState('networkidle')
      await analisar('/login (en)')
    },
  },
  {
    nome: 'cadastro',
    contexto: 'deslogado',
    fixme: 'issue #23: link-in-text-block no link da página',
    passos: async (page, analisar) => {
      await page.goto('/register')
      await page.waitForLoadState('networkidle')
      await analisar('/register')
    },
  },
  {
    nome: 'cadastro en',
    contexto: 'deslogado',
    idioma: 'en',
    fixme: 'issue #23: link-in-text-block no link da página',
    passos: async (page, analisar) => {
      await page.goto('/register')
      await page.waitForLoadState('networkidle')
      await analisar('/register (en)')
    },
  },
  {
    nome: 'boas-vindas (conta recém-criada)',
    contexto: 'recemCriado',
    passos: async (page, analisar) => {
      await page.goto('/welcome')
      await expect(page.getByTestId('onboarding-skip')).toBeVisible()
      await page.waitForLoadState('networkidle')
      await analisar('/welcome')
    },
  },

  // Telas principais, claro e escuro.
  {
    nome: 'telas principais',
    contexto: 'logado',
    passos: rotas,
  },
  {
    nome: 'telas principais (tema escuro)',
    contexto: 'logado',
    escuro: true,
    passos: rotas,
  },

  // Diálogos: o axe olha só o diálogo aberto (o resto da página fica inerte por trás).
  {
    nome: 'diálogo Novo hábito',
    contexto: 'logado',
    fixme: 'issue #24: color-contrast no seletor Frequência',
    passos: async (page, analisar) => {
      await page.goto('/habits?new=1')
      const dialogo = page.getByRole('dialog', { name: 'Novo hábito' })
      await expect(dialogo).toBeVisible()
      await analisar('Novo hábito', 'dialog[open]')
    },
  },
  {
    nome: 'diálogo Novo hábito (tema escuro)',
    contexto: 'logado',
    escuro: true,
    fixme: 'issue #24: color-contrast no botão Criar',
    passos: async (page, analisar) => {
      await page.goto('/habits?new=1')
      await expect(page.getByRole('dialog', { name: 'Novo hábito' })).toBeVisible()
      await analisar('Novo hábito (escuro)', 'dialog[open]')
    },
  },
  {
    nome: 'confirmação de descarte (alertdialog)',
    contexto: 'logado',
    fixme: 'issue #25: target-size nos botões do alertdialog',
    fixmeSoMobile: true,
    passos: async (page, analisar) => {
      await page.goto('/habits?new=1')
      const dialogo = page.getByRole('dialog', { name: 'Novo hábito' })
      await expect(dialogo).toBeVisible()
      await dialogo.getByLabel('Nome', { exact: true }).fill('Rascunho a11y')
      await page.keyboard.press('Escape')
      await expect(page.getByRole('alertdialog')).toBeVisible()
      await analisar('descarte', '[role="alertdialog"]')
    },
  },
  {
    nome: 'confirmação de arquivar (ConfirmDialog)',
    contexto: 'logado',
    passos: async (page, analisar) => {
      await abrirDetalheDeHabito(page)
      await page.getByRole('button', { name: 'Arquivar hábito' }).click()
      const dialogo = page.getByRole('dialog', { name: 'Arquivar hábito?' })
      await expect(dialogo).toBeVisible()
      await analisar('arquivar', 'dialog[open]')
    },
  },
]

for (const idioma of ['pt', 'en'] as const) {
  test.describe(`axe (${idioma})`, () => {
    if (idioma === 'en') test.use({ locale: 'en-US' })

    for (const c of CENARIOS.filter((x) => (x.idioma ?? 'pt') === idioma)) {
      test(`axe: ${c.nome}`, async ({ page }, testInfo) => {
        if (c.fixme) test.fixme(!c.fixmeSoMobile || testInfo.project.name === 'e2e-mobile', c.fixme)

        if (idioma === 'pt') await pinLocale(page)
        if (c.escuro) {
          await page.emulateMedia({ colorScheme: 'dark' })
          await page.addInitScript(() => {
            try {
              localStorage.setItem('arco-theme', 'dark')
            } catch {
              // sem storage: vale o colorScheme emulado
            }
          })
        }

        if (c.contexto === 'logado') {
          await register(page)
        } else if (c.contexto === 'recemCriado') {
          const res = await page.request.post('/api/auth/register', {
            data: { email: uniqueEmail('a11y'), password: PASSWORD, displayName: 'Pessoa E2E', locale: 'pt-BR' },
          })
          expect(res.ok(), await res.text()).toBeTruthy()
        }

        const achados: string[] = []
        const analisar: Analisar = async (rotulo, include) => {
          // Diálogos abrem com animação (open:animate-pop); contraste medido no meio dela é falso positivo.
          await page.evaluate(() => Promise.all(document.getAnimations().map((a) => a.finished.catch(() => undefined))))
          let axe = new AxeBuilder({ page }).withTags(TAGS)
          if (include) axe = axe.include(include)
          const { violations } = await axe.analyze()
          for (const v of violations) {
            const linha = `${rotulo} [${v.impact}] ${v.id}: ${v.nodes.map((n) => n.target.join(' ')).slice(0, 3).join(' | ')}`
            console.log(linha)
            if (v.impact === 'critical' || v.impact === 'serious') achados.push(linha)
          }
        }

        await c.passos(page, analisar)
        expect(achados, achados.join('\n')).toEqual([])
      })
    }
  })
}
