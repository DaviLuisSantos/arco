# Mapa do repositório

## Pontos de entrada
- `src/Arco.Api/Program.cs`: CLI `hash-password` ou sobe a API.
- `src/Arco.Api/Infrastructure/Startup.cs`: `AddArco` (DI, auth, hosted services), `ApplyMigrations`, `UseArco`, `MapArco` (registra todas as rotas).
- `web/src/main.tsx` → `web/src/app/router.tsx` (rotas: `/`, habits, habits/:id, cycle, profile, review, stats, login, register, forgot-password, reset-password, verify-email).
- `web/src/sw.ts`: service worker (push; nunca cacheia `/api`).

## Módulos
| Onde | O quê |
|---|---|
| `src/Arco.Api/Domain/` | Entidades (`Entities.cs`, `Enums.cs`) e regras puras: XP, nível, agenda, streak, ciclos, conquistas, sugestões de nível, revisão semanal |
| `src/Arco.Api/Features/<X>/` | Um arquivo de endpoints por funcionalidade (Today, CheckIns, Habits, Cycles, Goals, Player, Stats, Reviews, Achievements, Focus, Settings, Push, Backup (export JSON), ApiKeys, Auth, Demo, Health) |
| `Features/Focus/` + `Domain/FocusRules.cs` | Sessões de foco (`/api/focus-sessions`) ; minutos manuais vêm do check-in |
| `Features/Cycles/CycleArchEndpoints.cs` | `GET /api/cycles/{id}/arch` (pedras por semana) |
| `web/src/features/focus`, `web/src/features/arch` | Timer de foco e o arco do ciclo (rota `/cycle`) |
| `Features/Shared/` | DTOs e consultas reaproveitadas |
| `Features/Mcp/` | Servidor MCP (`ArcoTools` chama a API REST via `ArcoApiClient`) |
| `Features/Push/ReminderDispatcher.cs` | Lembretes (`ReminderService`, 1×/min) |
| `src/Arco.Api/Infrastructure/` | DbContext, migrations, `AppClock`, auth por cookie/chave, `ApiResults`, backup, Web Push, `CurrentUser` (filtro por usuário), `OwnerBootstrap`, `Email/` (Resend ou log) |
| `tests/Arco.Api.Tests/` | `*DomainTests` (unitários), `*ApiTests`/`McpTests` (integração com `ApiFactory`) |
| `web/src/api/` | Cliente tipado (espelho do `docs/API.md`) |
| `web/src/features/` | Componentes e hooks por domínio |
| `web/src/pages/` | Uma página por rota |
| `web/src/lib/` | Datas, erros, mutações, IndexedDB, push |
| `web/src/components/ui/` | Primitivas visuais |

## Testes e checagens
- `dotnet test` (tudo) · `dotnet test --filter FullyQualifiedName~DomainTests` (só domínio, rápido)
- `cd web && npm run lint && npm run build`
- CI: `.github/workflows/deploy.yml` (roda os dois em todo PR)

## Áreas sensíveis
- **XP e conquistas:** `XpRules`, `XpEvent`, `AchievementService`. Livro-razão: gravar na mesma transação e revogar ao excluir.
- **Datas:** `AppClock` e `web/src/lib/dates.ts`. Nunca usar `DateTime.UtcNow` nem `new Date("YYYY-MM-DD")`.
- **Migrations:** `Infrastructure/Migrations/` (aplicadas no startup; não edite as antigas).
- **Auth:** `ApiKeyAuthentication.cs`, `Features/Auth/` (`AuthEndpoints`, `AuthSession`, `AuthTokens`, `GoogleAuth`), `CurrentUser.cs`, `OwnerBootstrap.cs`.
- **Push:** `Infrastructure/Push/` (criptografia própria, sem pacote externo).
- **Offline:** `features/checkins/checkInMutation.ts`, `app/queryClient.ts` (`buster`).
- **Deploy:** `deploy.yml` (win-x86, plano Free), `Dockerfile`.
- **Contrato:** mudou rota ou DTO? Atualize `docs/API.md` e `web/src/api/`.

## Ler primeiro
1. `docs/PRODUCT.md` (regras) · 2. `docs/API.md` (só a seção da feature) · 3. `Startup.cs` · 4. o `Features/<X>/` e o `web/src/features/<x>/` da tarefa
