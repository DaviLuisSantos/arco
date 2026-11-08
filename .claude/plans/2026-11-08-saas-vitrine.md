# Plano: Arco SaaS vitrine

Itens deste plano:
- onboarding e `/api/docs`;
- E2E, sessões, 2FA e Sentry;
- perfil público, parceiro e admin;
- Stripe Pro, inglês e README.

## Contexto
O Arco já é multiusuário em produção. Este plano junta as recomendações de portfólio.

**Decisões do usuário:**
- **Pro:** gratuito com limite de 5 hábitos ativos e 1 chave de API, sem MCP e sem tema do perfil; o Pro é ilimitado e tem tudo isso. O usuário 1 e as demos são Pro.
- **Observabilidade:** Sentry no front e no back, ativo só com DSN.
- **Inglês:** app, landing e e-mails.
- **Parceiro:** vê o resumo semanal.

## Regras gerais (todo executor)
- Siga `CLAUDE.md`. As queries novas respeitam o filtro por usuário; `IgnoreQueryFilters` só em leitura cruzada intencional, com checagem explícita de dono. Texto da UI via i18n (pt-BR é a fonte).
- **Testes de backend:** cada tarefa cria o próprio arquivo em `tests/Arco.Api.Tests/` e **não edita `ApiFactory.cs`**. Configuração extra vai por `factory.WithWebHostBuilder(b => b.UseSetting(...))`. Os helpers existentes são `LoggedInClientAsync`, `RegisterAsync`, `AsUserAsync`, `UserIdAsync`, `FakeEmailSender` e `MutableTime`.
- **Erros:** `{ error, code }`. Códigos novos, declarados em `Infrastructure/ApiResults.cs`, `ErrorCodes`: `plan_limit` (403), `slug_taken` (409), `two_factor_required`, `invalid_two_factor_code` (401) e `stripe_disabled` (404).
- **API:** documente toda rota nova em `docs/API.md` (T20 consolida) e anote com `.WithTags(...)`, `.WithSummary(...)` e `.Produces<T>(...)`.

## Contratos

### Schema (T1, migration `SaasShowcase`)
```csharp
// User (+campos)
string Locale = "pt-BR";            // "pt-BR" | "en"
UserPlan Plan = UserPlan.Free;      // enum Free, Pro
string? StripeCustomerId; string? StripeSubscriptionId; DateTime? PlanRenewsAt; bool PlanCancelAtPeriodEnd;
DateTime? OnboardedAt;
string? TotpSecretProtected; string? TotpPendingSecretProtected; DateTime? TotpEnabledAt; // IDataProtector "arco.totp"
bool PublicProfileEnabled; string? PublicSlug /*único, minúsculo*/; bool PublicShowHabits = true; string PublicTheme = "indigo";

class UserSession : IUserOwned { Guid Id; int UserId; DateTime CreatedAt; DateTime LastSeenAt; string UserAgent /*≤200*/; string Ip /*≤45*/; DateTime? RevokedAt; }
class TotpRecoveryCode : IUserOwned { int Id; int UserId; string CodeHash /*SHA-256*/; DateTime? UsedAt; }
class Partnership { int Id; int OwnerUserId; int? PartnerUserId; string TokenHash /*único*/; DateTime CreatedAt; DateTime ExpiresAt; DateTime? AcceptedAt; } // NÃO IUserOwned; FKs User cascade (owner) / cascade (partner)
class StripeEvent { string Id /*PK*/; DateTime ProcessedAt; }
```
- **Backfill da migration:**
  - usuário 1 e demos: `Plan = Pro`;
  - todos os existentes: `OnboardedAt = CreatedAt`.

```csharp
// Domain/Plans.cs
public static class PlanLimits {
  public static int? MaxActiveHabits(UserPlan p) => p == UserPlan.Free ? 5 : null;
  public static int? MaxApiKeys(UserPlan p) => p == UserPlan.Free ? 1 : null;
  public static bool Mcp(UserPlan p) => p == UserPlan.Pro;
  public static bool ProfileTheme(UserPlan p) => p == UserPlan.Pro;
}
```

### API nova
**Auth (T5):**
- `Me` passa a ser `{ id, email, displayName, emailVerified, isDemo, hasPassword, hasGoogle, twoFactorEnabled, plan: "free"|"pro", locale, isAdmin, onboarded, publicProfile: { enabled, slug, showHabits, theme } }`. É admin quem tem o e-mail em `Admin:Emails` (separados por vírgula).
- `POST /api/auth/login`:
  - **204** + cookie;
  - com 2FA, **200** `{ twoFactorRequired: true, ticket }` (ticket protegido, 5 min, propósito `login-2fa`).
