# Arco — Contrato da API (Fase 2)

## Convenções

- Todas as rotas ficam sob `/api`. JSON em camelCase e enums como string.
- Datas de calendário são `"YYYY-MM-DD"` (DateOnly). Instantes são ISO-8601 UTC com `Z`.
- Ids são `int` (autoincremento).
- Multiusuário: cada usuário só vê e altera os próprios dados (filtro global por usuário). Recurso de outro usuário → 404.
- Autenticação, de um destes jeitos (toda rota `/api/*` aceita os dois):
  - **Cookie** HttpOnly `arco_auth` (o app): SameSite=Lax, Secure fora de Development, sliding de 30 dias.
  - **Chave de API**: cabeçalho `Authorization: Bearer arco_…`. Criada e revogada no app (Perfil → Acesso pela API);
    dá acesso total, exceto gerenciar chaves. Ver "Chaves de API".

  Sem credencial válida, toda rota `/api/*`, exceto as rotas anônimas de `/api/auth/*` (ver "Auth") e `/api/health`, devolve
  **401** (nunca redirect). Chave inválida ou revogada → 401 `unauthorized`.
- Erros: `{ "error": "mensagem pt-BR", "code": "snake_case" }`.

| Status | Codes |
|---|---|
| 400 | `validation`, `invalid_token`, `invalid_two_factor_code` (ativar/desativar 2FA) |
| 401 | `unauthorized`, `invalid_credentials`, `invalid_two_factor_code` (login com 2FA) |
| 403 | `forbidden`, `plan_limit` |
| 404 | `not_found`, `stripe_disabled` |
| 409 | `active_cycle_exists`, `conflict`, `email_taken`, `slug_taken` |
| 429 | `rate_limited` |
| 500 | `internal_error` |

- O "hoje" é calculado no fuso de `Settings.Timezone` (padrão `America/Sao_Paulo`).
- Toda resposta de ação que dá ou tira XP (`{ …, xpDelta, player }`) traz também `achievements: Achievement[]`: as
  conquistas que essa ação desbloqueou (normalmente vazio). Abaixo, `{ xpDelta, player }` subentende esse campo.

## Tipos compartilhados

```ts
type FrequencyType = 'Daily' | 'SpecificDays' | 'WeeklyTarget';
type CheckInStatus = 'Done' | 'Minimum' | 'Missed';
type GoalType = 'Numeric' | 'Milestones';
type XpSource = 'CheckIn' | 'HabitLevelUp' | 'Milestone' | 'GoalCompleted' | 'CycleCompleted' | 'WeeklyReview';

interface Player {
  level: number;          // começa em 1
  totalXp: number;
  xpIntoLevel: number;    // XP acumulado dentro do nível atual
  xpForNextLevel: number; // custo do nível atual para o próximo = 100 + 50*(level-1)
}

interface Cycle {
  id: number; name: string;
  startDate: string; endDate: string;  // endDate = start + weeks*7 - 1
  weeks: number;         // semanas do ciclo (pedras do arco)
  weeklyFocusMinutes: number; // meta semanal de foco, 60..3360
  status: 'Active' | 'Completed';
  dayNumber: number;     // 1..totalDays (limitado ao intervalo) em relação a hoje
  totalDays: number;
  reviewNotes: string | null;      // notas livres da revisão de encerramento
  reviewWins: string | null;       // revisão guiada: o que funcionou
  reviewObstacles: string | null;  // o que não funcionou
  reviewNext: string | null;       // o que levar para o próximo ciclo
  completedAt: string | null;
}

interface Milestone { id: number; title: string; order: number; doneAt: string | null; }

interface Goal {
  id: number; cycleId: number;
  title: string; description: string | null;
  type: GoalType;
  unit: string | null; startValue: number | null; targetValue: number | null; currentValue: number | null;
  milestones: Milestone[];             // vazio para Numeric
  deadline: string;                    // YYYY-MM-DD
  progress: number;                    // 0..1
  completedAt: string | null;
  habitIds: number[];
}

interface HabitLevel { level: number; description: string; minimumDescription: string; reachedAt: string; }

// Degrau planejado (ainda não alcançado); level = número que ele vai ter (nível atual + 1, + 2…).
interface HabitPlannedLevel { level: number; description: string; minimumDescription: string; }

interface CheckIn { habitId: number; date: string; status: CheckInStatus; xp: number; }

interface Habit {
  id: number; name: string; emoji: string | null;
  goalId: number | null;
  frequencyType: FrequencyType;
  frequencyDays: number[];             // 0=domingo..6=sábado, só em SpecificDays
  timesPerWeek: number | null;         // 1..7, só em WeeklyTarget
  reminderTime: string | null;         // "HH:mm" no fuso configurado; lembrete push do hábito
  level: number;                       // nível atual
  levelDescription: string;            // descrição do nível atual
  minimumDescription: string;
  levels: HabitLevel[];                // crescente
  plannedLevels: HabitPlannedLevel[];  // a escada à frente, em ordem; subir de nível consome o primeiro
  levelUpWeeks: number | null;         // regra "N semanas seguidas com X+ dias"; null = regra padrão (80% em 14 dias)
  levelUpDaysPerWeek: number | null;
  archivedAt: string | null;
  createdAt: string;
}

// Consistência para a sugestão de subir de nível: 14 dias terminando hoje (se hoje já tem
// check-in) ou ontem. Só "Feito" conta. expected: Daily = 14; SpecificDays = dias da lista
// na janela; WeeklyTarget = ceil(timesPerWeek × 14 / 7).
interface Consistency { from: string; to: string; done: number; expected: number; rate: number; } // rate 0..1

// Sequência (streak) do hábito, calculada sobre todo o histórico (nunca gravada); ver PRODUCT.md.
// unit: "Days" (Daily/SpecificDays: dias devidos) ou "Weeks" (WeeklyTarget: semanas com a meta batida).
interface Streak { current: number; best: number; unit: "Days" | "Weeks"; }

interface WeeklyReview {
  id: number;
  weekStart: string; weekEnd: string;  // segunda a domingo
  wins: string | null; obstacles: string | null; next: string | null;
  createdAt: string;
}

// Números de um hábito num período (revisões semanal e de ciclo).
interface HabitPeriodStats {
  habitId: number; name: string; emoji: string | null;
  level: number;                       // nível atual
  levelsGained: number;                // níveis alcançados no período
  done: number; minimum: number; missed: number;
  expected: number;                    // pela frequência atual, a partir da criação do hábito (ou de um check-in anterior a ela)
  rate: number;                        // (done + minimum) / expected, 0..1
  levelUpSuggested: boolean;           // sugestão de subir de nível hoje
}

interface CheckInTotals { done: number; minimum: number; missed: number; }
```

