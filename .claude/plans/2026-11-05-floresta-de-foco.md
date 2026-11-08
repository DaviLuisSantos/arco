# Floresta de foco — plano

## Contexto
O Arco não guarda tempo: o check-in é Feito/Mínimo/Não fiz e a "dose" é texto livre. O usuário quer ver o progresso de forma visual e estatística (horas por hábito), como um app de foco que planta uma árvore a cada sessão. Decisões dele: tempo vem do **timer e dos minutos no check-in**; visual = **floresta por hábito**.

Princípios: tabela única `FocusSession` (uma fonte da verdade para horas); **sem XP** (não mexe no livro-razão); tudo pela API (e MCP), como manda o CLAUDE.md.

## Contratos (compartilhados)

**Entidade** `FocusSession`: `Id, HabitId (FK Habit, cascade), Date (DateOnly, dia local), Minutes (1–480), Source (Timer|Manual, string no banco, max 16), StartedAt (DateTime? UTC), CreatedAt`. Índice `(HabitId, Date)`. Enum `FocusSource { Timer, Manual }` em `Domain/Enums.cs`.

**Regra pura** `Domain/FocusRules.cs`: `MaxMinutes = 480`; `TreeStage(int minutes)` → `TreeStage { Sprout (1–14), Small (15–29), Tree (30–59), Large (60+) }`.

**DTO** `FocusSessionDto(int Id, int HabitId, DateOnly Date, int Minutes, FocusSource Source, DateTime? StartedAt, TreeStage Tree)`.

**Rotas** (`Features/Focus/FocusEndpoints.cs`, `MapFocus` em `Startup.cs`):
- `POST /api/focus-sessions` body `{ habitId, minutes, date?, startedAt? }`. `date` padrão = hoje (AppClock), no máx. 7 dias atrás, nunca futura (mesmas regras do check-in). Hábito arquivado → validação. Cria sessão `Timer`. Resposta `{ session: FocusSessionDto }`.
- `DELETE /api/focus-sessions/{id}` (mesmo padrão dos deletes de `GoalEndpoints`). Só remove; sem XP.
- `GET /api/focus-sessions?from&to&habitId?` (máx. 366 dias, mesmas mensagens do `GET /checkins`) → `{ sessions: FocusSessionDto[], totalMinutes, byHabit: [{ habitId, name, emoji, minutes, sessions }] }`.

**Check-in** `PUT /api/checkins` ganha `minutes?: int` (0–480): ausente = não mexe; `0` = remove a sessão Manual do dia; `>0` = cria/substitui a **única** sessão Manual de (hábito, data). `minutes > 0` com status `Missed` → validação (pt-BR). `DELETE /api/checkins/{habitId}/{date}` remove também a sessão Manual (Timer permanece). `CheckInDto` ganha `int? Minutes` (minutos manuais do dia; atualizar `From` e a projeção de `GET /checkins`).

**Estatísticas** (aditivo): `Minutes` (soma de todas as sessões) em `HabitPeriodStatsDto`, `WeekStatsDto` e `PeriodDto`. Reaparece sozinho em ciclo e revisão semanal.

**MCP**: `registrar_foco(habitId, minutos, data?)` e `ver_floresta(de?, ate?, habitId?)` (somente leitura).

**Web (tipos)**: `FocusSession`, `TreeStage`, `FocusList`; `CheckInInput.minutes?`, `CheckIn.minutes`; `focusApi.{list,create,remove}`; `queryKeys.focus(from,to)`; `formatDuration(min)` em `lib/format.ts` ("1 h 25 min", "45 min").

**UX**:
- *Timer*: seção na página do hábito. Presets 15/25/45/60 min (contagem regressiva) ou cronômetro livre. Estado em `localStorage` com `startedAt` (sobrevive a recarregar/trocar de aba; calcula por timestamp, não por intervalo). Ao acabar ou "Concluir agora", salva os minutos decorridos (≥1), sem punição. Falha de rede: guarda como pendente e oferece "Tentar de novo". Depois de salvar, oferece "Marcar como Feito".
- *Árvore*: SVG de 4 estágios; cor (matiz) determinística pelo `habitId`; emoji do hábito no tronco/legenda. Sem campo de cor novo.
- *Floresta* `/forest`: período (Semana/Mês/Ano), total de horas, grade de árvores agrupadas por dia, legenda com horas por hábito. Link no Perfil (ao lado de Estatísticas); NAV de 4 colunas não muda.
- Textos em pt-BR; datas via `lib/dates.ts`.

