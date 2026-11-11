# Plano: issues abertas do Arco (#13, #17, #18, #19, #20, #21)

## Contexto
Seis issues abertas, todas `enhancement`. #13 já tem `azure-pipelines.yml` versionado (sem deploy); falta só o badge no README (linhas 8-10, placeholders `ORG/PROJETO`/`definitionId=1`). Isso depende de org/projeto/id reais do Azure DevOps, então **fica fora das ondas** (ação manual do usuário).

## Contratos
- Nenhum DTO novo. Textos novos nos dois locales (`pt-BR`, `en`), sem texto fixo em componente.
- #18, contas antigas sem migration: nova chave localStorage `arco-first-session-seen`, gravada na primeira renderização do checklist. Se `checkins-1` está desbloqueada e a chave não existe, não mostra (conta antiga). Limitação aceita: é por dispositivo.
- #18, um único slot de onboarding em Hoje: checklist incompleto tem prioridade; `PushPrimingCard`/`InstallCard` só renderizam quando o checklist não está visível.
- #18, passo `checkIn` do checklist passa a contar só Done/Minimum (alinha com `checkins-1`).
- Backend: `DELETE` de check-in chama `AchievementService.RevokeUnmetAsync` (regra do CLAUDE.md).
- Mudou contrato da API? Atualizar `docs/API.md` (não esperado).

## Tarefas

## T1 — #19 PlanCard sem set-state-in-effect
- Arquivos: `web/src/features/billing/PlanCard.tsx`
- Deps: nenhuma
- Pronto: oxlint sem `set-state-in-effect`, sem supressão; polling máx. 5 tentativas, para ao sair ou virar Pro; toast único sob StrictMode (inicializar `polling` a partir de `?checkout=success` na montagem; manter guard `handled`).
- modelo: sonnet · esforço: medium

## T2 — #18 Checklist da primeira sessão (frontend)
- Arquivos: `web/src/features/onboarding/FirstSessionChecklist.tsx`, `web/src/features/onboarding/PushPrimingCard.tsx`, `web/src/pages/TodayPage.tsx`, `web/src/i18n/locales/pt-BR/onboarding.ts`, `web/src/i18n/locales/en/onboarding.ts`
- Deps: nenhuma
- Pronto: após o 1º check-in o checklist mostra progresso até concluir os 3 passos ou ser dispensado; conta antiga (checkins-1 sem chave `seen`) não vê; priming/instalação não aparecem junto com o checklist; `npm run lint` e `build` ok.
- modelo: sonnet · esforço: medium

## T3 — #18 Revogar conquista ao excluir check-in (backend)
- Arquivos: `src/Arco.Api/Features/CheckIns/CheckInEndpoints.cs`, `tests/Arco.Api.Tests/Phase3ApiTests.cs`
- Deps: nenhuma
- Pronto: `DELETE` chama `RevokeUnmetAsync` após salvar; teste novo: marcar, excluir, `checkins-1` volta a bloqueada; `dotnet test` verde.
- modelo: sonnet · esforço: medium

## T4 — #17 E2E do Desfazer na fila offline
- Arquivos: `web/e2e/checkin-offline.spec.ts` (novo)
- Deps: nenhuma
- Pronto: usa `page.context().setOffline(true)` depois da página carregada (não `route.abort`); cria hábito antes via `createHabit`; cenários: (a) marcar offline, religar, 1 registro; (b) marcar, Desfazer, religar, status vazio e XP igual ao inicial (nunca negativo/duplicado); (c) minutos 25 com blur e depois Mínimo, religar, termina Mínimo com 25 min. Espera pela resposta da API ao religar, sem sleep fixo. Confere estado final por `GET` na API. Roda nos projetos `e2e` e `e2e-mobile`. Se um bug real aparecer (ex.: rollback apagando otimismo seguinte), registrar em issue e não mascarar.
- modelo: sonnet · esforço: high