## Auth

Sessão pelo cookie `arco_auth`. Erros no formato `{ error, code }`. Senha de 8 a 72 caracteres.

| Rota | Corpo | Resposta |
|---|---|---|
| `POST /api/auth/register` | `{ email, password, displayName?, locale? }` | **201** `Me` + cookie + e-mail de verificação; 409 `email_taken`; 400 `validation`. Sem `locale`, vem do `Accept-Language` (pt* → `pt-BR`, outro → `en`, ausente → `pt-BR`) |
| `POST /api/auth/login` | `{ email, password }` | **204** + cookie; com 2FA, **200** `{ twoFactorRequired: true, ticket }` (sem cookie; ticket vale 5 min); 401 `invalid_credentials` |
| `POST /api/auth/login/2fa` | `{ ticket, code }` | `code` = TOTP de 6 dígitos ou código de recuperação (uso único). **204** + cookie; 401 `invalid_two_factor_code` (código errado, ticket inválido/expirado ou senha trocada). Rate limit `login` |
| `POST /api/auth/logout` | — | **204**; revoga a sessão atual |
| `GET /api/auth/me` | — | **200** `Me` ou **401** |
| `GET /api/auth/providers` | — | **200** `{ google: boolean }` |
| `POST /api/auth/demo` | — | **201** + cookie de uma conta demo com dados de exemplo |
| `POST /api/auth/verify-email` | `{ token }` | **204**; 400 `invalid_token` (o token vale 48h) |
| `POST /api/auth/resend-verification` | (autenticado) | **204** |
| `POST /api/auth/forgot-password` | `{ email }` | sempre **204**; só envia para e-mail verificado; token de 1h |
| `POST /api/auth/reset-password` | `{ token, password }` | **204**; troca o stamp de segurança (derruba as outras sessões) e também verifica o e-mail |
| `POST /api/auth/change-password` | `{ currentPassword?, newPassword }` | **204**; 401 `invalid_credentials`; só com o cookie |
| `DELETE /api/auth/account` | `{ password? }` | **204**; apaga o usuário e todos os dados em cascata; só com o cookie |
| `GET /api/auth/google?returnUrl=` | — | challenge OAuth; falha → redireciona a `/login?error=google` |
| `/api/auth/google/callback` | — | retorno do Google (tratado pelo middleware; é o redirect URI a cadastrar) |
| `GET /api/auth/google/complete` | — | vincula ou cria o usuário, define o cookie e redireciona para `returnUrl` ou `/`; com 2FA ligado, não define o cookie e redireciona a `/login?twofactor=<ticket>` (conclua com `login/2fa`) |
| `POST /api/auth/2fa/setup` | — | **200** `{ secret, otpauthUri }` (segredo pendente; Base32, SHA-1, 6 dígitos, 30 s); 409 `conflict` se já ativo; 403 em demo |
| `POST /api/auth/2fa/enable` | `{ code }` | **200** `{ recoveryCodes: string[10] }` (mostrados só agora); 400 `invalid_two_factor_code`; 400 `validation` sem setup; 409 se já ativo |
| `POST /api/auth/2fa/disable` | `{ code }` | **204** (TOTP ou código de recuperação); 400 `invalid_two_factor_code`; 400 `validation` se não está ativo |
| `GET /api/auth/sessions` | — | **200** `[{ id, createdAt, lastSeenAt, userAgent, current }]` (abertas nos últimos 30 dias, mais recentes primeiro) |
| `DELETE /api/auth/sessions/{id}` | — | **204**; o cookie daquela sessão deixa de valer (a atual = logout); 404 se não é sua |
| `POST /api/auth/sessions/revoke-others` | — | **204**; mantém só a sessão atual |
| `PUT /api/auth/display-name` | `{ displayName }` | **204**; 1 a 80 caracteres; 400 `validation` |
| `PUT /api/auth/locale` | `{ locale }` | **204**; `"pt-BR"` ou `"en"` (idioma do app e dos e-mails); 400 `validation` |
| `POST /api/auth/onboarding/complete` | — | **204**; `onboarded` passa a `true` |

