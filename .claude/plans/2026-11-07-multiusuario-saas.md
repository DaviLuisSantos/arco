# Plano: Arco multiusuário (contas, isolamento, e-mail, Google, demo)

## Contexto
Hoje o Arco é single-user: login só por senha (`Auth:PasswordHash`), claim `Name="owner"` e nenhum `UserId` nas tabelas. Para o portfólio, ele passa a ser um SaaS funcional:
- cadastro aberto com e-mail e senha, login com Google e conta demo;
- verificação de e-mail que não bloqueia o uso (só um banner);
- recuperação de senha por Resend;
- dados isolados por usuário.

Cobrança fica fora. O dono atual não perde nada: os dados existentes passam para o usuário 1.

## Decisões
- **Isolamento:** `UserId` em todas as entidades (`IUserOwned`) e global query filter no `AppDbContext` com `UserId == CurrentUser.Id`. O `SaveChanges` preenche o `UserId` dos Added e lança exceção sem usuário. Jobs de fundo usam `IgnoreQueryFilters()` para listar usuários e `CurrentUser.Impersonate(id)` num scope novo por usuário.
- **Sessão:** continua o cookie `arco_auth`, com estes claims:
  - `NameIdentifier=userId`;
  - `stamp=SecurityStamp`;
  - `amr` = `pwd` | `google` | `demo`.

  O `OnValidatePrincipal` rejeita a sessão se o usuário não existe mais ou se o stamp mudou (reset de senha derruba as outras sessões).
- **Chave de API:** resolve o usuário dono com `IgnoreQueryFilters` e emite o mesmo `NameIdentifier`. O MCP continua repassando o header, sem mudança.
- **Dono atual:** a migration cria `Users(Id=1, Email='owner@arco.local')` somente se já existirem dados, com `UserId=1` como default no backfill. Ao subir, o `OwnerBootstrap` faz o seguinte: se o usuário 1 tem o e-mail placeholder e há `Auth:OwnerEmail` + `Auth:PasswordHash` na config, grava os dois e marca o e-mail como verificado.
- **Backup:** a cópia diária `VACUUM INTO` continua global (operação). O `GET /api/backup` passa a devolver o export JSON dos dados do usuário, e o `GET /api/backups` é removido.
- **Demo:** `POST /api/auth/demo` cria um usuário `IsDemo` com dados de exemplo dos últimos 21 dias. Um serviço apaga as demos com mais de 24h.
- **E-mail:** `IEmailSender`, com Resend (`Email:ResendApiKey`, `Email:From`) e `LogEmailSender` quando não há chave. Os links usam `App:BaseUrl`.
- **Google:** `Microsoft.AspNetCore.Authentication.Google` (versão 10.x). Só fica ativo com `Auth:Google:ClientId` e `Auth:Google:ClientSecret`. Vincula por `GoogleSubject` ou por e-mail verificado; se não achar, cria o usuário já verificado.

## Contratos

### Domínio (`Domain/Entities.cs`)
```csharp
public interface IUserOwned { int UserId { get; set; } }
public class User { int Id; string Email /*lower, unique*/; string? PasswordHash; string? GoogleSubject /*unique*/;
  string DisplayName; DateTime? EmailVerifiedAt; bool IsDemo; Guid SecurityStamp; DateTime CreatedAt; }
public enum UserTokenPurpose { VerifyEmail, ResetPassword }
public class UserToken { int Id; int UserId; UserTokenPurpose Purpose; string TokenHash /*SHA-256*/; DateTime ExpiresAt; DateTime? UsedAt; }
// Todas as entidades atuais ganham UserId (FK User, cascade) e implementam IUserOwned.
// Settings perde SingletonId: Id autoincremento + UserId único. "Achar ou criar" via db.Settings.FirstOrDefaultAsync().
```
**Índices únicos:**
- passam a incluir `UserId`: `(UserId, Status)` do ciclo ativo (filtrado), `(UserId, WeekStart)`, `(UserId, Key)` de Achievement e `(UserId, Key)` de PushDispatch;
- não mudam: `PushSubscription.Endpoint` e `ApiKey.KeyHash`.

