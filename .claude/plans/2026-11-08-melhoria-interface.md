# Plano: melhoria incremental da interface do Arco

## Contexto
Auditoria do `web/` + pesquisa de mercado. Escopo decidido: app logado, onboarding, landing e planos; evolução incremental da identidade atual; mobile/PWA primeiro.

A base é boa (tokens, `<dialog>` nativo, safe-area, otimismo no check-in, fila offline). Os problemas estão em:
- hierarquia de Hoje (hábitos só depois de 4–6 blocos);
- Perfil com ~14 blocos, Estatísticas e Revisão a 2 toques;
- só spinner, sem skeleton; sem "Desfazer"; "Não fiz" sem feedback;
- formulário de hábito longo, sem guarda de perda de dados;
- contraste abaixo de 4,5:1 em alguns pares do tema claro; alvos de 36px;
- bundle único, sem code splitting;
- sem priming de push, sem instalação PWA, sem checklist de primeira sessão;
- limite do Free só aparece depois do erro; retorno do checkout sem feedback;
- landing: mockup com XP errado, sem Free x Pro, exemplos pouco variados.

Decisões: comparação Free x Pro **sem preço**; aviso "Modo de teste" **fica** (Stripe em sandbox, issue #2; SaaS de portfólio); cadastro linka **só privacidade** (não há página de termos).

## Padrões de mercado adotados
| Padrão | Referência | Onde |
|---|---|---|
| Barra inferior com 3–5 destinos; retoque na aba ativa volta ao topo | Material 3 | T3 |
| Alvo mínimo 24px (AA); meta prática 44/48 | WCAG 2.2 SC 2.5.8, Apple, Material | T2 |
| Foco não encoberto; arrastar sempre com alternativa sem arrasto | WCAG 2.2 SC 2.4.11 e 2.5.7 | T2, T12 |
| Skeleton só em carga de página inteira | NN/g | T2, T4, T8 |
| Estado vazio diz o que houve e o próximo passo | NN/g | T8, T9 |
| View transitions com detecção e fallback | web.dev (Baseline out/2025) | T3 |
| Marcar hábito em até 2 toques | Convergência de apps (blogs, direcional) | T4, T5 |
| Permissão pedida após valor percebido, com tela de priming | Appcues, OneSignal (direcional) | T9 |
| Upgrade no ponto do bloqueio, com contador prévio e benefício específico | Appcues, SaaS UI (direcional) | T10 |

Evidência sobre gamificação é mista (burnout com badges complexos: Koivisto & Hamari; hábito tolera falhas isoladas: Lally 2009). Por isso **este plano não acrescenta mecânicas de jogo**: só melhora o feedback das existentes. Números de vendors não entram como meta.

## Fora do escopo (próximas rodadas)
Mudança de regra de sequência/tolerância a falhas (domínio), conversão de demo em conta (backend), widgets, página de termos, preço público.

## Contratos
- Sem mudança na API, no domínio ou no `buster` do cache. `GET /api/billing` já traz `usage` e `limits` (chave `['billing']`).
- `Toast`: opções `action: { label, onClick }` e `duration`; clique na ação não dispensa por propagação.
- `Dialog`: prop `dirty?: boolean` intercepta Esc (`cancel`), backdrop e X com confirmação; hook `useDialogGuardedClose()` para botões "Cancelar" internos; `description` liga `aria-describedby`.
- `Skeleton`: `<Skeleton className />`, sem animação sob reduced-motion.
- `lib/theme.ts`: `getTheme()`, `setTheme('system'|'light'|'dark')`; localStorage `arco-theme`; atributo `data-theme` em `<html>`; script inline antes da pintura.
- `PlayerProgress`: prop `compact?: boolean`.
- Rotas novas: `/progress`, `/settings` (stubs criados em T3). Rotas atuais continuam válidas.
- Rascunho da revisão: sessionStorage `arco-review-draft:<userId>:<weekStart>`, limpo ao salvar.
- `lib/pwaInstall.ts`: captura `beforeinstallprompt` no boot.
- **Regras para todas as tarefas:** todo texto via i18n em pt-BR e en; `getByRole`/`getByLabel`/`getByText` do E2E casam substring, então nenhum link novo em Hoje pode conter "Criar hábito", "Hoje" ou "Perfil", e nenhum rótulo novo no formulário de hábito pode conter "Nome". Preservar: "0 XP hoje", /\+\d+ XP hoje/, "Hábitos de hoje", "Nenhum hábito ainda", "Plano atual", "Ver plano", "Seu plano atingiu esse limite", h1 "Cada dia alimenta", grupo "Check-in de <hábito>" com "Feito" e `aria-pressed`, `onboarding-skip`.

## T1 Tokens e tema
Em `index.css`: escala semântica de texto (caption/body/title) sem migrar telas; contraste ≥4,5:1 no claro ajustando só valores de token (`text-xp` 3,99/3,69; texto sobre `bg-xp` 3,69; placeholder 2,92; `text-done` sobre `bg-done/15` 4,43; `text-danger` sobre `bg-danger/15` ~4,1); `[data-theme]` além do media query; `theme-color` por esquema; `animate-spin` e `::view-transition-*` sob reduced-motion; fallback `vh` antes do `dvh` no `body`.
- Arquivos: `web/src/index.css`, `web/index.html`, `web/src/components/ui/Input.tsx`, `web/src/lib/theme.ts` (novo).
- Depende de: nada.
- Pronto: pares acima ≥4,5:1 nos dois temas; tema forçado funciona sem flash.
- modelo: sonnet · esforço: medium

## T2 Primitivas
`Skeleton` novo; `Button sm`, fechar do `Dialog` e `Segmented` com alvo ≥44px; `Dialog` conforme Contratos, com arrastar para fechar opcional (o X segue como alternativa); `Segmented` com setas e roving tabindex; `Toast` com `action` e `duration`.
- Arquivos: `web/src/components/ui/{Button,Dialog,Segmented,Toast,Skeleton}.tsx`, `web/src/components/ui/toast-context.ts`, `web/src/i18n/locales/{pt-BR,en}/common.ts`.
- Depende de: nada.
- Pronto: guarda cobre Esc, backdrop, X e hook; E2E verde.
- modelo: sonnet · esforço: high

## T3 Shell e navegação
Code splitting com `route.lazy`; `errorElement` e tratamento de `vite:preloadError` (chunk antigo após deploy); barra com Hoje, Hábitos, Ciclo, Progresso, Perfil (Admin sai da barra; o link em Perfil é de T7); retoque na aba ativa volta ao topo; Progresso ativa em `/stats` e `/review`, Perfil em `/settings`; view transitions com fallback; stubs de `/progress` e `/settings`.
- Arquivos: `web/src/app/router.tsx`, `web/src/components/layout/AppLayout.tsx`, `web/src/main.tsx`, `web/src/pages/{ProgressPage,SettingsPage}.tsx` (novos), `web/src/i18n/locales/{pt-BR,en}/layout.ts`.
- Depende de: nada.
- Pronto: build gera chunks por rota; E2E verde; links "Hoje"/"Perfil" únicos por viewport.
- modelo: sonnet · esforço: high

## T4 Tela Hoje
Lista de hábitos na primeira dobra: reordenar `CycleStrip`, `DayStone` e objetivos; `PlayerProgress compact`; nudges compactos; indicador de progresso do dia (sem repetir o texto "XP hoje"); skeleton na carga; remover `aria-label` de `div` sem role.
- Arquivos: `web/src/pages/TodayPage.tsx`, `web/src/features/player/PlayerProgress.tsx`, `web/src/features/reviews/TodayNudges.tsx`, `web/src/i18n/locales/{pt-BR,en}/{pages,player,reviews}.ts`.
- Depende de: T2.
- Pronto: em 390×844, primeiro hábito visível sem rolagem; E2E verde.
- modelo: sonnet · esforço: medium

## T5 Feedback do check-in
Toast "Desfazer" restaurando o status anterior, disparado no toque/`onMutate` (offline não roda `onSuccess`) e passando pela fila `scope`; `navigator.vibrate` com detecção; feedback em "Não fiz"; minutos salvos ao sair do campo sem competir com os botões de status; celebração de dia completo **sem modal** (toast + `animate-pop`), respeitando reduced-motion.
- Arquivos: `web/src/features/checkins/{HabitCheckInCard.tsx,useCheckIn.ts}`, `web/src/i18n/locales/{pt-BR,en}/checkins.ts`.
- Depende de: T2.
- Pronto: desfazer funciona online e na fila offline, mantendo a ordem; E2E verde.
- modelo: sonnet · esforço: high

## T6 Formulário de hábito
Escada, regra e horário em "Opções avançadas" (abre se houver erro oculto ou, na edição, valor preenchido); `dirty` e fechamento guardado; validação ao sair do campo; remover `placeholder="🏃"` fixo (linha 169); `habits.ts:88` passa a "em Configurações".
- Arquivos: `web/src/features/habits/HabitFormDialog.tsx`, `web/src/i18n/locales/{pt-BR,en}/habits.ts`.
- Depende de: T2.
- Pronto: criar hábito simples exige só os campos essenciais; E2E verde.
- modelo: sonnet · esforço: medium

## T7 Perfil e Configurações
`/settings`: fuso/idioma, tema, notificações, segurança, backup, chaves de API, conta. Perfil mantém progresso, conquistas, XP recente, perfil público, parceiros, `PlanCard`, link para Configurações, link de Admin e "Sair". O "voltar" de Configurações não pode se chamar "Perfil". Reaproveitar chaves `pages.profile.*` sem editar `pages.ts`. Em `security.spec`, voltar a Perfil antes de "Sair".
- Arquivos: `web/src/pages/{ProfilePage,SettingsPage}.tsx`, `web/src/i18n/locales/{pt-BR,en}/settings.ts` (novo), `web/e2e/{helpers.ts,security.spec.ts,locale.spec.ts}`.
- Depende de: T1, T3.
- Pronto: Perfil sem blocos de configuração; E2E verde.
- modelo: sonnet · esforço: medium

## T8 Hub de Progresso
`/progress` reúne Estatísticas, Revisão e Conquistas; estado vazio em Estatísticas; erro visível das listas secundárias de `ReviewPage` e `CyclePage`; rascunho da revisão conforme Contratos; skeletons; "voltar" de `StatsPage` para `/progress`; remover `aria-labelledby` de `Card` sem role (`CyclePage:75`).
- Arquivos: `web/src/pages/{ProgressPage,StatsPage,ReviewPage,CyclePage}.tsx`, `web/src/i18n/locales/{pt-BR,en}/progress.ts` (novo).
- Depende de: T2, T3.
- Pronto: rascunho sobrevive a sair e voltar; E2E verde.
- modelo: sonnet · esforço: medium · skills: dataviz

## T9 Onboarding e primeira sessão
Estado vazio de Hoje com atalhos de modelo (botões que chamam `habitsApi.create` e tratam `plan_limit`); checklist de primeira sessão; cartão de priming de push após o primeiro check-in (derivar de `checkIn` em `/api/today` e da conquista `checkins-1`; exigir `hasServiceWorker()`; "já ofereci" em localStorage), definindo o horário do lembrete, pois hábitos de modelo não têm `reminderTime`; instalação PWA + instrução iOS; modelos e placeholders mais variados; Google e link de privacidade (`<a>`, fora do SPA) no cadastro.
- Arquivos: `web/src/pages/{TodayPage,RegisterPage}.tsx`, `web/src/main.tsx`, `web/src/lib/{push.ts,pwaInstall.ts}` (`pwaInstall` novo), `web/src/features/onboarding/{templates.ts,TemplateShortcuts.tsx,FirstSessionChecklist.tsx,PushPrimingCard.tsx,InstallCard.tsx}` (`.tsx` novos), `web/src/i18n/locales/{pt-BR,en}/{onboarding,auth,pages,habits,goals}.ts`.
- Depende de: T4, T5.
- Pronto: conta nova chega ao primeiro check-in a partir do estado vazio; push nunca é pedido antes do priming; E2E verde.
- modelo: sonnet · esforço: high

## T10 Planos e upgrade
Contador de uso em Hábitos antes do limite (`useQuery(['billing'])`); `PlanLimitNotice` com benefício específico; ler `?checkout=` dentro de `PlanCard`, com toast único (StrictMode) e nova consulta algumas vezes (webhook pode atrasar); comparação Free x Pro sem preço; "Modo de teste" inalterado. Preservar `/profile$`, "Ver plano" e "Plano atual" único.
- Arquivos: `web/src/pages/HabitsPage.tsx`, `web/src/features/billing/{PlanCard,PlanLimitNotice}.tsx`, `web/src/features/habits/HabitFormDialog.tsx`, `web/src/features/publicProfile/PublicProfileCard.tsx`, `web/src/i18n/locales/{pt-BR,en}/billing.ts`.
- Depende de: T6, T7.
- Pronto: uso visível antes do limite; retorno do checkout com feedback; E2E verde.
- modelo: sonnet · esforço: medium

## T11 Landing
pt-BR e en: exemplos variados (finanças, trabalho, criatividade, relações); `data-xp` 10,5 / 12,6 / 14,7 conforme `XpRules`; seção Free x Pro sem preço; ressalva de push no iPhone e MCP só no Pro; JSON-LD; `robots.txt` e `sitemap.xml`; remover `aria-label` de `.phone` sem role.
- Arquivos: `web/public/landing/index.html`, `web/public/landing/en/index.html`, `web/public/{robots.txt,sitemap.xml}` (novos).
- Depende de: nada.
- Pronto: mockup coerente com as regras; h1 preservado; E2E verde.
- modelo: sonnet · esforço: medium

## T12 Qualidade e documentação
Projeto Playwright mobile (perfil Pixel, incluído em `npm run e2e`, excluindo `public-profile.spec`); `@axe-core/playwright` (devDependency) nas telas principais; atualizar `PRODUCT.md:147` e `repo_map.md`; regenerar imagens com `npm run screenshots`. Violações que exijam arquivo fora da lista: reportar, não corrigir aqui.
- Arquivos: `web/playwright.config.ts`, `web/package.json`, `web/package-lock.json`, `web/e2e/a11y.spec.ts` (novo), `docs/PRODUCT.md`, `docs/repo_map.md`, `docs/images/*.png`.
- Depende de: T1–T11.
- Pronto: E2E desktop e mobile verdes; violações críticas do axe zeradas ou reportadas.
- modelo: sonnet · esforço: medium

## Ondas
- [x] **Onda 1:** T1, T2, T3
- [x] **Onda 2:** T4, T5, T6, T7, T8
- [x] **Onda 3:** T9, T10, T11
- [x] **Onda 4:** T12

## Verificação (ao fim de cada onda)
`cd web && npm run lint && npm run build && npm run e2e`

Onda 4: o mesmo, incluindo o projeto mobile e `a11y.spec`, mais `dotnet test` (a landing e o `wwwroot` são servidos pelo ASP.NET).

Não há bump de cache manual: o service worker pré-cacheia a landing, então quem já tem SW pode ver a versão anterior por uma carga.

## Fontes
- Primárias: [W3C — Target Size](https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html), [W3C — What's New in WCAG 2.2](https://www.w3.org/WAI/standards-guidelines/wcag/new-in-22/), [Material 3 — Navigation bar](https://m3.material.io/components/navigation-bar/guidelines), [Material 3 — Bottom sheets](https://m3.material.io/components/bottom-sheets/guidelines), [NN/g — Skeleton Screens](https://www.nngroup.com/articles/skeleton-screens/), [NN/g — Empty States](https://www.nngroup.com/articles/empty-state-interface-design/), [web.dev — View Transitions Baseline](https://web.dev/blog/same-document-view-transitions-are-now-baseline-newly-available), [Duolingo — Streaks](https://blog.duolingo.com/how-streaks-keep-duolingo-learners-committed-to-their-language-goals/), [HabitWalk](https://www.ncbi.nlm.nih.gov/pmc/articles/PMC11635918/), [mHealth abandonment](https://pmc.ncbi.nlm.nih.gov/articles/PMC8872344/).
- Direcionais (vendors): [Appcues — Permission priming](https://www.appcues.com/blog/mobile-permission-priming), [OneSignal — Onboarding](https://onesignal.com/blog/how-to-build-an-effective-mobile-app-onboarding-process/), [Appcues — Upgrade prompts](https://www.appcues.com/blog/how-freemium-apps-convert-users-with-brilliant-upgrade-prompts), [SaaS UI — Paywall patterns](https://www.saasui.design/blog/saas-upgrade-paywall-ux-patterns), [Mobbin — Streak designs](https://mobbin.com/blog/streaks-are-about-losing-a-study-of-859-streak-designs), [Yu-kai Chou — Streak design](https://yukaichou.com/gamification-analysis/streak-design-gamification-motivation-burnout/).