- `Me` = `{ id, email, displayName, emailVerified, isDemo, hasPassword, hasGoogle, twoFactorEnabled, plan: "free"|"pro", locale: "pt-BR"|"en", isAdmin, onboarded, publicProfile: { enabled, slug, showHabits, theme } }`. `isAdmin` = e-mail listado em `Admin:Emails` (separados por vírgula).
- Rotas anônimas: `register`, `login`, `login/2fa`, `me`, `providers`, `demo`, `verify-email`, `forgot-password`, `reset-password` e as do Google.
- Sessões: cada cookie aponta para uma sessão (claim `sid`); sessão revogada ou inexistente → 401. `lastSeenAt` é atualizado no máximo a cada 5 min. Trocar a senha revoga as outras sessões; redefinir revoga todas.
- 2FA: segredo cifrado (DataProtection, `arco.totp`); aceita 1 passo de 30 s de folga para cada lado; códigos de recuperação guardados só como SHA-256. Erros de código em rotas logadas são 400 (não 401, que o app trata como sessão encerrada).
- Conta demo: nasce `pro` e `onboarded`.
- E-mails (verificação, redefinição) saem no idioma da conta (`locale`).
- Rate limit por IP: `login` (`Auth:LoginAttemptsPerMinute`, padrão 5) e `register`, `demo`, `forgot-password`, `resend-verification` (`Auth:ActionsPerMinute`, padrão 10) → 429 `rate_limited`.
- Conta demo (`isDemo`): apagada automaticamente após 24h; não pode definir senha (403).
- Vincular o Google a uma conta com e-mail não verificado apaga a senha dela e troca o stamp.
- `change-password`, `account`, `sessions*`, `2fa/*`, `locale` e `onboarding/complete` com chave de API → 403 `forbidden`.
- Chaves de API pertencem ao usuário que as criou e acessam apenas os dados dele.

## Plano e cobrança

Dois planos: `free` e `pro`. Limites (`Domain/Plans.cs`): Free tem até 5 hábitos ativos, 1 chave de API, sem MCP e só o tema
`indigo` no perfil público; Pro não tem limite. Passar do limite devolve **403** `plan_limit`: criar ou desarquivar hábito acima
do limite, criar chave acima do limite, qualquer chamada a `/api/mcp` no Free, tema de perfil diferente de `indigo` no Free.
No downgrade nada é apagado; só a criação fica bloqueada.

- `GET /api/billing` → `{ plan, renewsAt: string | null, cancelAtPeriodEnd, stripeEnabled, limits: { habits: number | null, apiKeys: number | null, mcp, profileTheme }, usage: { habits, apiKeys } }` (`null` = sem limite).
- `POST /api/billing/checkout` → `{ url }` (Stripe Checkout da assinatura Pro).
- `POST /api/billing/portal` → `{ url }` (portal de cobrança do Stripe).
- Sem `Stripe:SecretKey`/`Stripe:PriceId`, `stripeEnabled` é `false` e checkout/portal devolvem **404** `stripe_disabled`.
- Checkout: quem já tem assinatura Pro recebe **409**; o portal de quem ainda não tem cliente no Stripe devolve **404** `not_found`. Falha ao falar com o Stripe → **502** `internal_error`.
- `POST /api/billing/webhook`: anônimo; valida a assinatura (`Stripe:WebhookSecret`; inválida → 400) e é idempotente por id de evento (`StripeEvent`).
  Trata `checkout.session.completed`, `customer.subscription.updated` e `customer.subscription.deleted`.

## Perfil público