### Infra
```csharp
public sealed class CurrentUser { int Id /*0 = nenhum*/; bool IsSet; void Impersonate(int id); } // scoped; lê ClaimTypes.NameIdentifier do HttpContext
public interface IEmailSender { Task SendAsync(string to, string subject, string html, CancellationToken ct); }
public static class EmailTemplates { (string subject, string html) VerifyEmail(string link); ResetPassword(string link); } // pt-BR
public static IServiceCollection AddArcoEmail(this IServiceCollection s, IConfiguration c);
public static class DemoSeeder { Task SeedAsync(AppDbContext db, DateOnly today, CancellationToken ct); } // com CurrentUser já definido
public sealed class DemoCleanupService : BackgroundService // de hora em hora apaga IsDemo com CreatedAt < agora-24h
```

### API (`/api/auth`; erros `{ error, code }`)
| Rota | Corpo | Resposta |
|---|---|---|
| `POST /register` | `{ email, password, displayName? }` | 201 + cookie + e-mail de verificação; 409 `email_taken`; 400 `validation` (senha ≥ 8) |
| `POST /login` | `{ email, password }` | 204 + cookie; 401 `invalid_credentials` |
| `POST /logout` | — | 204 |
| `GET /me` | — | 200 `Me` ou 401 |
| `GET /providers` | — | 200 `{ google: bool }` |
| `POST /demo` | — | 201 + cookie |
| `POST /verify-email` | `{ token }` | 204; 400 `invalid_token` |
| `POST /resend-verification` | (autenticado) | 204 |
| `POST /forgot-password` | `{ email }` | sempre 204; só envia para e-mail verificado; token de 1h |
| `POST /reset-password` | `{ token, password }` | 204; troca o stamp |
| `POST /change-password` | `{ currentPassword?, newPassword }` | 204; 401 `invalid_credentials`; exige cookie |
| `DELETE /account` | `{ password? }` | 204; apaga o usuário e os dados em cascata; exige cookie |
| `GET /google?returnUrl=` | — | challenge |
| `/google/callback` | — | redireciona para `returnUrl` ou `/` |

- `Me` = `{ id, email, displayName, emailVerified, isDemo, hasPassword, hasGoogle }`.
- O token de verificação vale 48h.
- Rate limit por IP em `register`, `login`, `forgot-password`, `demo` e `resend-verification`.
- Rotas anônimas: todas as de auth, exceto `logout`, `resend-verification`, `change-password` e `account`.

### Web
- `web/src/api`: `authApi.{register, login, logout, me, providers, demo, verifyEmail, resendVerification, forgotPassword, resetPassword, changePassword, deleteAccount}` e `exportApi.download`; tipo `Me`.
- `useAuthStatus()` passa a devolver `Me | null`.

## T1 — Modelo multiusuário, filtro e migration
- **Descrição:** criar `User`, `UserToken`, `IUserOwned` e `UserId` em todas as entidades. Criar `CurrentUser` com o filtro global e o preenchimento no `SaveChanges`. Novos índices.
  - A migration `MultiUser` faz:
    1. backfill do usuário 1 somente se houver dados (`INSERT ... WHERE EXISTS` em Habits, Cycles ou XpEvents);
    2. `UserId` default 1;
    3. remover a seed do Settings singleton e apagar a linha de Settings se não houver usuário.
  - `OwnerBootstrap.Run(app)` chamado depois do `Migrate` (a chamada fica em T4).
  - Trocar os 4 usos de `Settings.SingletonId` por `FirstOrDefaultAsync` (com criação, se faltar).
- **Arquivos:** `Domain/Entities.cs`, `Domain/Enums.cs`, `Infrastructure/AppDbContext.cs`, `Infrastructure/CurrentUser.cs` (novo), `Infrastructure/OwnerBootstrap.cs` (novo), `Infrastructure/Migrations/*` (nova migration + snapshot), `Infrastructure/AppClock.cs`, `Features/Settings/SettingsEndpoints.cs`, `Features/Push/ReminderDispatcher.cs` (só a linha do Settings).
- **Dependências:** nenhuma.
- **Pronto quando:** `dotnet build` passa e a migration gerada com `dotnet ef migrations add MultiUser` foi revisada à mão (SQL de backfill).
- **Modelo/esforço:** `opus` / `high`.

## T2 — E-mail
- **Descrição:** `IEmailSender`, `ResendEmailSender` (HttpClient `POST https://api.resend.com/emails`, Bearer), `LogEmailSender`, `EmailTemplates` (HTML simples, pt-BR, marca Arco) e `AddArcoEmail`. A escolha do sender depende de `Email:ResendApiKey`.
- **Arquivos:** `Infrastructure/Email/*.cs` (novos).
- **Dependências:** nenhuma.
- **Pronto quando:** `dotnet build` passa.
- **Modelo/esforço:** `sonnet` / `low`.