## Tarefas

## T1 — Domínio, EF e migration
Arquivos: `Domain/Entities.cs`, `Domain/Enums.cs`, `Domain/FocusRules.cs` (novo), `Infrastructure/AppDbContext.cs`, `Infrastructure/Migrations/*FocusSessions*` (gerada), `tests/Arco.Api.Tests/FocusRulesDomainTests.cs` (novo).
Dep.: nenhuma. Pronto: `dotnet ef migrations add FocusSessions` (comando do README) gerada, testes de `TreeStage` nos limites 1/14/15/29/30/59/60. Config EF no padrão de `CheckIn` (enum→string).
modelo: sonnet · esforço: medium

## T2 — Web: contratos
Arquivos: `web/src/api/types.ts`, `web/src/api/endpoints.ts`, `web/src/lib/queryKeys.ts`, `web/src/lib/format.ts`.
Dep.: nenhuma. Pronto: tipos/endpoints/chaves/`formatDuration` conforme Contratos; `npm run build` passa.
modelo: sonnet · esforço: low

## T3 — Web: componente Tree (SVG)
Arquivos: `web/src/features/forest/Tree.tsx` (novo), `web/src/features/forest/treeColor.ts` (novo).
Dep.: nenhuma (tipo `TreeStage` declarado localmente até T2 existir; depois importar). Pronto: `<Tree stage hue size />`, 4 estágios, legível em tema claro/escuro (tokens do `index.css`), `aria-label`.
modelo: sonnet · esforço: medium · skills: artifact-design não; usar só os tokens do projeto.

## T4 — API de sessões de foco
Arquivos: `Features/Focus/FocusEndpoints.cs` (novo), `Infrastructure/Startup.cs` (só `secured.MapFocus()`), `tests/Arco.Api.Tests/FocusApiTests.cs` (novo).
Dep.: T1. Pronto: rotas dos Contratos; testes de integração (criar, data futura/>7 dias/arquivado rejeitados, listar com `byHabit`, excluir, 401 sem login, funciona com chave de API).
modelo: sonnet · esforço: medium

## T5 — Minutos no check-in
Arquivos: `Features/CheckIns/CheckInEndpoints.cs`, `Features/Shared/Dtos.cs`, `tests/Arco.Api.Tests/CheckInMinutesApiTests.cs` (novo).
Dep.: T1. Pronto: semântica de `minutes` (ausente/0/>0, Missed rejeitado), DELETE remove Manual e preserva Timer, mesma transação do check-in; `CheckInDto.Minutes`; testes.
modelo: sonnet · esforço: high (transação, offline reenviando o mesmo PUT deve ser idempotente)

## T6 — Estatísticas com minutos
Arquivos: `Features/Reviews/ReviewStats.cs`, `Features/Stats/StatsEndpoints.cs`, `tests/Arco.Api.Tests/StatsFocusTests.cs` (novo).
Dep.: T1. Pronto: `Minutes` nos três DTOs, respeitando os mesmos limites de intervalo (semana atual só até hoje); fixar construtores usados por `CycleEndpoints`/`ReviewEndpoints` sem editá-los (se precisar editar, parar e reportar); testes.
modelo: sonnet · esforço: medium

## T7 — Web: timer de foco
Arquivos: `web/src/features/focus/FocusTimer.tsx` (novo), `web/src/features/focus/useFocusTimer.ts` (novo), `web/src/pages/HabitDetailPage.tsx` (nova seção entre "Dose atual" e "Histórico").
Dep.: T2, T3. Pronto: UX do timer dos Contratos; invalida `focus`, `stats`; mutação com `networkMode: 'always'` (padrão das não-check-in); pendente em `localStorage` com try/catch.
modelo: sonnet · esforço: high

## T8 — Web: página Floresta
Arquivos: `web/src/pages/ForestPage.tsx` (novo), `web/src/features/forest/Forest.tsx` (novo), `web/src/features/forest/ForestLegend.tsx` (novo), `web/src/app/router.tsx`, `web/src/pages/ProfilePage.tsx`.
Dep.: T2, T3. Pronto: rota `/forest`, estados loading/erro (`isLoadingError`)/vazio ("Sua floresta começa na primeira sessão"), excluir sessão com `ConfirmDialog`, link no Perfil.
modelo: sonnet · esforço: medium