- `PUT /api/profile/public` `{ enabled, slug, showHabits, theme }` (todos opcionais; corpo vazio só devolve o estado) → `{ enabled, slug, showHabits, theme, url }` (cookie ou chave de API). `url` é `{App:BaseUrl}/u/{slug}` (`null` sem slug).
  `slug` casa `^[a-z0-9-]{3,30}$` e não pode ser reservado (admin, api, login…) → 400 `validation`; já em uso → **409** `slug_taken`.
  `theme` em `indigo|emerald|amber|rose|sky` (outro → 400); diferente de `indigo` no Free → **403** `plan_limit`.
- `GET /api/public/profiles/{slug}` (anônimo) → `{ displayName, level, totalXp, theme, cycle: { name, week, totalWeeks, stones: [{ week, rate }] } | null, habits: [{ name, level, streak }] | null }`.
  `habits` é `null` se `showHabits` está desligado; perfil desligado ou inexistente → 404. Conta Free exibe sempre `indigo`.
- `GET /u/{slug}`: página HTML do servidor (Open Graph, `noindex`), fora de `/api`. `GET /u/{slug}/og.png`: PNG 1200×630 (SkiaSharp), cache de 1 h.

## Parceiro de responsabilidade

Um usuário (dono) convida outro para ver o resumo semanal dele. Aceitam cookie ou chave de API.

- `POST /api/partners/invites` → **201** `{ id, link, expiresAt }` (vale 7 dias; no máximo 10 convites pendentes, senão 400).
- `POST /api/partners/accept` `{ token }` → `{ id, owner: { displayName } }`. Convite inexistente, expirado ou usado → 404; o próprio convite → 400 `validation`; parceria repetida → 409.
- `GET /api/partners` → `{ sharingWith: [{ id, displayName, since }], sharedWithMe: [{ id, displayName, since }], pendingInvites: [{ id, createdAt, expiresAt }] }`.
- `GET /api/partners/{id}/summary`: só quem recebe o compartilhamento (senão 404) → `{ owner: { displayName, level }, weeks: [{ start, end, habits: [{ name, level, done, minimum, expected, rate, streak }], totals }] }`. Duas semanas: a atual (segunda até hoje) e a anterior.
- `DELETE /api/partners/{id}` → **204** (qualquer um dos lados).

## Admin

- `GET /api/admin/metrics` → `{ users: { total, verified, demo, pro }, signupsByDay, checkInsByDay (30 dias), activeUsers: { d1, d7, d30 }, retention: [{ cohortWeek, size, week1, week2, week4 }] (8 coortes; frações de 0 a 1) }`.
  Quem não está em `Admin:Emails` recebe **404**. Contas demo ficam de fora, exceto na contagem.
- `POST /api/admin/sentry-test` → força um **500** (`internal_error`) para testar o Sentry. Quem não está em `Admin:Emails` recebe **404**.

## Documentação interativa (OpenAPI)

- `GET /api/openapi/v1.json`: documento OpenAPI (anônimo). `GET /api/docs`: UI do Scalar (anônimo), com os esquemas cookie e Bearer.
  Rotas novas trazem `.WithTags`, `.WithSummary` e `.Produces<T>`; as antigas ainda não têm os tipos de resposta.

## Chaves de API

Só com o cookie do app: com uma chave, estas rotas devolvem **403** `forbidden`.

- `GET /api/api-keys` → `Array<{ id, name, prefix, createdAt, lastUsedAt: string | null }>` (mais recentes primeiro).
  `prefix` = os 12 primeiros caracteres da chave; `lastUsedAt` é atualizado no máximo 1×/min.
- `POST /api/api-keys` `{ name }` (1 a 60 caracteres; no máximo 20 chaves; Free: 1 chave, acima disso 403 `plan_limit`) → **201**
  `{ id, name, prefix, createdAt, key }`. `key` (`arco_` + 43 caracteres) aparece **só nesta resposta**; o servidor
  guarda apenas o SHA-256.
- `DELETE /api/api-keys/{id}` → **204** (revoga na hora). Inexistente → 404.

Exemplo: `curl -H "Authorization: Bearer arco_…" https://<host>/api/today`

## Servidor MCP