## T3 — Web: cliente e telas de auth
- **Descrição:** tipos e endpoints do contrato Web, além dos códigos novos em `errors.ts`.
  - `LoginPage`: e-mail e senha, botão "Continuar com Google" (só se `providers.google`; navega para `/api/auth/google?returnUrl=`), "Experimentar demo" e links para cadastro e esqueci a senha.
  - Telas novas `RegisterPage`, `ForgotPasswordPage`, `ResetPasswordPage` (`?token=`) e `VerifyEmailPage` (`?token=`, funciona com ou sem sessão).
  - Rotas públicas no `router.tsx`.
  - `useAuthStatus` devolve `Me | null`. Ao logar, cadastrar ou entrar na demo, chamar `qc.clear()` antes de gravar o `Me`.
  - Validação manual no padrão atual (`Input error`), texto em pt-BR.
- **Arquivos:** `web/src/api/client.ts`, `web/src/api/endpoints.ts`, `web/src/api/types.ts` (ou equivalente), `web/src/lib/errors.ts`, `web/src/lib/useAuthStatus.ts`, `web/src/app/router.tsx`, `web/src/components/layout/RequireAuth.tsx`, `web/src/pages/LoginPage.tsx`, `web/src/pages/{Register,ForgotPassword,ResetPassword,VerifyEmail}Page.tsx` (novos).
- **Dependências:** nenhuma (só o contrato).
- **Pronto quando:** `npm run lint` e `npm run build` passam.
- **Modelo/esforço:** `sonnet` / `medium`.

## T4 — Auth backend, Google, stamp e wiring
- **Descrição:**
  - Reescrever `AuthEndpoints` conforme o contrato:
    - senha com BCrypt (work factor 11);
    - tokens com 32 bytes aleatórios em base64url, guardando o SHA-256;
    - links `{App:BaseUrl}/verify-email?token=` e `/reset-password?token=`.
  - `Startup`:
    - registrar `CurrentUser`, `AddArcoEmail`, o Google condicional (`SignInScheme` = cookie externo temporário e callback que faz o vínculo) e `OnValidatePrincipal` com o stamp;
    - registrar as políticas de rate limit, o `DemoCleanupService` (respeita `Demo:CleanupEnabled`) e o `OwnerBootstrap` depois do `Migrate`;
    - no startup, o `AchievementService.UnlockAsync` passa a rodar por usuário (impersonate) ou é removido.
  - `ApiKeyAuthentication` resolve o `UserId` da chave.
  - `ApiKeyEndpoints` passa a usar `User.UsedApiKey()` também em `change-password` e `account`.
  - Remover `PasswordVerifier`. O CLI `hash-password` continua (para `Auth:PasswordHash` do dono).
  - `/api/auth/demo` chama `DemoSeeder.SeedAsync`.
- **Arquivos:** `Features/Auth/*` (pode dividir em `AuthEndpoints.cs`, `GoogleAuth.cs`, `AuthTokens.cs`), `Infrastructure/Startup.cs`, `Infrastructure/ApiKeyAuthentication.cs`, `Infrastructure/PasswordVerifier.cs` (remover), `Program.cs`, `Arco.Api.csproj`.
- **Dependências:** T1, T2.
- **Pronto quando:** `dotnet build` passa; manualmente, register → me → logout → login funciona em Development.
- **Modelo/esforço:** `sonnet` / `high`.

## T5 — Features e jobs por usuário
- **Descrição:**
  - Auditar `Features/**` atrás de `IgnoreQueryFilters`, `FromSql`/`ExecuteSql`, `ExecuteDelete`/`ExecuteUpdate` e caches estáticos. Garantir que tudo respeita o filtro e que `Find`, `Include` e os filhos estão cobertos.
  - `ReminderDispatcher`: listar com `IgnoreQueryFilters` os usuários que têm `PushSubscription`; para cada um, abrir um scope novo, fazer o `Impersonate` e rodar a lógica atual (fuso do Settings do usuário).
  - `PushNotifier.SendToAllAsync` passa a enviar para as inscrições do usuário corrente.
  - A limpeza de `PushDispatch` continua global, com `IgnoreQueryFilters`.
  - `BackupEndpoints`: `GET /api/backup` passa a ser o export JSON do usuário (`arco-export-YYYY-MM-DD.json`, todas as entidades sem os hashes) e `/api/backups` sai. A cópia diária em `DatabaseBackup` não muda.