## T9 — Web: minutos no check-in
Arquivos: `web/src/features/checkins/HabitCheckInCard.tsx`, `checkInMutation.ts`, `useCheckIn.ts`, `web/src/app/queryClient.ts` (só se o `buster` precisar mudar).
Dep.: T2, T5. Pronto: campo opcional "Quantos minutos?" após marcar Feito/Mínimo; `minutes` serializável na fila offline e na atualização otimista; formato de cache compatível (se não, trocar `buster`).
modelo: sonnet · esforço: high

## T10 — Web: horas nas Estatísticas
Arquivos: `web/src/pages/StatsPage.tsx`, `web/src/features/stats/WeeklyChart.tsx`, `web/src/features/reviews/PeriodStats.tsx`.
Dep.: T2, T6. Pronto: terceira medida "Horas" no gráfico semanal; horas por hábito (e total) nos últimos 30 dias; link para `/forest`.
modelo: sonnet · esforço: medium

## T11 — MCP
Arquivos: `Features/Mcp/ArcoTools.cs`, `tests/Arco.Api.Tests/McpTests.cs`.
Dep.: T4, T5. Pronto: `registrar_foco` e `ver_floresta` no padrão de `fazer_checkin`/`ver_estatisticas`; `Assert.Subset` e um teste atualizados; `fazer_checkin` aceita `minutos?`.
modelo: sonnet · esforço: low

## T12 — Documentação
Arquivos: `docs/API.md`, `docs/PRODUCT.md`, `docs/repo_map.md`.
Dep.: T4, T5, T6, T11 (contratos). Pronto: seção "Foco e floresta" em API.md (rotas, `minutes`, `Minutes` nas stats, lista de ferramentas MCP), PRODUCT.md (regras: sem XP, árvore por duração, minutos manuais únicos por dia, timer sem punição), repo_map (módulo Focus, página Floresta).
modelo: sonnet · esforço: low

## Ondas
1. **Onda 1** ✅ concluída: T1, T2, T3. Verificação: `dotnet build` + `dotnet test --filter FullyQualifiedName~DomainTests`; `cd web && npm run lint && npm run build`.
2. **Onda 2** ✅ concluída: T4, T5, T6, T7, T8. Verificação: `dotnet test`; `cd web && npm run lint && npm run build`.
3. **Onda 3** ✅ concluída: T9, T10, T11, T12, T13. Verificação final: `dotnet test` completo; `cd web && npm run lint && npm run build`.

## Verificação ponta a ponta (manual, após a onda 3)
- `dotnet run --project src/Arco.Api` (senha "arco" em Development; porta 5080 pode estar ocupada) e `cd web && npm run dev`.
- Criar hábito → rodar timer curto (ajustar preset para teste) → árvore aparece em `/forest`; recarregar durante o timer mantém a contagem.
- Check-in Feito com 30 min → soma em Estatísticas (horas por hábito); refazer o mesmo check-in não duplica; desfazer remove os minutos.
- Chamar `GET /api/forest`-equivalente (`/api/focus-sessions`) com chave de API e as ferramentas MCP.

## Riscos
- Duplicar horas se o usuário usar timer e minutos manuais no mesmo dia: soma é intencional; documentado.
- Contrato de `/stats`, ciclo e revisão muda de forma aditiva (campo `Minutes`): cache offline antigo apenas não terá o campo; UI tolera `undefined`.
- Conquistas de foco ficam fora desta entrega (seguimento opcional).

## T13 — Minutos manuais em Hoje e no detalhe do hábito (adicionada após a onda 2)
Arquivos: `Features/Today/TodayEndpoints.cs`, `Features/Habits/HabitEndpoints.cs`, `tests/Arco.Api.Tests/TodayMinutesApiTests.cs` (novo).
Dep.: T5. Pronto: `checkIn.minutes` (minutos da sessão Manual do dia, ou null) em `GET /today` e em `recentCheckIns` do detalhe do hábito, sem N+1 (uma consulta); testes.
modelo: sonnet · esforço: medium