- `POST /api/auth/login/2fa` `{ ticket, code }`: o `code` é TOTP de 6 dígitos ou código de recuperação. **204** + cookie; 401 `invalid_two_factor_code`. Rate limit `login`.
- No callback do Google, com 2FA ligado, redireciona para `/login?twofactor=<ticket>`.
- `POST /api/auth/2fa/setup` → `{ secret, otpauthUri }`.
- `POST /api/auth/2fa/enable` `{ code }` → `{ recoveryCodes: string[10] }`.
- `POST /api/auth/2fa/disable` `{ code }` → 204.
- `GET /api/auth/sessions` → `[{ id, createdAt, lastSeenAt, userAgent, current }]`.
- `DELETE /api/auth/sessions/{id}` → 204.
- `POST /api/auth/sessions/revoke-others` → 204.
- O cookie ganha o claim `sid`. O `OnValidatePrincipal` rejeita sessão revogada ou inexistente e atualiza `LastSeenAt` no máximo 1×/5 min. O logout revoga a sessão atual.
- `PUT /api/auth/locale` `{ locale }` → 204.
- `POST /api/auth/onboarding/complete` → 204.
- Só com cookie (chave de API → 403): sessões, 2FA, locale e onboarding.

**Billing (T6):**
- `GET /api/billing` → `{ plan, renewsAt, cancelAtPeriodEnd, stripeEnabled, limits: { habits, apiKeys, mcp, profileTheme }, usage: { habits, apiKeys } }`.
- `POST /api/billing/checkout` → `{ url }`.
- `POST /api/billing/portal` → `{ url }`.
- `POST /api/billing/webhook`: anônimo, assinatura `Stripe:WebhookSecret`, idempotente por `StripeEvent`.
- Configuração: `Stripe:SecretKey`, `Stripe:PriceId` e `Stripe:WebhookSecret`. Sem ela, `stripeEnabled=false` e checkout/portal devolvem 404 `stripe_disabled`.
- Limites → 403 `plan_limit`:
  - criar ou desarquivar hábito acima do limite;
  - criar chave acima do limite;
  - `/api/mcp` no Free.
- No downgrade nada é apagado; só a criação fica bloqueada.

**Perfil público (T7):**
- `PUT /api/profile/public` `{ enabled, slug, showHabits, theme }`:
  - slug `^[a-z0-9-]{3,30}$`, com lista de reservados (admin, api, login…);
  - 409 `slug_taken`;
  - tema diferente de `indigo` no Free → 403 `plan_limit`.
- `GET /api/public/profiles/{slug}` (anônimo) → `{ displayName, level, totalXp, theme, cycle: { name, week, totalWeeks, stones: [{ week, rate }] } | null, habits: [{ name, level, streak }] | null }`. 404 se desligado.
- `GET /u/{slug}`: HTML do servidor com OG.
- `GET /u/{slug}/og.png`: PNG 1200×630 via SkiaSharp, cache de 1h.

**Parceiro (T8):**
- `POST /api/partners/invites` → 201 `{ id, link, expiresAt }` (7 dias).
- `POST /api/partners/accept` `{ token }` → 200 `{ id, owner: { displayName } }`. Não pode aceitar o próprio convite: 400 `validation`.
- `GET /api/partners` → `{ sharingWith: [{ id, displayName, since }], sharedWithMe: [{ id, displayName, since }], pendingInvites: [{ id, createdAt, expiresAt }] }`.
- `GET /api/partners/{id}/summary`: só para o parceiro, senão 404. Devolve o resumo da semana atual (segunda→hoje) e da anterior: `{ owner: { displayName, level }, weeks: [{ start, end, habits: [{ name, level, done, minimum, expected, rate, streak }], totals }] }`.
  - Lê os dados do dono num scope novo com `CurrentUser.Impersonate(ownerId)`.
  - Reaproveita `ReviewStats.ForRangeAsync` e `Queries.Streak`.
- `DELETE /api/partners/{id}` (qualquer lado) → 204.

**Admin (T9):**
- `GET /api/admin/metrics`: não-admin → 404. Devolve:
  - `users: { total, verified, demo, pro }`;
  - `signupsByDay` e `checkInsByDay` de 30 dias;
  - `activeUsers: { d1, d7, d30 }` (pelo menos um check-in);
  - `retention: [{ cohortWeek, size, week1, week2, week4 }]` (8 coortes).
  - Demos ficam fora, exceto na contagem.

**Documentação da API (T3):**
- `GET /api/openapi/v1.json` (anônimo).
- `GET /api/docs`: Scalar, anônimo, com esquemas de segurança cookie e Bearer.