- **Arquivos:** `Features/Push/ReminderDispatcher.cs`, `Infrastructure/Push/PushNotifier.cs`, `Features/Backup/BackupEndpoints.cs`, `Features/Shared/*`, `Features/Achievements/AchievementEndpoints.cs` e outros `Features/*/*Endpoints.cs` só se a auditoria achar furo (reportar quais).
- **Dependências:** T1.
- **Pronto quando:** `dotnet build` passa e a lista do que foi auditado ou alterado está no relatório.
- **Modelo/esforço:** `sonnet` / `high`.

## T6 — Web: conta, banners, export e landing
- **Descrição:**
  - `AccountCard` no Perfil: e-mail e nome, trocar ou definir senha, status do Google e excluir conta com `ConfirmDialog`.
  - `BackupCard` vira "Exportar meus dados".
  - Banner "Confirme seu e-mail" com botão de reenviar no `AppLayout`, quando `!emailVerified && !isDemo`.
  - Banner da demo ("conta de demonstração, apagada em 24h · Criar conta").
  - O logout continua com `qc.clear()`.
  - Landing: CTA principal para `/login?mode=register` e um secundário "Experimentar demo" (`/login?demo=1`; a `LoginPage` dispara a demo ao ver o parâmetro, e o T3 já trata isso).
  - `buster` vai para `'v4'`.
- **Arquivos:** `web/src/pages/ProfilePage.tsx`, `web/src/features/account/*` (novos), `web/src/features/backup/BackupCard.tsx`, `web/src/components/layout/AppLayout.tsx`, `web/src/app/queryClient.ts`, `web/src/main.tsx` (trocar `false` por `null` em `queryKeys.auth`; o mesmo no ProfilePage), `web/public/landing/index.html`, remover `backupApi.list` de `web/src/api/endpoints.ts`.
- **Dependências:** T3.
- **Pronto quando:** lint e build passam.
- **Modelo/esforço:** `sonnet` / `medium`.

## T7 — Seeder e limpeza da demo
- **Descrição:**
  - `DemoSeeder`: um ciclo ativo (6 semanas, começou há 21 dias) e 2 objetivos (um numérico e um de marcos).
  - 4 hábitos, com níveis, um com escada planejada e um com regra de semanas.
  - Check-ins dos últimos 21 dias com ~75% de acerto, algumas sessões de foco e 2 revisões semanais.
  - XpEvents coerentes com `XpRules`. Chamar `AchievementService.UnlockAsync`.
  - `DemoCleanupService`: apagar com `IgnoreQueryFilters` e em cascata.
- **Arquivos:** `Features/Demo/DemoSeeder.cs`, `Features/Demo/DemoCleanupService.cs` (novos).
- **Dependências:** T1.
- **Pronto quando:** `dotnet build` passa.
- **Modelo/esforço:** `sonnet` / `medium`.

## T8 — Testes
- **Descrição:**
  - `ApiFactory`:
    - `LoggedInClientAsync(string? email = null)` cadastra um usuário único;
    - `FakeEmailSender` captura os links;
    - `Demo:CleanupEnabled=false`.
  - Ajustar `ApiTests` (login por e-mail).
  - Testes novos em `AuthApiTests`:
    - register, duplicado e login;
    - verify-email, forgot/reset (inclusive o stamp derrubando o cookie antigo) e change-password;
    - delete account, demo e chave de API sem poder trocar a senha.
  - Testes novos em `IsolationApiTests`: o usuário B não vê nem altera hábito, ciclo, objetivo, check-in, foco, revisão, conquistas, XP, chaves e push do usuário A (GET, PUT e DELETE por id → 404; listas vazias); chave e MCP de B só veem B; o export só traz o próprio usuário.
  - `ReminderDispatcher` por usuário com fusos diferentes.
  - Migration: banco antigo com dados → usuário 1 dono, e o `OwnerBootstrap` grava e-mail e senha.
