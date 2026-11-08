# Plano A (backend): issues do Arco

Sessão paralela. Outra sessão executa o outro plano ao mesmo tempo em outro worktree: não mexa fora de `D:/Projetos/Arco/arco-a`.
Caminhos curtos (`Domain/…`, `Infrastructure/…`, `Features/…`) são relativos a `src/Arco.Api/`. Portas E2E deste plano: `E2E_API_PORT=5299 E2E_WEB_PORT=4299`.

## Como executar
Para cada onda, em ordem:
1. Dispare todos os agentes `executor` da onda numa única mensagem, em paralelo, com `model` e `effort` da tarefa. Prompt: "Execute a tarefa T<n> do PLAN.md (leia só a seção da tarefa, os Contratos e as Regras comuns). Relatório em no máximo 10 linhas."
2. Aguarde todos. Rode a verificação da onda e mostre na conversa só erros ou as últimas linhas.
3. Se falhar, corrija antes de seguir. Faça o commit da onda, marque a onda como concluída neste arquivo e escreva: "Onda <n> concluída: verificação passou."
Se uma tarefa precisar editar arquivo fora da lista dela, o executor para e reporta. Não peça confirmações: pare só por erro que não consiga corrigir.
Ao fim, siga "Encerramento de cada plano" e resuma o que foi feito.

## Regras comuns (vão nos dois PLAN.md)
- **Portas E2E:** Plano A usa `E2E_API_PORT=5299 E2E_WEB_PORT=4299`; Plano B usa `5399/4399`.
- **Commits:** um por onda, depois da verificação. `git add` sem incluir `PLAN.md` nem `.claude/`. Nenhuma atribuição a IA no commit (sem `Co-Authored-By`). Use `Closes #N` quando a issue estiver completa e `Refs #N` quando sobrar parte manual.
- **Só um executor por onda roda Playwright.**
- **Arquivos compartilhados entre A e B:**
  - `docs/DEPLOY.md`: A só acrescenta linhas na tabela do §2; B só acrescenta uma seção nova no fim.
  - `docs/repo_map.md`: cada plano só acrescenta ou altera linhas da própria área.
  - `docs/PRODUCT.md`: A acrescenta o aviso ao parceiro; B só mexe em listas de ferramentas MCP, se houver.
- **Encerramento de cada plano:**
  1. `git fetch origin && git rebase origin/main`. Em conflito de docs, manter os dois lados. O commit da `plano-base` some sozinho se o outro plano já o tiver levado.
  2. Rodar a verificação final.
  3. `git push origin HEAD:main`. Se for rejeitado, voltar ao passo 1.
  4. Mover o `PLAN.md` para `D:\Projetos\Arco\arco\.claude\plans\2026-11-08-issues-plano-<a|b>.md` (pasta da cópia principal, porque `.claude` não é versionado).
- **Verificação final:** `dotnet test`; `cd web && npm run lint && npm run build`; `npm run e2e` com as portas do plano; `dotnet publish src/Arco.Api -c Release -r win-x86 --self-contained`.

---

# PLANO A: backend