### Stubs criados na onda 1 (as tarefas seguintes preenchem)
- **Backend (T3):**
  - `Features/Billing/BillingEndpoints.cs` com `MapBilling(this RouteGroupBuilder secured, RouteGroupBuilder anon)`;
  - `Features/Profiles/ProfileEndpoints.cs` com `MapProfiles(secured, anon)` e `MapPublicProfilePage(this WebApplication)`;
  - `Features/Partners/PartnerEndpoints.cs` com `MapPartners(secured)`;
  - `Features/Admin/AdminEndpoints.cs` com `MapAdmin(secured)`;
  - `Infrastructure/PlanGate.cs` com `McpPlanFilter : IEndpointFilter` (no-op).
  - Todos já ligados no `Startup`. Cada stub compila e devolve o grupo.
- **Web (T10):**
  - `api/types.ts` e `api/endpoints.ts` com **todos** os tipos e chamadas acima (`authApi.*` novos, `billingApi`, `profileApi`, `partnersApi`, `adminApi`);
  - stubs `export function X() { return null }` em `pages/{Onboarding,Admin,PartnerSummary,AcceptInvite}Page.tsx` e `features/{security/SecurityCard,billing/PlanCard,publicProfile/PublicProfileCard,partners/PartnersCard}.tsx`;
  - rotas `/welcome`, `/admin`, `/partners/:id` e `/invite/:token` (pública, com redirecionamento ao login se preciso);
  - seções no Perfil;
  - item Admin na navegação quando `me.isAdmin`.

### i18n (T2)
- `i18next` + `react-i18next`. Recursos em `web/src/i18n/locales/{pt-BR,en}/<ns>.ts` (`export default {...}`), carregados por `import.meta.glob(..., { eager: true })`. O namespace é o nome do arquivo. Os tipos vêm do pt-BR (`CustomTypeOptions`).
- **Detecção:** `localStorage['arco-locale']` → `me.locale` → navegador (`pt*` → pt-BR, senão en). `setLocale(l)` grava localmente e chama `PUT /api/auth/locale` quando há sessão.
- `lib/dates.ts`, `lib/format.ts` e `ConnectionBanner` usam o idioma atual.
- No `lib/errors.ts`, em en, as mensagens vêm do namespace `errors` pelo código, ignorando a mensagem pt do backend.
- Cada tarefa de UI cria o próprio namespace (pt-BR e en). O texto nunca fica fixo em componente novo.
- **Pacotes, todos instalados no T2:** `i18next`, `react-i18next`, `qrcode` + `@types/qrcode`, `@sentry/react` e, em dev, `@playwright/test`.

## T1: Schema, planos e migration
- **Descrição:** contrato de Schema completo, `Domain/Plans.cs`, configuração do EF (índices únicos `PublicSlug`, `Partnership.TokenHash` e `UserSession` por `UserId`; filtro em `UserSession`/`TotpRecoveryCode` via `IUserOwned`), migration com backfill revisado à mão e `ErrorCodes` novos.
- **Arquivos:** `src/Arco.Api/Domain/Entities.cs`, `Domain/Enums.cs`, `Domain/Plans.cs` (novo), `Infrastructure/AppDbContext.cs`, `Infrastructure/ApiResults.cs`, `Infrastructure/Migrations/*`.
- **Dependências:** nenhuma.
- **Pronto quando:** `dotnet build` + `dotnet test` passam e a migration aplica em banco com dados.
- **Modelo/esforço:** `sonnet` / `high`.

## T2: Base de i18n no web
- **Descrição:** instalar os pacotes do contrato, montar `web/src/i18n/index.ts` (init, detecção, `setLocale`, `useLocale`) e criar os namespaces `common` e `errors` (pt-BR e en). Ajustar `dates.ts`, `format.ts`, `ConnectionBanner` e o init no `main.tsx`.
- **Arquivos:** `web/package.json`, `web/package-lock.json`, `web/src/i18n/**`, `web/src/lib/dates.ts`, `web/src/lib/format.ts`, `web/src/components/layout/ConnectionBanner.tsx`, `web/src/main.tsx`.
- **Dependências:** nenhuma.
- **Pronto quando:** lint e build passam.
- **Modelo/esforço:** `sonnet` / `medium`.