- `POST /api/mcp`: servidor [MCP](https://modelcontextprotocol.io) (Streamable HTTP, sem estado), autenticado como o
  resto de `/api` (na prática, com chave de API). Só no plano Pro (Free → 403 `plan_limit`). Para assistentes de IA operarem o Arco.
- As ferramentas chamam esta própria API com a chave de quem chamou, então seguem as mesmas regras, XP e mensagens.
  Erros da API voltam como erro da ferramenta (`isError`), com a mensagem pt-BR.
- Ferramentas: `ver_hoje`, `fazer_checkin` (aceita `minutos?`), `desfazer_checkin`, `registrar_foco`, `ver_arco`, `listar_habitos`, `ver_habito`, `criar_habito`,
  `subir_nivel`, `voltar_nivel`, `ver_ciclo_atual`, `atualizar_objetivo`, `marcar_marco`, `ver_revisao_semanal`,
  `fazer_revisao_semanal`, `ver_estatisticas`, `ver_conquistas`, `ver_parceiros`, `ver_resumo_parceiro(id)`,
  `configurar_perfil_publico(ativo?, slug?)` (sem argumentos só devolve o estado e o link), `ver_plano`,
  `ler_documentacao_api` (este arquivo) e `chamar_api` (qualquer rota; exceto `/api/backup`, `/api/api-keys`, `/api/auth/*` e o próprio `/api/mcp`).
- Conectar no Claude Code:
  `claude mcp add --transport http arco https://<host>/api/mcp --header "Authorization: Bearer arco_…"`

## Health

- `GET /api/health` → `{ status: "healthy", version: "x.y.z" }` (faz ping no banco).

## Today (tela principal)

`GET /api/today` →

```ts
{
  date: string;                       // hoje no fuso configurado
  player: Player;
  xpToday: number;                    // soma dos XpEvents com data = hoje
  cycle: Cycle | null;                // ciclo ativo
  goals: Goal[];                      // objetivos do ciclo ativo
  habits: Array<Habit & {
    isDueToday: boolean;              // Daily: true; SpecificDays: hoje está na lista; WeeklyTarget: weekCount < timesPerWeek
    weekCount: number;                // check-ins Done/Minimum na semana atual (segunda a domingo)
    weekTarget: number | null;        // timesPerWeek em WeeklyTarget, senão null
    checkIn: CheckIn | null;          // check-in de hoje
    goalTitle: string | null;
    consistency: Consistency;
    levelUpSuggested: boolean;        // ver "Sugestão de subir de nível"
    levelUpWeeksDone: number | null;  // semanas seguidas que contam para a regra de semanas (null na regra padrão)
    levelDownSuggested: boolean;      // ver "Sugestão de voltar um nível"
    streak: Streak;
  }>;                                 // só não arquivados; ordem: devidos hoje primeiro, depois por nome
  weeklyReview: {
    weekStart: string; weekEnd: string; // semana revisável (ver "Revisão semanal")
    due: boolean;                     // a semana teve check-ins e ainda não foi revisada
  };
}
```

## Check-ins

- `PUT /api/checkins` `{ habitId, date, status, minutes? }`: cria ou atualiza o check-in do dia. Responde **200** `{ checkIn: CheckIn, xpDelta: number, player: Player }`.
  - Data futura ou mais de 7 dias atrás → 400 `validation`.
  - Hábito arquivado → 400 `validation`.
  - `minutes` (0–480, opcional): ausente = não mexe; `0` = remove a sessão de foco Manual do dia; `>0` = cria/substitui a única sessão Manual de (hábito, data). `minutes > 0` com status `Missed` → 400 `validation`. `checkIn.minutes` (`number | null`) devolve os minutos manuais do dia; aparece também em `GET /api/today` e no `recentCheckIns` de `GET /api/habits/{id}`.
- `DELETE /api/checkins/{habitId}/{date}` → **200** `{ xpDelta, player }` (xpDelta ≤ 0). Inexistente → 404. Remove também a sessão de foco Manual do dia (as de Timer permanecem).
- `GET /api/checkins?from=YYYY-MM-DD&to=YYYY-MM-DD&habitId=` → `CheckIn[]` (intervalo máximo de 366 dias; `habitId` opcional filtra um hábito).

## Hábitos

- `GET /api/habits?includeArchived=false` → `Habit[]`
- `GET /api/habits/{id}` → `Habit & { recentCheckIns: CheckIn[], consistency: Consistency, levelUpSuggested: boolean, levelUpWeeksDone: number | null, levelDownSuggested: boolean, streak: Streak }` (check-ins dos últimos 28 dias)
- `POST /api/habits` → **201** `Habit`. Corpo:

  ```
  { name, emoji?, goalId?, frequencyType, frequencyDays?, timesPerWeek?,
    levelDescription, minimumDescription, reminderTime?,
    plannedLevels?: Array<{ description, minimumDescription }>,
    levelUpWeeks?, levelUpDaysPerWeek? }
  ```

  Cria o nível 1.
- `PUT /api/habits/{id}` → `Habit`. Corpo:

  ```
  { name, emoji?, goalId?, frequencyType, frequencyDays?, timesPerWeek?,
    levelDescription, minimumDescription, reminderTime?,
    plannedLevels?: Array<{ description, minimumDescription }>,
    levelUpWeeks?, levelUpDaysPerWeek? }
  ```

  `levelDescription` e `minimumDescription` editam o nível atual. `reminderTime` ausente ou null remove o lembrete.
  `plannedLevels` substitui a escada inteira (ausente = sem níveis planejados). Cada item usa os mesmos nomes de
  `Habit.plannedLevels`, então a escada lida no GET pode voltar como está no PUT (o `level` é ignorado: a ordem define
  a numeração). O nome antigo `levelDescription` no lugar de `description` ainda é aceito, mas está obsoleto. `levelUpWeeks` ausente ou null volta à
  regra padrão.
- `POST /api/habits/{id}/level-up` `{ levelDescription?, minimumDescription? }` → `{ habit: Habit, xpDelta: 50, player }`.
  Campo ausente ou vazio usa o do primeiro nível planejado; havendo nível planejado, ele é consumido (sai da escada).
  Sem nível planejado, os dois campos são obrigatórios.
- `POST /api/habits/{id}/level-up-suggestion/dismiss` → **204**. Esconde a sugestão por 7 dias.
- `POST /api/habits/{id}/level-down` → `Habit`. Volta um nível: o nível atual sai do histórico e vira o primeiro nível
  planejado. Não mexe no XP. Nível 1 ou hábito arquivado → 400.
- `POST /api/habits/{id}/level-down-suggestion/dismiss` → **204**. Esconde a sugestão de voltar por 7 dias.
- `POST /api/habits/{id}/archive` → `Habit`; `POST /api/habits/{id}/restore` → `Habit`.

Sugestão de subir de nível (`levelUpSuggested`): hábito não arquivado e sem adiamento ativo, e:
- regra padrão: `consistency.rate ≥ 0.8`, `expected > 0`, a janela inteira no nível atual (nível alcançado em `from`
  ou antes, no fuso configurado);
- regra de semanas: `levelUpWeeksDone ≥ levelUpWeeks`. Contam semanas (segunda a domingo) seguidas, inteiras no nível
  atual, com pelo menos `levelUpDaysPerWeek` dias com Feito ou Mínimo. A semana atual entra assim que bate a meta.

Subir de nível limpa o adiamento.

Sugestão de voltar um nível (`levelDownSuggested`): nível ≥ 2, não arquivado, sem adiamento ativo, sem sugestão de
subir, e as 2 últimas semanas terminadas (segunda a domingo), inteiras no nível atual, com menos da metade da meta
semanal em dias com Feito ou Mínimo. Meta = `levelUpDaysPerWeek` se houver; senão Daily 7, SpecificDays = quantidade
de dias, WeeklyTarget = `timesPerWeek`. Subir ou voltar de nível limpa os adiamentos.

Validação dos hábitos:
- `name` com 1 a 80 caracteres.
- `emoji` com até 8 caracteres.
- `levelDescription` e `minimumDescription` com 1 a 120 caracteres (também em cada nível planejado).
- `plannedLevels` com no máximo 10 níveis.
- `levelUpWeeks` de 1 a 8 e `levelUpDaysPerWeek` de 1 a 7 (os dois juntos).
- `SpecificDays` exige ao menos 1 dia entre 0 e 6, sem repetições.
- `WeeklyTarget` exige `timesPerWeek` entre 1 e 7.
- `goalId` precisa existir.
- `reminderTime`, se informado, no formato `"HH:mm"` (24h).

## Ciclos

- `GET /api/cycles` → `Cycle[]` (mais recentes primeiro)
- `GET /api/cycles/current` → `Cycle` ou **204** quando não há ciclo ativo.
- `POST /api/cycles` `{ name, startDate?, weeks?, weeklyFocusMinutes? }` → **201** `Cycle`.
  - `startDate` padrão = hoje e `weeks` padrão = 6 (só números pares de 2 a 12; senão 400 `validation`). `weeklyFocusMinutes` entre 60 e 3360 ("A meta semanal de foco deve ficar entre 60 e 3360 minutos.").
  - Se já existe ciclo ativo → 409 `active_cycle_exists`.
- `PUT /api/cycles/{id}` `{ name, endDate, weeklyFocusMinutes? }` → `Cycle`. Só para ciclos ativos; `endDate` precisa ser ≥ `startDate`.
- `GET /api/cycles/{id}/arch` → o ciclo como arco de pedras (uma por semana), 404 se inexistente:

  ```ts
  { cycleId: number; startDate: string; endDate: string; closed: boolean; weeks: number;
    layout: "Arch" | "Row";            // Arch para semanas pares (>= 2); Row para as demais (ex.: endDate editado)
    weeklyTargetMinutes: number; totalMinutes: number;
    habits: Array<{ habitId, name, emoji }>;   // hábitos com foco no ciclo
    stones: Array<{ week: number; from: string; to: string; minutes: number;
      byHabit: Array<{ habitId, minutes }>;
      days: Array<{ date: string; minutes: number;
        bricks: Array<{ sessionId, habitId, minutes, source: "Timer" | "Manual" }> }> }> }
  ```

- `GET /api/cycles/{id}/summary` → números para a revisão de encerramento, de `startDate` até min(`endDate`, hoje):

  ```ts
  { cycle: Cycle; from: string; to: string;
    xp: number;                       // soma dos XpEvents com data no intervalo
    checkIns: CheckInTotals;
    goals: Goal[];
    habits: HabitPeriodStats[];       // ordem: maior rate primeiro
    weeklyReviews: WeeklyReview[] }   // semanas que tocam o intervalo
  ```

- `POST /api/cycles/{id}/complete` `{ reviewNotes?, wins?, obstacles?, next? }` → `{ cycle, xpDelta: 100, player }`.
  - `reviewNotes` até 4000 caracteres; `wins`, `obstacles` e `next` até 2000 cada. Todos opcionais.
  - Ciclo já concluído → 409 `conflict`.
- `DELETE /api/cycles/{id}` → `{ xpDelta, player, achievements: [] }` (xpDelta ≤ 0). Exclui o ciclo, ativo ou
  encerrado, com seus objetivos, marcos e atualizações, e remove todo o XP que eles deram (encerramento, objetivos
  concluídos e marcos). Os hábitos continuam, com `goalId = null` nos que estavam ligados. Conquistas que deixam de
  valer são retiradas. Inexistente → 404.

## Objetivos

- `GET /api/goals?cycleId={id}` → `Goal[]`. Sem `cycleId`, devolve os do ciclo ativo.
- `POST /api/goals` → **201** `Goal`. Corpo:

  ```
  { cycleId, title, description?, type, unit?, startValue?, targetValue?,
    deadline?, milestones?: string[] }
  ```

  - `Numeric` exige `startValue` e `targetValue`, que devem ser diferentes.
  - `Milestones` exige de 1 a 20 marcos.
- `PUT /api/goals/{id}` → `Goal`. Corpo: `{ title, description?, unit?, targetValue?, deadline? }`.
- `DELETE /api/goals/{id}` → `{ xpDelta, player, achievements: [] }` (xpDelta ≤ 0). Remove o XP que o objetivo deu
  (conclusão e marcos); hábitos ligados a ele ficam com `goalId = null`; conquistas que deixam de valer são retiradas.
  Inexistente → 404.
- `POST /api/goals/{id}/progress` `{ value, note? }`, só para Numeric → `{ goal, xpDelta, player }`.
  - Atingir a meta conclui o objetivo automaticamente e dá +150 XP.
  - Meta crescente: concluído quando `value ≥ target`. Meta decrescente: concluído quando `value ≤ target`.
- `POST /api/goals/{id}/milestones/{milestoneId}/toggle` → `{ goal, xpDelta, player }`.
  - Concluir um marco dá +25 XP; reabrir remove esses 25 XP.
  - Concluir o último marco também conclui o objetivo (+150 XP). Reabrir um marco de objetivo concluído reabre o objetivo e remove os 150 XP.
- `POST /api/goals/{id}/complete` → `{ goal, xpDelta, player }`. Conclusão manual com +150 XP. Se já estava concluído → 409.

## Player

- `GET /api/player` → `Player & { recentEvents: Array<{ date, amount, source: XpSource, description }> }` (últimos 20).

## Estatísticas

- `GET /api/stats` →

  ```ts
  {
    weeks: Array<{                     // 12 semanas (segunda a domingo), da mais antiga à atual
      weekStart: string; weekEnd: string;
      xp: number;                      // soma dos XpEvents da semana
      done: number; minimum: number; missed: number;
      expected: number;                // soma dos esperados por hábito (mesma regra das revisões)
      rate: number;                    // (done + minimum) / expected, 0..1; 0 quando expected = 0
    }>;                                // a semana atual conta só até hoje
    recent: {                          // últimos 30 dias, terminando hoje
      from: string; to: string; xp: number;
      checkIns: CheckInTotals; habits: HabitPeriodStats[];
    };
  }
  ```

## Foco e arco

Sessões de foco (tempo dedicado a um hábito). Não dão XP. Os minutos enchem as pedras do arco do ciclo (`GET /api/cycles/{id}/arch`).

- `POST /api/focus-sessions` `{ habitId, minutes, date?, startedAt? }`: cria sessão `Timer` (minutes 1–480). `date` padrão = hoje; no máximo 7 dias atrás, nunca futura. Hábito inexistente → 404; arquivado → 400 `validation`. Responde **200** `{ session: FocusSession }`.
- `DELETE /api/focus-sessions/{id}` → **204** sem corpo. Inexistente → 404.
- `GET /api/focus-sessions?from=YYYY-MM-DD&to=YYYY-MM-DD&habitId=` (intervalo máximo de 366 dias) →
  `{ sessions: FocusSession[], totalMinutes: number, byHabit: Array<{ habitId, name, emoji, minutes, sessions }> }`.
- `FocusSession`: `{ id, habitId, date, minutes, source: "Timer" | "Manual", startedAt: string | null }`.
- Estatísticas: `minutes` (soma de todas as sessões) em `weeks[]` e `recent` de `/api/stats`, em `HabitPeriodStats`, e também no ciclo e na revisão semanal.
- Ferramentas MCP: `registrar_foco(habitId, minutos, data?)`, `ver_arco(cicloId?)` e `fazer_checkin` com `minutos?`.

## Exportação

- `GET /api/backup` → JSON (`arco-export-AAAA-MM-DD.json`) com tudo o que o usuário atual possui (ciclos, objetivos,
  hábitos, check-ins, foco, XP, conquistas, revisões, configurações). Não inclui hashes de chaves de API nem chaves de push.
- O servidor continua fazendo a cópia diária do SQLite inteiro (14 dias), mas ela não é exposta pela API;
  `GET /api/backups` foi removido.

## Conquistas

- `GET /api/achievements` → `Achievement[]`, o catálogo inteiro na ordem de exibição:

  ```ts
  interface Achievement {
    key: string;                       // ex.: "streak-7"
    emoji: string; title: string; description: string;
    target: number;
    progress: number;                  // 0..target (= target quando desbloqueada)
    unlockedAt: string | null;         // null enquanto bloqueada
  }
  ```

  Desbloqueio: depois de cada ação que dá XP (a resposta traz as novas em `achievements`) e na subida da API (em
  silêncio, para dados antigos). Não dá XP. Desfazer um check-in nunca a revoga; só excluir dados (um ciclo ou um
  objetivo) retira as conquistas que deixaram de valer. Catálogo em `Domain/AchievementCatalog.cs`.

## Revisão semanal

A semana revisável é a semana atual no domingo (ela termina hoje) e a anterior de segunda a sábado.
Cada semana tem no máximo uma revisão.

- `GET /api/reviews/weekly/current` →

  ```ts
  { weekStart: string; weekEnd: string;
    review: WeeklyReview | null;      // null enquanto não foi feita
    xp: number;                       // XP com data na semana
    checkIns: CheckInTotals;
    habits: HabitPeriodStats[];
    cycle: Cycle | null; goals: Goal[] } // ciclo ativo e seus objetivos
  ```

- `GET /api/reviews/weekly` → `WeeklyReview[]` (mais recentes primeiro).
- `POST /api/reviews/weekly` `{ weekStart, wins?, obstacles?, next? }` → **201** `{ review, xpDelta: 30, player }`.
  - `weekStart` diferente da semana revisável → 400 `validation`.
  - Pelo menos uma resposta não vazia; cada uma até 2000 caracteres → senão 400.
  - Semana já revisada → 409 `conflict`.

## Push

Web Push com VAPID (RFC 8291/8292). As chaves vêm de `Push:VapidPublicKey`/`Push:VapidPrivateKey`
(base64url) ou são geradas uma vez em `vapid.json`, ao lado do banco. `Push:Subject` (padrão
`mailto:arco@example.com`) vai no JWT do VAPID.

- `GET /api/push/config` → `{ publicKey: string, subscriptions: number }` (`publicKey` em base64url, para `applicationServerKey`).
- `POST /api/push/subscriptions` `{ endpoint, keys: { p256dh, auth } }` (o `PushSubscription.toJSON()` do navegador) → **204**.
  Cria ou atualiza pelo `endpoint`. `endpoint` precisa ser https; `p256dh` um ponto P-256 não comprimido e `auth` 16 bytes (base64url) → senão 400.
- `POST /api/push/subscriptions/remove` `{ endpoint }` → **204** (idempotente).
- `POST /api/push/test` → `{ sent: number }`: manda uma notificação de teste para todos os dispositivos.

Payload entregue ao service worker: `{ title, body, url, tag }`. O push service respondendo 404/410
apaga a inscrição; 10 falhas seguidas também.

Lembretes (verificados a cada minuto, no fuso configurado, enviados no máximo uma vez):
- **Hábito**: a partir de `reminderTime`, nos dias em que o hábito é devido e ainda não tem check-in hoje.
- **Revisão semanal**: no dia e horário de `weeklyReviewReminder`, se a semana revisável ainda não foi revisada.
- Um lembrete com mais de 2 h de atraso (ex.: servidor fora do ar) é pulado, não enviado tarde.

## Settings

- `GET /api/settings` →

  ```ts
  { timezone: string;
    weeklyReviewReminder: { enabled: boolean; day: number; time: string } } // day 0=domingo..6; time "HH:mm"
  ```

  Padrão do lembrete: ativado, domingo, 18:00.
- `PUT /api/settings` `{ timezone?, weeklyReviewReminder?: { enabled?, day?, time? } }` → o mesmo formato do GET.
  Campos ausentes ficam como estão. O fuso precisa ser um id IANA válido; `day` entre 0 e 6; `time` em `"HH:mm"`.