## Contratos
```csharp
// Domain/Entities.cs (não é IUserOwned)
class OutboxMessage { long Id; string Kind; string Payload; DateTime CreatedAt; DateTime NextAttemptAt;
  int Attempts; DateTime? LockedUntil; DateTime? SentAt; DateTime? FailedAt; string? LastError; }
// Infrastructure/Outbox
static class OutboxKinds { const string Email = "email"; const string WeekClosed = "week_closed"; }
static long/void Outbox.Enqueue(AppDbContext db, string kind, object payload);  // não salva; entra no SaveChanges da ação
interface IOutboxHandler { string Kind { get; } Task HandleAsync(OutboxMessage m, CancellationToken ct); }
class OutboxProcessor { Task<int> RunOnceAsync(ct); Task TryDispatchAsync(long id, ct); }
// Infrastructure/Email: falha agora lança EmailSendException
Task SendAsync(string to, string subject, string html, CancellationToken ct, string? idempotencyKey = null);
// Infrastructure/Events
record WeekClosed(Guid/… OwnerUserId, DateOnly WeekStart);   // usar o tipo real de User.Id
interface IEventBus { Task PublishAsync(WeekClosed e, CancellationToken ct); }
class WeekClosedConsumer { Task HandleAsync(WeekClosed e, CancellationToken ct); }
// Infrastructure/Observability/Telemetry.cs
static ActivitySource Source; static Meter; Counter CheckIns, PushSent, PushFailed, PlanLimitBlocked;
```
- Claim atômico por `UPDATE … SET LockedUntil WHERE LockedUntil IS NULL OR LockedUntil < now`, para o envio na hora e o worker não enviarem em dobro.
- Backoff: 1 min, 5 min, 30 min, 2 h, 12 h; `FailedAt` depois da 6ª tentativa.
- **O payload de e-mail contém token de redefinição em claro:** ao concluir, gravar `Payload = ""`. Limpar enviados com mais de 30 dias.
- Worker: flag `Outbox:WorkerEnabled` (false no `ApiFactory`), ciclo de 30 s no modelo de `ReminderDispatcher.cs:155-172`, com `TimeProvider`.

## T1: pacotes
- Arquivos: `src/Arco.Api/Arco.Api.csproj`
- Adicionar: `Microsoft.Extensions.Http.Resilience`, `Azure.Monitor.OpenTelemetry.AspNetCore`, `OpenTelemetry.Instrumentation.EntityFrameworkCore`, `Dapper`, `Azure.Messaging.ServiceBus` (versões estáveis compatíveis com .NET 10).
- Pronto: `dotnet restore` e `dotnet publish -r win-x86` passam.
- modelo: sonnet · esforço: low

## T2: outbox (#11)
- Arquivos: `Domain/Entities.cs`, `Infrastructure/AppDbContext.cs`, `Infrastructure/Migrations/*` (migration `Outbox`), `Infrastructure/Outbox/*` (novos), `Infrastructure/Startup.cs` (só bloco de hosted services, :92-100), `Infrastructure/Email/IEmailSender.cs`, `Infrastructure/Email/ResendEmailSender.cs`, `Infrastructure/Email/LogEmailSender.cs`, `Features/Auth/AuthEndpoints.cs`, `tests/Arco.Api.Tests/ApiFactory.cs`, `tests/Arco.Api.Tests/OutboxTests.cs` (novo)
- `AuthEndpoints` (:117, :236, :251-255, :356-377): `Outbox.Enqueue` no mesmo `SaveChanges` do token e, depois de salvar, `TryDispatchAsync` (mantém o envio imediato e os testes atuais de `FakeEmailSender.Last`). Remover `TrySendAsync`.
- `ResendEmailSender` lança em não-2xx e envia `Idempotency-Key: outbox-{id}`. `FakeEmailSender` ganha modo de falha.
- Pronto: `OutboxTests` cobre falha seguida de sucesso, backoff, desistência, claim sem envio duplo, limpeza e payload apagado; `dotnet test` passa.
- modelo: sonnet · esforço: high

## T3: resiliência (#7)
- Arquivos: `Infrastructure/Resilience.cs` (novo), `Infrastructure/Startup.cs` (:90), `Infrastructure/Email/EmailServiceCollectionExtensions.cs`, `Infrastructure/Push/WebPushSender.cs`, `Infrastructure/Push/PushNotifier.cs`, `tests/Arco.Api.Tests/ResilienceTests.cs` (novo)
- `IHttpClientBuilder.AddArcoResilience(TimeSpan? baseDelay = null)`: `AddStandardResilienceHandler` + `.SelectPipelineByAuthority()`. Remover o `c.Timeout = 15s`, que cortaria as tentativas (`TotalRequestTimeout` assume, ~25 s, tentativa ~8 s; `SamplingDuration ≥ 2×` o timeout da tentativa).
- 404/410 não entram em retry nem contam no breaker (conferir o predicado padrão). Retry de POST permitido: o Resend tem Idempotency-Key.
- `PushNotifier.cs:28-37`: capturar também `BrokenCircuitException` e `TimeoutRejectedException`, sem incrementar `FailureCount` nesses casos.
- Pronto: teste com handler falso: 5xx seguidos abrem o circuito; 410 faz 1 chamada só; outro host segue fechado.
- modelo: sonnet · esforço: high

