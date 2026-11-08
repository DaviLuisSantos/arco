# Arco de pedra — plano

## Contexto
A "floresta" (uma árvore por sessão) foi aprovada como ponto de partida, mas o usuário aprovou trocá-la por um **arco de pedra** (protótipo: `C:\Users\davil\AppData\Local\Temp\claude\68e076720fedd11d\bd0a9b96-7040-4160-a2d6-143119ac7fdf\scratchpad\arco-de-pedra.html`, artifact https://claude.ai/artifact/6GpuzypV5EjKjaBhfAUt2q; **ler o HTML**: geometria `sector/stoneIndex/archSVG/daysSVG`).
O ciclo é um arco: uma pedra por semana, que enche com as horas de foco (faixas por hábito) até a meta do ciclo; a pedra-chave encaixa ao encerrar. Tocar numa pedra mostra os dias (um tijolo por sessão). Na tela Hoje, a "pedra do dia".
Decisões: **semanas pares**; **meta por ciclo**; backend de sessões (`FocusSession`, `/api/focus-sessions`, `CheckIn.minutes`, stats com `Minutes`) é mantido.
Nada da feature anterior está commitado, então remover `tree`/`TreeStage` não quebra contrato publicado.

## Decisões de projeto (assumidas; ajustar se discordar)
- **Criação de ciclo** exige semanas pares, 2–12 (padrão 6). `PUT` (`endDate`) continua livre; ciclos antigos/ímpares ou com semana parcial usam o layout **Row** (fila reta de pedras + pedra de fecho), com a mesma interação.
- **Semana do ciclo** = bloco de 7 dias a partir de `StartDate` (não seg–dom), como o `EndDate = start + weeks*7 − 1`. Rótulo dos dias na UI vem do dia da semana real de cada data.
- **Meta**: `Cycle.WeeklyFocusMinutes` (padrão 420, faixa 60–3360), definida na criação e editável no ciclo ativo.
- Some `/forest` (página "ano/mês/semana de árvores"); o arco vive em `/cycle`, ciclos encerrados abrem em diálogo.
- Cor do hábito: matiz por `habitId` (como `treeHue`), com saturação/luminosidade por tema em tokens CSS.

## Contratos

**Entidade/DTO de ciclo**: `Cycle.WeeklyFocusMinutes int` (migration `CycleFocusGoal`, default 420 nas linhas existentes). `CycleDto` ganha `Weeks` (= ceil(TotalDays/7)) e `WeeklyFocusMinutes`. `POST /api/cycles` `{ name, startDate?, weeks?, weeklyFocusMinutes? }`: `weeks` par 2–12, senão validação pt-BR ("O ciclo precisa ter um número par de semanas (2, 4 … 12)."). `PUT /api/cycles/{id}` `{ name, endDate, weeklyFocusMinutes? }` (ausente = não muda).

**Regras puras** (`Domain/CycleMath.cs`): `MinWeeks=2`, `MaxWeeks=12`, `IsValidWeeks(w)` (par, 2–12), `WeekOfCycle(start, date)` = ⌊(date−start)/7⌋+1, `WeekCount(start, end)` = ceil(totalDias/7), `ArchLayout(weeks)` → `Arch` se par ≥2, senão `Row`.

**Remoção**: `TreeStage`, `FocusRules.TreeStage`, `FocusSessionDto.Tree` (mantém `FocusRules.MaxMinutes`).

**Novo endpoint** `GET /api/cycles/{id}/arch` (`Features/Cycles/CycleArchEndpoints.cs`, `MapCycleArch` em `Startup.cs`; 404 se não existe):
```
CycleArchDto(int CycleId, DateOnly StartDate, DateOnly EndDate, bool Closed /*Status==Completed*/, int Weeks,
  string Layout /*"Arch"|"Row"*/, int WeeklyTargetMinutes, int TotalMinutes,
  IReadOnlyList<ArchHabitDto> Habits, IReadOnlyList<ArchStoneDto> Stones)
ArchHabitDto(int HabitId, string Name, string? Emoji)               // hábitos com sessão no ciclo
ArchStoneDto(int Week, DateOnly From, DateOnly To, int Minutes,
  IReadOnlyList<ArchHabitMinutesDto> ByHabit, IReadOnlyList<ArchDayDto> Days)   // todas as semanas, inclusive futuras (vazias)
ArchHabitMinutesDto(int HabitId, int Minutes)
ArchDayDto(DateOnly Date, int Minutes, IReadOnlyList<ArchBrickDto> Bricks)       // 7 datas por semana (corta no EndDate)
ArchBrickDto(int SessionId, int HabitId, int Minutes, FocusSource Source)
```
Sessões = `FocusSession` com `Date` entre `StartDate` e `EndDate`. Uma query só, agrupamento em memória.

**MCP**: `ver_arco(cicloId?)` (somente leitura; sem id usa `/api/cycles/current`). `ver_ciclo_atual` já devolve os campos novos.

**Web (tipos)**: `Cycle.weeks`, `Cycle.weeklyFocusMinutes`; `CycleInput.weeks?`, `weeklyFocusMinutes?`; `CycleArch`, `ArchStone`, `ArchDay`, `ArchBrick`, `ArchHabit` espelhando o DTO; `cyclesApi.arch(id)`; `queryKeys.cycleArch(id)`; `habitColor(habitId): string` em `lib/habitColor.ts`; tokens `--habit-s/--habit-l` em `index.css`.

**Componentes** (`web/src/features/arch/`): `geometry.ts` (puro: `sector`, `stoneIndex`, constantes do protótipo, layout Row), `Arch.tsx` (`{ arch, selectedWeek?, onSelect?, small?, animateKey? }`, pedra-chave que desce, `prefers-reduced-motion`), `StoneDays.tsx` (7 colunas, um tijolo por sessão, tooltip), `ArchPanel.tsx` (`{ cycleId }`: busca, arco, legenda, detalhe da semana, dias, editar meta se ativo), `DayStone.tsx` (Hoje). Tudo em pt-BR, textos acessíveis (`role`, `aria-label`, foco por teclado), tokens do `index.css`.

## Tarefas

## T1 — Domínio, EF, migration
Arquivos: `Domain/Entities.cs`, `Domain/CycleMath.cs`, `Infrastructure/AppDbContext.cs`, `Infrastructure/Migrations/*CycleFocusGoal*` (gerada), `tests/Arco.Api.Tests/DomainTests.cs` (só `CycleMathTests`).
Dep.: nenhuma. Pronto: coluna com default 420, regras puras e testes (pares/ímpares, semanas parciais, `WeekOfCycle` nas bordas). Migration via `dotnet ef migrations add CycleFocusGoal --project src/Arco.Api --output-dir Infrastructure/Migrations`; timestamp deve ficar depois de `20261106001019_FocusSessions`.
modelo: sonnet · esforço: medium

## T2 — Remover árvore do backend
Arquivos: `Domain/FocusRules.cs`, `Features/Focus/FocusEndpoints.cs`, `tests/Arco.Api.Tests/FocusRulesDomainTests.cs`, `tests/Arco.Api.Tests/FocusApiTests.cs`.
Dep.: nenhuma. Pronto: `TreeStage`/`Tree` somem (resposta de sessão sem `tree`); testes ajustados; `dotnet test` passa.
modelo: sonnet · esforço: low

## T3 — Web: contratos, cor, geometria
Arquivos: `web/src/api/types.ts` (só **adicionar**; manter `TreeStage`/`tree` até T10), `web/src/api/endpoints.ts`, `web/src/lib/queryKeys.ts`, `web/src/lib/habitColor.ts` (novo), `web/src/index.css`, `web/src/features/arch/geometry.ts` (novo).
Dep.: nenhuma. Pronto: tipos/endpoints/chaves conforme Contratos; `geometry.ts` porta fielmente `sector/stoneIndex` e constantes do protótipo, com layout Row; `npm run build` passa.
modelo: sonnet · esforço: medium

## T4 — API do arco
Arquivos: `Features/Cycles/CycleArchEndpoints.cs` (novo), `Infrastructure/Startup.cs` (só `MapCycleArch`), `tests/Arco.Api.Tests/CycleArchApiTests.cs` (novo).
Dep.: T1. Pronto: `GET /api/cycles/{id}/arch` conforme Contratos; testes: agrupamento por semana (bordas de dia 7/8), dias futuros vazios, ciclo encerrado (`Closed`), layout Row para ciclo ímpar, 404, chave de API; relógio via `ApiFactory(now)`.
modelo: sonnet · esforço: high

## T5 — Ciclo: criação, edição e DTO
Arquivos: `Features/Cycles/CycleEndpoints.cs`, `Features/Shared/Dtos.cs` (só `CycleDto`), `tests/Arco.Api.Tests/ApiTests.cs`, `Phase2ApiTests.cs`, `Phase3ApiTests.cs` (só para trocar `weeks` ímpares usados em ciclos de teste), `tests/Arco.Api.Tests/CycleGoalApiTests.cs` (novo).
Dep.: T1. Pronto: validação de semanas pares, `weeklyFocusMinutes` (faixa 60–3360) em POST/PUT, `CycleDto.Weeks/WeeklyFocusMinutes`; `dotnet test` verde; testes novos (par/ímpar, faixa, PUT sem o campo mantém).
modelo: sonnet · esforço: medium

## T6 — Web: Arch e StoneDays
Arquivos: `web/src/features/arch/Arch.tsx`, `web/src/features/arch/StoneDays.tsx` (novos).
Dep.: T3. Pronto: porta do protótipo para React/TS com dados reais; faixas `max(total, meta)`; pedras futuras tracejadas; semana atual destacada (data de hoje via `lib/dates.ts`, nunca `new Date("YYYY-MM-DD")`); Row para `layout:"Row"`; `small` sem texto/interação; `animateKey`; lint/build verdes.
modelo: sonnet · esforço: high · skills: artifact-design não; seguir os tokens do projeto.

## T7 — Web: pedra do dia e timer
Arquivos: `web/src/features/arch/DayStone.tsx` (novo), `web/src/features/focus/FocusTimer.tsx`, `web/src/pages/TodayPage.tsx`.
Dep.: T3. Pronto: `DayStone` após `CycleStrip` (query `focusApi.list(hoje, hoje)`, tijolos empilhados por hábito, total do dia, estado vazio com chamada para o timer); `FocusTimer` troca `Tree`/`stageFor` por um tijolo SVG que enche com o tempo (sem importar `Tree.tsx`/`treeColor.ts`; usar `habitColor`).
modelo: sonnet · esforço: medium

## T8 — Web: formulário de ciclo
Arquivos: `web/src/features/cycle/CreateCycleForm.tsx`.
Dep.: T3. Pronto: select de semanas só pares (2–12, padrão 6) e o título/texto "6 semanas" deixa de ser fixo; campo "Meta de foco por semana (h)" (padrão 7; envia minutos); validação manual no estilo atual.
modelo: sonnet · esforço: low

## T9 — Web: ArchPanel, página do ciclo, galeria
Arquivos: `web/src/features/arch/ArchPanel.tsx` (novo), `web/src/pages/CyclePage.tsx`, `web/src/features/cycle/PastCycles.tsx`.
Dep.: T4, T5, T6. Pronto: arco do ciclo ativo entre o card "Dia X de Y" e o aviso de revisão; editar meta semanal (PUT) no ativo; `PastCycles` mostra mini-arcos (`small`) e abre `Dialog` com `ArchPanel`; estados loading/erro (`isLoadingError`)/vazio; ao encerrar o ciclo, mostrar o arco com a pedra-chave descendo (se o fluxo atual tiver estado de sucesso; senão, arco fechado).
modelo: sonnet · esforço: high

## T10 — Web: remover a floresta
Arquivos: `web/src/pages/ForestPage.tsx`, `web/src/features/forest/*` (apagar), `web/src/app/router.tsx`, `web/src/pages/ProfilePage.tsx`, `web/src/pages/StatsPage.tsx`, `web/src/api/types.ts` (remover `TreeStage` e `FocusSession.tree`).
Dep.: T2, T7, T9. Pronto: sem importações de `Tree`/`treeColor`; rota `/forest` removida; link do Perfil e "Ver floresta" da Estatísticas passam a levar ao arco (`/cycle`, texto "Ver arco do ciclo"); lint/build verdes.
modelo: sonnet · esforço: medium

## T11 — MCP
Arquivos: `Features/Mcp/ArcoTools.cs`, `tests/Arco.Api.Tests/McpTests.cs`.
Dep.: T4. Pronto: `ver_arco(cicloId?)`; `Assert.Subset` e um teste que chama a ferramenta; `ver_floresta` permanece (lista de sessões).
modelo: sonnet · esforço: low

## T12 — Documentação
Arquivos: `docs/API.md`, `docs/PRODUCT.md`, `docs/repo_map.md`.
Dep.: Contratos. Pronto: API.md: ciclo (`weeks` par 2–12, `weeklyFocusMinutes`, `Cycle.weeks`), novo `GET /api/cycles/{id}/arch`, "Foco e floresta" sem `tree`, lista MCP com `ver_arco`; PRODUCT.md: diagrama/L15 (par, 2–12), seção "Foco e floresta" vira "Foco e arco" (pedra por semana, meta por ciclo, pedra-chave, tijolos por dia, pedra do dia, layout Row para ciclos ímpares); repo_map: módulo `features/arch`, rota.
modelo: sonnet · esforço: low

## Ondas
1. **Onda 1** ✅ concluída: T1, T2, T3. Verificação: `dotnet build` + `dotnet test`; `cd web && npm run lint && npm run build`.
2. **Onda 2** ✅ concluída: T4, T5, T6, T7, T8. Verificação: `dotnet test`; `cd web && npm run lint && npm run build`.
3. **Onda 3** ✅ concluída: T9, T10, T11, T12. Verificação final: `dotnet test` completo; `cd web && npm run lint && npm run build`; `grep -r "treeColor\|TreeStage\|features/forest" web/src src docs` sem ocorrências.

## Verificação ponta a ponta (manual, após a onda 3)
- `dotnet run --project src/Arco.Api` e `cd web && npm run dev`.
- Criar ciclo (6 semanas, meta 7 h): tentar 3 semanas é recusado. Registrar sessões pelo timer e por minutos no check-in; ver pedra encher e o tijolo aparecer no dia certo e na pedra do dia em Hoje.
- Alterar a meta e ver a proporção das faixas mudar. Encerrar o ciclo: pedra-chave encaixa; ele aparece em "Ciclos anteriores" e abre em diálogo.
- Ciclo com `endDate` editado para ímpar: layout Row. `GET /api/cycles/{id}/arch` com chave de API e `ver_arco` via MCP.

## Riscos
- Muda o limite de criação (era 1–12): documentado; ciclos existentes seguem funcionando (Row).
- `T5` toca testes antigos só para trocar semanas ímpares; se algum teste depender de `weeks=1/3`, o executor ajusta o valor, não a regra.
- Sem testes de frontend no projeto: a validação visual é manual (lint/build apenas).
