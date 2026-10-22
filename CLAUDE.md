# CLAUDE.md

Arco: app pessoal (single-user) de hábitos com progressão. Leia `docs/PRODUCT.md` (conceito, regras de XP) e `docs/API.md` (contrato da API) antes de mudar comportamento. **Mantenha o API.md atualizado** a cada mudança de contrato.

## Layout
- `src/Arco.Api`: .NET 10 Minimal APIs, organizado em fatias.
  - `Features/<Feature>/` contém o endpoint e a lógica de cada funcionalidade.
  - `Domain/` tem entidades e regras puras e testáveis: `XpRules`, `PlayerLevel`, `HabitSchedule`, `GoalProgress`, `CycleMath`.
  - `Infrastructure/` tem DbContext, migrations, clock, auth e `ApiResults`.
  - Não há MediatR nem repositórios.
- `tests/Arco.Api.Tests`: xUnit, com testes unitários de domínio e de integração (WebApplicationFactory + SQLite temporário).
- `web/`: React 19 + Vite + TS + TanStack Query + React Router 7 + Tailwind v4 + vite-plugin-pwa.
  - `src/api` é o cliente tipado, espelho do API.md.
  - `src/features` concentra a lógica por domínio.
  - `src/components/ui` tem as primitivas visuais.
- Produção: um único container (`Dockerfile`) em que o ASP.NET serve a API e `wwwroot` (o build do web). O Caddy faz o HTTPS (`docker-compose.yml`).

## Comandos
- API: `dotnet run --project src/Arco.Api`, em :5080. Em Development, a senha é "arco".
- Web: `cd web && npm run dev`, em :5173, com proxy de `/api`.
- Testes: `dotnet test`. Web: `npm run lint` (oxlint) e `npm run build`.
- Hash de senha: `dotnet run --project src/Arco.Api -- hash-password <senha>`.

## Regras importantes
- **Datas:** "hoje" vem sempre do fuso em `Settings` (padrão `America/Sao_Paulo`), via o clock da Infrastructure. Nunca derive a data do usuário de `DateTime.UtcNow`. Datas de calendário usam `DateOnly` e `"YYYY-MM-DD"`; todo `DateTime` é UTC (há um converter no DbContext).
- **Dias da semana:** 0 = domingo … 6 = sábado. A semana do `WeeklyTarget` começa na segunda.
- **XP:** é um livro-razão (`XpEvent`). Toda ação que dá ou tira XP grava o evento na mesma transação, e as respostas devolvem `{ xpDelta, player }`.
- **Erros:** o formato é `{ error, code }`, com mensagens em pt-BR. `/api/*` nunca redireciona: devolve 401 em JSON.
- **Frontend:** nunca use `new Date("YYYY-MM-DD")`; use `web/src/lib/dates.ts`. O service worker nunca cacheia `/api`.
- **UI:** todo o texto é em pt-BR.