## T4: Dapper no admin (#10)
- Arquivos: `Features/Admin/AdminEndpoints.cs`, `tests/Arco.Api.Tests/AdminApiTests.cs`
- SQL via `db.Database.GetDbConnection()`, resposta idêntica. `CheckInsByDay` continua contando usuários distintos. Enums como texto (`'Pro'`, `'Missed'`); `DateOnly` e `DateTime` chegam como TEXT, então selecionar string e converter em UTC.
- Pronto: testes do admin passam, mais um cobrindo coorte de retenção com valores conhecidos.
- modelo: sonnet · esforço: medium

## T5: OpenTelemetry (#8)
- Arquivos: `Infrastructure/Observability/Telemetry.cs` (novo), `Infrastructure/Startup.cs`, `Features/Push/ReminderDispatcher.cs`, `Infrastructure/Push/PushNotifier.cs`, `Infrastructure/Outbox/*`, `Features/CheckIns/CheckInEndpoints.cs`, `Infrastructure/PlanGate.cs`, `docs/DEPLOY.md` (linha na tabela §2), `tests/Arco.Api.Tests/TelemetryTests.cs` (novo)
- Só ativa com `APPLICATIONINSIGHTS_CONNECTION_STRING`. Traces de requisição, HttpClient e EF, sem texto de SQL.
- PII: processor remove `url.query`, `client.address`, `http.client_ip`, `user_agent.original`, `enduser.*`. Auditar `Log*` para não exportar e-mail ou IP.
- Contadores: `CheckInEndpoints` (:105, só na criação), `PlanGate.Limit`, resultado em `PushNotifier`.
- Pronto: sem a variável, nada é registrado; teste com `MeterListener` cobre o contador de check-in.
- modelo: sonnet · esforço: medium

## T6: aviso ao parceiro (#12)
- Arquivos: `Infrastructure/Events/*` (novos), `Infrastructure/Outbox/*`, `Infrastructure/Startup.cs`, `Features/Reviews/ReviewEndpoints.cs`, `docs/DEPLOY.md` (linha na tabela §2), `tests/Arco.Api.Tests/ApiFactory.cs`, `tests/Arco.Api.Tests/WeekClosedTests.cs` (novo)
- "Fechar a semana" = `POST /api/reviews/weekly` (`ReviewEndpoints.cs:96`): `Outbox.Enqueue(WeekClosed)` no mesmo `SaveChanges`.
- `IEventBus`: em memória (chama o consumidor) ou Service Bus (fila `week-closed`, sender + processor hospedado) só com `ServiceBus:ConnectionString`.
- Consumidor: para cada `Partnership` aceita, `Impersonate(partnerId)`, dedupe com `PushDispatch` chave `week-closed:{owner}:{weekStart}` (padrão de `ReminderDispatcher.TryClaimAsync`, sem tabela nova). Push se houver inscrição; senão e-mail via `Outbox`. Texto conforme `User.Locale`.
- Pronto: testes cobrem push, e-mail sem inscrição, reentrega sem duplicar, conta sem parceiro.
- modelo: sonnet · esforço: high

## T7: docs do backend
- Arquivos: `docs/repo_map.md`, `docs/PRODUCT.md`, `CLAUDE.md`
- Registrar outbox, resiliência, telemetria e eventos; regra: e-mail novo passa pelo `Outbox`.
- Pronto: docs refletem o código.
- modelo: sonnet · esforço: low

## Ondas
- [x] Onda 1: T1, T2
- [x] Onda 2: T3, T4
- [x] Onda 3: T5
- [x] Onda 4: T6, T7

## Verificação por onda
`dotnet build && dotnet test`. Na onda 1, também `dotnet publish src/Arco.Api -c Release -r win-x86 --self-contained`.

