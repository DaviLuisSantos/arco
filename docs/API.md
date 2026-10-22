# Arco — Contrato da API (Fase 2)

## Convenções

- Todas as rotas ficam sob `/api`. JSON em camelCase e enums como string.
- Datas de calendário são `"YYYY-MM-DD"` (DateOnly). Instantes são ISO-8601 UTC com `Z`.
- Ids são `int` (autoincremento).
- Autenticação por cookie HttpOnly `arco_auth` (SameSite=Lax, Secure fora de Development, sliding de 30 dias). Sem cookie válido, toda rota `/api/*`, exceto `/api/auth/login`, `/api/auth/me` e `/api/health`, devolve **401** (nunca redirect).
- Erros: `{ "error": "mensagem pt-BR", "code": "snake_case" }`.

| Status | Codes |
|---|---|
| 400 | `validation` |
| 401 | `unauthorized`, `invalid_password` |
| 404 | `not_found` |
| 409 | `active_cycle_exists`, `conflict` |
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

- `POST /api/auth/login` `{ password }` → **204** e define o cookie. Senha errada → 401 `invalid_password`. Rate limit de 5/min por IP → 429.
- `POST /api/auth/logout` → **204**.
- `GET /api/auth/me` → **200** `{ authenticated: true }` ou **401**.

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
    streak: Streak;
  }>;                                 // só não arquivados; ordem: devidos hoje primeiro, depois por nome
  weeklyReview: {
    weekStart: string; weekEnd: string; // semana revisável (ver "Revisão semanal")
    due: boolean;                     // a semana teve check-ins e ainda não foi revisada
  };
}
```

## Check-ins

- `PUT /api/checkins` `{ habitId, date, status }`: cria ou atualiza o check-in do dia. Responde **200** `{ checkIn: CheckIn, xpDelta: number, player: Player }`.
  - Data futura ou mais de 7 dias atrás → 400 `validation`.
  - Hábito arquivado → 400 `validation`.
- `DELETE /api/checkins/{habitId}/{date}` → **200** `{ xpDelta, player }` (xpDelta ≤ 0). Inexistente → 404.
- `GET /api/checkins?from=YYYY-MM-DD&to=YYYY-MM-DD&habitId=` → `CheckIn[]` (intervalo máximo de 366 dias; `habitId` opcional filtra um hábito).

## Hábitos

- `GET /api/habits?includeArchived=false` → `Habit[]`
- `GET /api/habits/{id}` → `Habit & { recentCheckIns: CheckIn[], consistency: Consistency, levelUpSuggested: boolean, levelUpWeeksDone: number | null, streak: Streak }` (check-ins dos últimos 28 dias)
- `POST /api/habits` → **201** `Habit`. Corpo:

  ```
  { name, emoji?, goalId?, frequencyType, frequencyDays?, timesPerWeek?,
    levelDescription, minimumDescription, reminderTime?,
    plannedLevels?: Array<{ levelDescription, minimumDescription }>,
    levelUpWeeks?, levelUpDaysPerWeek? }
  ```

  Cria o nível 1.
- `PUT /api/habits/{id}` → `Habit`. Corpo:

  ```
  { name, emoji?, goalId?, frequencyType, frequencyDays?, timesPerWeek?,
    levelDescription, minimumDescription, reminderTime?,
    plannedLevels?: Array<{ levelDescription, minimumDescription }>,
    levelUpWeeks?, levelUpDaysPerWeek? }
  ```

  `levelDescription` e `minimumDescription` editam o nível atual. `reminderTime` ausente ou null remove o lembrete.
  `plannedLevels` substitui a escada inteira (ausente = sem níveis planejados). `levelUpWeeks` ausente ou null volta à
  regra padrão.
- `POST /api/habits/{id}/level-up` `{ levelDescription?, minimumDescription? }` → `{ habit: Habit, xpDelta: 50, player }`.
  Campo ausente ou vazio usa o do primeiro nível planejado; havendo nível planejado, ele é consumido (sai da escada).
  Sem nível planejado, os dois campos são obrigatórios.
- `POST /api/habits/{id}/level-up-suggestion/dismiss` → **204**. Esconde a sugestão por 7 dias.
- `POST /api/habits/{id}/archive` → `Habit`; `POST /api/habits/{id}/restore` → `Habit`.

Sugestão de subir de nível (`levelUpSuggested`): hábito não arquivado e sem adiamento ativo, e:
- regra padrão: `consistency.rate ≥ 0.8`, `expected > 0`, a janela inteira no nível atual (nível alcançado em `from`
  ou antes, no fuso configurado);
- regra de semanas: `levelUpWeeksDone ≥ levelUpWeeks`. Contam semanas (segunda a domingo) seguidas, inteiras no nível
  atual, com pelo menos `levelUpDaysPerWeek` dias com Feito ou Mínimo. A semana atual entra assim que bate a meta.

Subir de nível limpa o adiamento.

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
- `POST /api/cycles` `{ name, startDate?, weeks? }` → **201** `Cycle`.
  - `startDate` padrão = hoje e `weeks` padrão = 6 (aceita 1 a 12).
  - Se já existe ciclo ativo → 409 `active_cycle_exists`.
- `PUT /api/cycles/{id}` `{ name, endDate }` → `Cycle`. Só para ciclos ativos; `endDate` precisa ser ≥ `startDate`.
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
- `DELETE /api/goals/{id}` → **204**. Hábitos ligados a ele ficam com `goalId = null`.
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

## Backup

- `GET /api/backup` → arquivo SQLite (`application/vnd.sqlite3`, `arco-AAAA-MM-DD.db`): cópia consistente do banco
  feita na hora (`VACUUM INTO`).
- `GET /api/backups` → `Array<{ date: string; sizeBytes: number }>`: as cópias automáticas diárias guardadas no
  servidor (últimos 14 dias), da mais recente à mais antiga.

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
  silêncio, para dados antigos). Nunca é revogada e não dá XP. Catálogo em `Domain/AchievementCatalog.cs`.

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