- **Arquivos:** `tests/Arco.Api.Tests/*`; e, só se um teste revelar bug, os arquivos de produção envolvidos (reportar).
- **Dependências:** T4, T5, T7.
- **Pronto quando:** `dotnet test` passa inteiro.
- **Modelo/esforço:** `sonnet` / `high`.

## T9 — Docs
- **Descrição:** atualizar os documentos para o modelo multiusuário:
  - `docs/API.md`: auth novo, export, remoção de `/api/backups`, isolamento;
  - `docs/PRODUCT.md`: multiusuário, demo, conta; tirar "single-user" e atualizar os próximos passos;
  - `docs/DEPLOY.md`: variáveis `Auth__OwnerEmail`, `Auth__PasswordHash` (só bootstrap), `Email__ResendApiKey`, `Email__From`, `App__BaseUrl`, `Auth__Google__ClientId`/`ClientSecret` e redirect URI `https://<host>/api/auth/google/callback`;
  - `docs/repo_map.md`;
  - `CLAUDE.md`: regra "toda query nova é filtrada por usuário; jobs de fundo usam Impersonate".
- **Arquivos:** os cinco acima, mais remover `BackupFile` de `web/src/api/types.ts`.
- **Dependências:** T4, T5, T6.
- **Pronto quando:** os docs batem com o código.
- **Modelo/esforço:** `sonnet` / `low`.

## Ondas
| Onda | Tarefas | Verificação |
|---|---|---|
| 1 ✅ concluída | T1, T2, T3 | `dotnet build` e `cd web && npm run lint && npm run build` |
| 2 ✅ concluída | T4, T5, T6, T7 | `dotnet build`, `dotnet test --filter FullyQualifiedName~DomainTests` e `cd web && npm run lint && npm run build` |
| 3 ✅ concluída | T8, T9 | `dotnet test` e `cd web && npm run lint && npm run build` |

**Conflitos checados:** T1 e T5 tocam `ReminderDispatcher` em ondas diferentes, e T4 é o único que toca `Startup`.

## Verificação ponta a ponta (depois da onda 3)
- `dotnet run --project src/Arco.Api` + `cd web && npm run dev`.
- Cadastrar dois usuários em abas anônimas e conferir que não há vazamento.
- Link de verificação e de reset no log (`LogEmailSender`).
- A demo abre com dados.
- Num banco copiado de produção, o dono loga com `Auth:OwnerEmail` e a senha atual e vê os dados antigos.

## Notas da onda 1 (para T4/T5)
- T4: registrar `AddHttpContextAccessor` + `CurrentUser` (scoped); `AppDbContext` já recebe `CurrentUser?`. Chamar `OwnerBootstrap.Run(app)` após `Migrate`. Busca de `ApiKey` por hash precisa de `IgnoreQueryFilters()`. Todo insert sem usuário lança exceção (intencional).
- T5: upsert de `PushSubscription` por `Endpoint` (único global) precisa de `IgnoreQueryFilters` e reatribuir `UserId`.

## Notas da onda 2 (para T8/T9)
- Códigos de erro novos ficam em `Features/Auth/AuthTokens.cs` (`AuthCodes`): `email_taken`, `invalid_credentials`, `invalid_token`. Rate limit: políticas `login` e `auth-actions` (`Auth:ActionsPerMinute`, padrão 10). Falha do Google → `/login?error=google`. Senha 8–72.
- Decisões da T4 (documentar no API.md/PRODUCT.md): catch-up de conquistas no startup só para o usuário 1; reset de senha também verifica o e-mail; vincular Google a conta com e-mail não verificado apaga a senha e troca o stamp; demo não pode definir senha (403); `change-password`/`account` com chave de API → 403.
- T5: `ReminderDispatcher.RunForAllUsersAsync(scopes, log, ct)` é o ponto de entrada; `Phase2ApiTests.cs:252` chama `RunAsync` sem usuário (ajustar). `/api/backups` sumiu (ajustar `Phase3ApiTests`/`ApiFactory`). Export não inclui hashes nem chaves de push.
- T7: `DemoCleanupService.CleanAsync(ct)` é público (testar a cascata). `DemoSeeder` chama `AchievementService.UnlockAsync` com `new AppClock(TimeProvider.System, db)`; se der divergência com o `MutableTime`, trocar para o `TimeProvider` do DI (pode editar `DemoSeeder.cs`/`AuthEndpoints.cs` para isso).
- Tipo `BackupFile` órfão em `web/src/api/types.ts` (T9 pode remover).