## T3: Infra do backend (OpenAPI, Scalar, Sentry, stubs, raiz em inglês)
- **Descrição:**
  - Pacotes no csproj: `Microsoft.AspNetCore.OpenApi`, `Scalar.AspNetCore`, `Sentry.AspNetCore`, `Stripe.net`, `SixLabors.ImageSharp`, `SixLabors.ImageSharp.Drawing` e `SixLabors.Fonts`.
  - `<EmbeddedResource Include="Resources/**" />` com a fonte Inter (OFL) em `src/Arco.Api/Resources/`.
  - OpenAPI em `/api/openapi/v1.json` e Scalar em `/api/docs` (anônimos, com esquemas cookie e Bearer).
  - Sentry só com `Sentry:Dsn`, sem PII, `SendDefaultPii=false`, usuário só pelo id.
  - O `HandleExceptionAsync` loga a exceção.
  - Criar os stubs do contrato, chamar `Map*` no `MapArco` e ligar o `McpPlanFilter` no `MapMcp`.
  - Middleware da raiz: visitante sem sessão com `Accept-Language` preferindo en e `/landing/en/index.html` existente → landing em inglês.
- **Arquivos:** `src/Arco.Api/Arco.Api.csproj`, `Program.cs`, `Infrastructure/Startup.cs`, `appsettings.json`, `Resources/*`, stubs do contrato (`Features/{Billing,Profiles,Partners,Admin}/*Endpoints.cs`, `Infrastructure/PlanGate.cs`).
- **Dependências:** nenhuma.
- **Pronto quando:** build e testes passam e `/api/docs` abre localmente.
- **Modelo/esforço:** `sonnet` / `high`.

## T4: Anotações OpenAPI nas rotas existentes
- **Descrição:** `.WithTags`, `.WithSummary` (pt-BR) e `.Produces<T>` / `.ProducesProblem` nas rotas existentes, para os tipos aparecerem no schema. Sem mudar comportamento.
- **Arquivos:** `src/Arco.Api/Features/*/*Endpoints.cs` existentes, **exceto** `Auth/*`, `Habits/HabitEndpoints.cs` e `ApiKeys/ApiKeyEndpoints.cs`; anote estes dois só nas linhas de mapeamento, não nos handlers. Mais `Features/Shared/Dtos.cs`, se precisar.
- **Dependências:** nenhuma.
- **Pronto quando:** build e testes passam.
- **Modelo/esforço:** `sonnet` / `low`.

## T10: Esqueleto do web (cliente, rotas, stubs)
- **Descrição:**
  - Tipos e endpoints novos.
  - Stubs, rotas e seções do Perfil: Plano, Perfil público, Parceiros, Segurança.
  - Admin na navegação.
  - Remover as rotas duplicadas do `router.tsx`.
  - Códigos novos no `MESSAGES` de `errors.ts`.
- **Arquivos:** `web/src/api/types.ts`, `web/src/api/endpoints.ts`, `web/src/app/router.tsx`, `web/src/pages/ProfilePage.tsx`, `web/src/components/layout/AppLayout.tsx`, `web/src/lib/errors.ts`, os stubs do contrato.
- **Dependências:** nenhuma.
- **Pronto quando:** lint e build passam.
- **Modelo/esforço:** `sonnet` / `medium`.

## T5: Sessões, 2FA, Me, locale, onboarding e e-mails em inglês
- **Descrição:**
  - Contrato de Auth completo:
    - TOTP RFC 6238 próprio (SHA-1, 30 s, janela ±1), com o segredo cifrado por DataProtection;
    - 10 códigos de recuperação de uso único;
    - tabela `UserSession`;
    - ticket de 2FA.
  - Demo nasce Pro e onboarded.
  - `EmailTemplates` recebe `locale` (pt-BR/en), e os envios usam `user.Locale`.
- **Arquivos:** `src/Arco.Api/Features/Auth/*`, `Infrastructure/Email/EmailTemplates.cs`, `tests/Arco.Api.Tests/AuthSecurityApiTests.cs` (novo).
- **Dependências:** T1, T3.
- **Pronto quando:** `dotnet test` passa, com testes de:
  - login com 2FA (TOTP e recuperação) e ticket expirado;
  - revogar sessão derruba o cookie;
  - revoke-others mantém a atual;
  - `Me` com os campos novos;
  - e-mail em inglês.
- **Modelo/esforço:** `opus` / `high`.

## T6: Stripe e limites do plano
- **Descrição:**
  - Billing com `Stripe.net`: Checkout (modo assinatura, `client_reference_id = userId`, `customer_email`), Portal e webhook (`checkout.session.completed`, `customer.subscription.updated/deleted` → Plan, RenewsAt e CancelAtPeriodEnd).
  - Limites com `PlanLimits` em `HabitEndpoints` (criar e desarquivar) e `ApiKeyEndpoints`.
  - `McpPlanFilter` real.