## T5 — #20 Ampliar axe
- Arquivos: `web/e2e/a11y.spec.ts`
- Deps: nenhuma
- Pronto: lista de cenários `{ nome, passos }` com as mesmas tags e regra (reprova só critical/serious, resto em log): landing `/landing/` e `/landing/en/`; `/login`, `/register`, `/welcome` (contexto deslogado/recém-criado); diálogos Novo hábito (`/habits?new=1`), descarte (`alertdialog` via Esc com nome preenchido) e confirmação (`ConfirmDialog` em detalhe do hábito), usando `.include` no diálogo; tema escuro (`colorScheme: 'dark'` ou `arco-theme=dark`) nas rotas logadas. Cenário en sem `pinLocale` (`test.use({ locale: 'en-US' })`). Violações critical/serious: corrigir se trivial, senão `gh issue create` próprias e marcar o cenário com `test.fixme` citando a issue.
- modelo: sonnet · esforço: high

## T6 — #21 Arco: acessibilidade e toque
- Arquivos: `web/src/features/arch/Arch.tsx`, `web/src/features/arch/StoneDays.tsx`, `web/src/features/arch/DayStone.tsx`, `web/src/features/arch/ArchPanel.tsx`, `web/src/i18n/locales/pt-BR/arch.ts`, `web/src/i18n/locales/en/arch.ts`
- Deps: nenhuma (onda 2)
- Pronto: `role="img"` deixa de esconder as pedras (grupo com nomes ou lista de botões fora do SVG); pedras futuras clicáveis no interior (fill transparente); alvo ≥44px efetivo (área de toque transparente) com 10-12 semanas; "em andamento" visualmente distinto de "concluída"; foco visível com contraste; "hoje" vindo do fuso do usuário (`todayApi`) em vez de `localToday()`; remover `animateKey` morto ou ligá-lo; layout `Row` conferido com 12 semanas.
- modelo: sonnet · esforço: high

## T7 — #21 Hierarquia da página do ciclo
- Arquivos: `web/src/pages/CyclePage.tsx`, `web/src/features/cycle/PastCycles.tsx`, `web/src/i18n/locales/pt-BR/pages.ts`, `web/src/i18n/locales/en/pages.ts`, `web/src/i18n/locales/pt-BR/cycle.ts`, `web/src/i18n/locales/en/cycle.ts`
- Deps: nenhuma (onda 2; arquivos disjuntos de T6)
- Pronto: um único CTA de encerrar (o cartão de revisão quando o ciclo acabou; o botão secundário só antes disso); progresso sem repetição tripla; exclusão menos proeminente; `PastCycles` sem `div`/`p` dentro de `button` e com nomes em cabeçalho; copy "Toque para tentar de novo" coerente com o comportamento.
- modelo: sonnet · esforço: medium

## T8 — #21 E2E da página /cycle e axe nela
- Arquivos: `web/e2e/cycle.spec.ts` (novo), `web/e2e/a11y.spec.ts`
- Deps: T5, T6, T7
- Pronto: teste cria ciclo (via API) e confere nas duas larguras: pedras acessíveis por nome, contagem de botões, sem overflow horizontal em 390px, uma só ação de encerrar; `/cycle` entra na lista do axe. Nota: `svg.arch path` (public-profile) e "Semana atual" (partners) são do HTML gerado em C#, não do React; continuam intactos.
- modelo: sonnet · esforço: medium

## Ondas
- **Onda 1:** T1, T2, T3, T4, T5 ✅ concluída
- **Onda 2:** T6, T7 ✅ concluída
- **Onda 3:** T8 ✅ concluída

## Verificação
- Onda 1: `dotnet test` · `cd web && npm run lint && npm run build && npx playwright test checkin-offline a11y`
- Onda 2: `cd web && npm run lint && npm run build`
- Onda 3: `dotnet test` · `cd web && npm run lint && npm run build && npm run e2e`

## Fora do plano
- #13: confirmar org/projeto/`definitionId` do Azure DevOps, rodar o pipeline uma vez (primeiro `dotnet test` em Linux) e então descomentar o badge em `README.md:8-10`.
- Limite do `/goal`: 5 × 3 ondas + 10 = 25 turnos.