- **Arquivos:** `src/Arco.Api/Features/Billing/*`, `Infrastructure/PlanGate.cs`, `Features/Habits/HabitEndpoints.cs`, `Features/ApiKeys/ApiKeyEndpoints.cs`, `tests/Arco.Api.Tests/BillingApiTests.cs` (novo).
- **Dependências:** T1, T3.
- **Pronto quando:** `dotnet test` passa, com:
  - limites Free/Pro (hábito, chave, MCP);
  - webhook com assinatura válida e inválida, idempotência e downgrade;
  - `stripeEnabled=false` sem config.
- **Modelo/esforço:** `sonnet` / `high`.

## T7: Perfil público e imagem OG
- **Descrição:**
  - Endpoints do contrato.
  - Página `/u/{slug}`: HTML próprio no estilo da landing, com dados, pedras do ciclo, metas OG/Twitter e `lang` do dono.
  - PNG via SkiaSharp (SKTypeface.FromStream com a fonte embutida Inter OTF): nome, nível, arco do ciclo e cor do tema.
  - Acrescentar `/^\/u\//` ao `denylist` do `sw.ts`.
- **Arquivos:** `src/Arco.Api/Features/Profiles/*`, `web/src/sw.ts`, `tests/Arco.Api.Tests/PublicProfileApiTests.cs` (novo).
- **Dependências:** T1, T3.
- **Pronto quando:** `dotnet test` passa, com:
  - slug válido, reservado e duplicado;
  - desligado → 404;
  - `showHabits=false` esconde os hábitos;
  - `og.png` devolve `image/png`;
  - tema Pro no Free → 403.
- **Modelo/esforço:** `sonnet` / `high`.

## T8: Parceiro de responsabilidade
- **Descrição:** endpoints do contrato. O token tem 32 bytes em base64url e é guardado como SHA-256. O link aponta para `{App:BaseUrl}/invite/{token}`.
- **Arquivos:** `src/Arco.Api/Features/Partners/*`, `tests/Arco.Api.Tests/PartnersApiTests.cs` (novo).
- **Dependências:** T1, T3.
- **Pronto quando:** `dotnet test` passa, com:
  - convidar, aceitar e ver o resumo;
  - terceiro → 404;
  - convite expirado ou usado;
  - aceitar o próprio convite;
  - remover pelos dois lados;
  - o resumo não traz notas nem revisões.
- **Modelo/esforço:** `sonnet` / `high`.

## T9: Admin e push em inglês
- **Descrição:**
  - `/api/admin/metrics` conforme o contrato.
  - Textos do `ReminderDispatcher` conforme `User.Locale`.
- **Arquivos:** `src/Arco.Api/Features/Admin/*`, `Features/Push/ReminderDispatcher.cs`, `tests/Arco.Api.Tests/AdminApiTests.cs` (novo).
- **Dependências:** T1, T3.
- **Pronto quando:** `dotnet test` passa, com:
  - não-admin → 404;
  - métricas com 2 usuários e check-ins;
  - push em inglês para usuário en.
- **Modelo/esforço:** `sonnet` / `medium`.

## T11: Primeiros passos (onboarding)
- **Descrição:**
  - `/welcome` em 4 passos:
    1. idioma e fuso (usa `PUT /api/settings` e `setLocale`);
    2. escolher 2–3 modelos de hábito (8 modelos pt/en, com dose e mínimo do nível 1) ou criar um próprio, via `POST /api/habits`; respeita o limite Free;
    3. iniciar um ciclo (nome, semanas, meta de foco), via a rota existente de ciclos;
    4. concluir com `POST /api/auth/onboarding/complete` e ir para Hoje.
  - "Pular" em todos os passos.
  - `RequireAuth` manda para `/welcome` quando `!me.onboarded` (exceto na própria rota).
- **Arquivos:** `web/src/pages/OnboardingPage.tsx`, `web/src/features/onboarding/*`, `web/src/components/layout/RequireAuth.tsx`, `web/src/i18n/locales/*/onboarding.ts`.
- **Dependências:** T2, T10, T5.
- **Pronto quando:** lint e build passam.
- **Modelo/esforço:** `sonnet` / `medium`.

## T12: Segurança da conta e 2FA no login
- **Descrição:**
  - `SecurityCard`:
    - sessões, com a atual marcada e "sair das outras";
    - ativar 2FA com QR (`qrcode`), confirmação do código e exibição única dos códigos de recuperação com copiar e baixar;
    - desativar.
  - `LoginPage`: passo de código 2FA (resposta `twoFactorRequired` ou `?twofactor=`), com link "usar código de recuperação".
- **Arquivos:** `web/src/features/security/*`, `web/src/pages/LoginPage.tsx`, `web/src/i18n/locales/*/security.ts`.
- **Dependências:** T2, T10, T5.
- **Pronto quando:** lint e build passam.
- **Modelo/esforço:** `sonnet` / `medium`.

## T13: Plano e perfil público (UI)
- **Descrição:**
  - `PlanCard`:
    - plano atual, uso × limite e "Assinar Pro" (checkout);
    - "Gerenciar assinatura" (portal);
    - aviso "modo de teste: use o cartão 4242 4242 4242 4242";
    - esconder os botões se `!stripeEnabled`.
  - Tratar `plan_limit` com um aviso que leva ao Plano (toast com link). Um helper `isPlanLimit(err)` fica em `features/billing`.
  - `PublicProfileCard`:
    - ligar e desligar, slug com prévia do link, mostrar hábitos, tema (só Pro);
    - copiar link e abrir.
- **Arquivos:** `web/src/features/billing/*`, `web/src/features/publicProfile/*`, `web/src/i18n/locales/*/{billing,publicProfile}.ts`.
- **Dependências:** T2, T10, T6, T7.
- **Pronto quando:** lint e build passam.
- **Modelo/esforço:** `sonnet` / `medium`.

## T14: Parceiros e painel admin (UI)
- **Descrição:**
  - `PartnersCard`: convidar (copiar link), lista dos dois lados, pendentes, remover.
  - `AcceptInvitePage`: aceitar e ir para o resumo.
  - `PartnerSummaryPage`: as duas semanas por hábito (barras de taxa) e sequência.
  - `AdminPage`:
    - cartões de números;
    - séries de 30 dias (cadastros e check-ins);
    - ativos d1/d7/d30;
    - tabela de retenção.
    - Gráficos em SVG próprio, sem lib nova.
- **Arquivos:** `web/src/features/partners/*`, `web/src/pages/{AcceptInvite,PartnerSummary,Admin}Page.tsx`, `web/src/features/admin/*`, `web/src/i18n/locales/*/{partners,admin}.ts`.
- **Dependências:** T2, T10, T8, T9.
- **Pronto quando:** lint e build passam.
- **Skills:** `dataviz`.
- **Modelo/esforço:** `sonnet` / `medium`.

## T15: Sentry no front, Playwright E2E e CI
- **Descrição:**
  - `web/src/app/sentry.ts` (init só com `VITE_SENTRY_DSN`, sem PII), importado no `main.tsx`.
  - `playwright.config.ts`, com `webServer`:
    1. sobe a API em `:5199` com banco temporário, `ASPNETCORE_ENVIRONMENT=Development` e `Auth:ActionsPerMinute=1000`;
    2. sobe o `vite preview` com proxy de `/api` para `:5199`.
  - Testes em pt-BR (fixam `localStorage['arco-locale']='pt-BR'`), preferindo seletores por role e test id:
    1. cadastro → onboarding (pular) → criar hábito → check-in → XP aparece;
    2. demo abre com dados;
    3. login e logout;
    4. visitante na raiz vê a landing.
  - Scripts `e2e` e `screenshots` (gera `docs/images/*.png` em 390×844 e 1280×800: Hoje, Ciclo, Hábito, Perfil, Landing).
  - No `deploy.yml`, um job `e2e` depois do `build` (instala o chromium) e o `deploy` passa a depender dele. Passa `VITE_SENTRY_DSN` do `vars` ao build.
- **Arquivos:** `web/src/app/sentry.ts`, `web/src/main.tsx`, `web/playwright.config.ts`, `web/e2e/**`, `web/package.json` (só scripts), `web/vite.config.ts` (proxy do preview), `.github/workflows/deploy.yml`, `.gitignore`.
- **Dependências:** T2, T10, T11 (o fluxo usa onboarding; se T11 ainda não estiver pronto, o teste usa o "Pular" por test id `onboarding-skip`).
- **Pronto quando:** `npm run e2e` passa localmente.
- **Modelo/esforço:** `sonnet` / `high`.

## T16: i18n das páginas e componentes
- **Descrição:** extrair os textos fixos para namespaces por página (pt-BR e en) com `useTranslation`, sem mudar comportamento. Os test ids do E2E ficam intactos.
- **Arquivos:** `web/src/pages/*` (exceto as já feitas com i18n), `web/src/components/**`, `web/src/app/*`, `web/src/i18n/locales/*/{pages,layout,…}.ts`.
- **Dependências:** onda 3.
- **Pronto quando:** lint e build passam e não sobra texto pt fixo nesses arquivos.
- **Modelo/esforço:** `sonnet` / `medium`.

## T17: i18n das features A
- **Descrição:** mesma tarefa que T16, para as features abaixo.
- **Arquivos:** `web/src/features/{habits,checkins,today,cycle,goals,arch,focus}/**` e os namespaces correspondentes.
- **Dependências:** onda 3.
- **Pronto quando:** lint e build passam.
- **Modelo/esforço:** `sonnet` / `medium`.

## T18: i18n das features B e do service worker
- **Descrição:** mesma tarefa, para as demais features. Inclui o `lang` e os textos de notificação do `sw.ts`, quando existirem.
- **Arquivos:** `web/src/features/**` restantes (fora as de T17 e as novas da onda 3), `web/src/sw.ts` e os namespaces correspondentes.
- **Dependências:** onda 3.
- **Pronto quando:** lint e build passam.
- **Modelo/esforço:** `sonnet` / `medium`.

## T19: Landing e privacidade em inglês
- **Descrição:**
  - `web/public/landing/en/index.html` e `landing/en/privacy/index.html`, traduzidos e com `hreflang` cruzado.
  - Seletor PT/EN no topo das duas versões.
  - O `og:locale` e os CTAs levam a `/login?lang=en`; a `LoginPage` já lê o parâmetro via `setLocale`, o que T12 deve garantir (se não estiver feito, reporte).
- **Arquivos:** `web/public/landing/**`.
- **Dependências:** onda 3.
- **Pronto quando:** lint e build passam.
- **Modelo/esforço:** `sonnet` / `low`.

## T20: README e docs
- **Descrição:**
  - README de vitrine:
    - frase, links da demo ao vivo e de `/api/docs`, e screenshots (rodar `npm run screenshots`; se falhar, deixar as referências e reportar);
    - funcionalidades atualizadas;
    - diagrama de arquitetura em Mermaid;
    - "Decisões técnicas": filtro global por usuário, XP como livro-razão, Web Push sem dependência, offline com fila, MCP, TOTP próprio, origem pública atrás do Cloudflare;
    - testes (integração + E2E) e como rodar.
  - Atualizar `docs/API.md` (todas as rotas novas), `docs/PRODUCT.md` (planos, parceiro, perfil público, onboarding, idiomas), `docs/DEPLOY.md` (`Stripe__*`, `Sentry__Dsn`, `VITE_SENTRY_DSN`, `Admin__Emails`, webhook do Stripe), `docs/repo_map.md` e `CLAUDE.md` (regra de i18n; corrigir o caminho `lib/errors.ts`).
- **Arquivos:** `README.md`, `docs/**`, `CLAUDE.md`.
- **Dependências:** onda 3.
- **Pronto quando:** os docs batem com o código.
- **Modelo/esforço:** `sonnet` / `medium`.

## Ondas
| Onda | Tarefas | Verificação |
|---|---|---|
| 1 ✅ concluída | T1, T2, T3, T4, T10 | `dotnet build`, `dotnet test` e `cd web && npm run lint && npm run build` |
| 2 ✅ concluída | T5, T6, T7, T8, T9 | `dotnet test` e `cd web && npm run build` (o `sw.ts` muda) |
| 3 ✅ concluída | T11, T12, T13, T14, T15 | `cd web && npm run lint && npm run build && npx playwright install chromium && npm run e2e` |
| 4 ✅ concluída | T16, T17, T18, T19, T20 | `dotnet test`, `cd web && npm run lint && npm run build && npm run e2e` |

**Conflitos checados:**
- `Startup` só no T3.
- `HabitEndpoints` e `ApiKeyEndpoints`: T4 só no mapeamento (onda 1) e T6 (onda 2).
- `LoginPage`: T12 (onda 3) e T16 (onda 4).
- `main.tsx`: T2 (onda 1) e T15 (onda 3).
- `sw.ts`: T7 (onda 2) e T18 (onda 4).
- `package.json`: T2 (onda 1) e T15, só scripts (onda 3).

## Depois do deploy (o usuário faz)
- **Stripe:** conta em modo teste, produto Pro com preço mensal e webhook `https://arco.daviluis.dev/api/billing/webhook` (eventos acima).
- **Azure:** `Stripe__SecretKey`, `Stripe__PriceId`, `Stripe__WebhookSecret`, `Sentry__Dsn` e `Admin__Emails`.
- **GitHub:** a variável `VITE_SENTRY_DSN`.

## Notas da onda 1 (para as próximas)
- **Build/test do .NET: use sempre `-c Release`** (`dotnet build -c Release`, `dotnet test -c Release`). Um processo `Arco.Api` antigo trava `src/Arco.Api/bin/Debug`. Nunca suba a API em `bin/Debug` nem deixe processo rodando: se subir algo para testar, use outra porta e encerre ao final.
- **Imagem:** ImageSharp foi trocado por **SkiaSharp 4.154.1** (+ `NativeAssets.Linux.NoDependencies`), porque o ImageSharp atual exige licença no build. A fonte embutida é `Resources/Inter-Regular.otf` e `Inter-Bold.otf` (CFF); carregue com `SKTypeface.FromStream`.
- **MCP:** o `McpPlanFilter` está em `EndpointBuilder.FilterFactories` (via `AddMcpPlanFilter`). Não foi confirmado que roda com o `RequestDelegate` do MCP. Se não rodar, T6 pode trocar por um middleware com escopo `/api/mcp` dentro de `PlanGate.cs`, desde que a ligação no `Startup` não mude: a extensão existente deve fazer o trabalho.
- **i18n (T2):** `web/src/i18n/index.ts` exporta `setLocale`, `useLocale`, `currentLocale`, `applyUserLocale` e `detectLocale`.
  - T11 deve chamar `applyUserLocale(me.locale)` no `RequireAuth` quando o `me` carregar.
  - T18 deve fazer `lib/errors.ts` usar `i18n.t('errors:<code>')` em en, ignorando a mensagem do backend, e traduzir as constantes de `lib/dates.ts` e `lib/format.ts` (`WEEKDAY_*`, `STATUS_LABELS`, `XP_SOURCE_LABELS`, `frequencyText`, `streakText`). Arquivos extras do T18: `web/src/lib/errors.ts`, `lib/dates.ts`, `lib/format.ts`.
- **T10:** `authApi.login` devolve `TwoFactorChallenge | undefined`. O `AcceptInvitePage` (T14) deve redirecionar ao login com `state.from` se não houver sessão.
- **T4:** sem `.Produces<T>` nas rotas antigas. As rotas novas devem tê-lo.

## Notas da onda 2 (para a onda 3+)
- **2FA (T5):**
  - código errado em `2fa/enable` e `2fa/disable` → **400** `invalid_two_factor_code`; em `login/2fa`, 401;
  - o login com 2FA devolve 200 `{ twoFactorRequired, ticket }`;
  - `POST /api/auth/register` aceita `locale` opcional;
  - `AuthEndpoints.IsAdminEmail(config, email)` é o helper de admin.
- **Parceiros (T8):** convite inexistente, expirado ou usado → 404; parceria repetida → 409; máximo de 10 convites pendentes (400).
- **Perfil público (T7):** temas `indigo|emerald|amber|rose|sky` (outro → 400); conta Free exibe indigo. Página `/u/{slug}` servida pelo backend (o sw ignora `/u/`).
- **Admin (T9):** `retention.week1/2/4` são frações de 0 a 1 (mostre em %).
- **Billing (T6):** checkout e portal só funcionam com `Stripe:*`; sem config, `stripeEnabled=false`. Os testes de MCP promovem o usuário a Pro.
- **Build/test do .NET:** `-c Release`. O web não muda.

## Notas da onda 3 (para a onda 4)
- **Tipos de i18n:** os namespaces de `web/src/i18n/index.ts` estão tipados via `export interface ArcoResources`. **Namespace novo: não edite `index.ts`.** Registre-o no próprio arquivo pt-BR:
  ```ts
  const ns = { ... }
  export default ns
  declare module '../../index' { interface ArcoResources { meuNs: typeof ns } }
  ```
- **Hooks sem checagem de chaves, a trocar por `useTranslation('<ns>')` tipado:**
  - T18: `features/billing/useLooseT.ts`, `features/security/useSecurityT.ts`, `features/partners/usePartnersT.ts`, e remover os arquivos;
  - T16: o cast local em `pages/OnboardingPage.tsx`, e os usos em `pages/{PartnerSummary,Admin,AcceptInvite}Page.tsx`.
- **Limite do plano:** `features/billing` exporta `isPlanLimit`, `usePlanLimitHandler` (para `onError`: toast com link para o Plano) e `PlanLimitNotice`. Use-os em:
  - T17: criar e restaurar hábito em `features/habits`;
  - T18: criar chave de API em `features/apikeys`.
- **E2E (T15):**
  - `npm run e2e` faz publish em pasta temporária, com API em :5199 e preview em :4199;
  - o "Pular" do onboarding avança um passo por clique;
  - os testes usam rótulos pt-BR ("E-mail", "Senha", "Entrar", "+N XP hoje"): ao extrair textos, mantenha os mesmos valores em pt-BR;
  - `npm run screenshots` já gerou `docs/images/*.png`; T20 pode reaproveitar ou regenerar.
- **Sentry web:** v11, sem `sendDefaultPii`.
