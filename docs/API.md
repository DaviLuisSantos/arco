# Arco — Contrato da API (Fase 1)

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

## Tipos compartilhados

```ts
type FrequencyType = 'Daily' | 'SpecificDays' | 'WeeklyTarget';
type CheckInStatus = 'Done' | 'Minimum' | 'Missed';
type GoalType = 'Numeric' | 'Milestones';
type XpSource = 'CheckIn' | 'HabitLevelUp' | 'Milestone' | 'GoalCompleted' | 'CycleCompleted';

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
  reviewNotes: string | null;
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

interface CheckIn { habitId: number; date: string; status: CheckInStatus; xp: number; }

interface Habit {
  id: number; name: string; emoji: string | null;
  goalId: number | null;
  frequencyType: FrequencyType;
  frequencyDays: number[];             // 0=domingo..6=sábado, só em SpecificDays
  timesPerWeek: number | null;         // 1..7, só em WeeklyTarget
  level: number;                       // nível atual
  levelDescription: string;            // descrição do nível atual
  minimumDescription: string;
  levels: HabitLevel[];                // crescente
  archivedAt: string | null;
  createdAt: string;
}
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
  }>;                                 // só não arquivados; ordem: devidos hoje primeiro, depois por nome
}
```

## Check-ins

- `PUT /api/checkins` `{ habitId, date, status }`: cria ou atualiza o check-in do dia. Responde **200** `{ checkIn: CheckIn, xpDelta: number, player: Player }`.
  - Data futura ou mais de 7 dias atrás → 400 `validation`.
  - Hábito arquivado → 400 `validation`.
- `DELETE /api/checkins/{habitId}/{date}` → **200** `{ xpDelta, player }` (xpDelta ≤ 0). Inexistente → 404.
- `GET /api/checkins?from=YYYY-MM-DD&to=YYYY-MM-DD` → `CheckIn[]` (intervalo máximo de 366 dias).

## Hábitos

- `GET /api/habits?includeArchived=false` → `Habit[]`
- `GET /api/habits/{id}` → `Habit & { recentCheckIns: CheckIn[] }` (últimos 28 dias)
- `POST /api/habits` → **201** `Habit`. Corpo:

  ```
  { name, emoji?, goalId?, frequencyType, frequencyDays?, timesPerWeek?,
    levelDescription, minimumDescription }
  ```

  Cria o nível 1.
- `PUT /api/habits/{id}` → `Habit`. Corpo:

  ```
  { name, emoji?, goalId?, frequencyType, frequencyDays?, timesPerWeek?,
    levelDescription, minimumDescription }
  ```

  `levelDescription` e `minimumDescription` editam o nível atual.
- `POST /api/habits/{id}/level-up` `{ levelDescription, minimumDescription }` → `{ habit: Habit, xpDelta: 50, player }`.
- `POST /api/habits/{id}/archive` → `Habit`; `POST /api/habits/{id}/restore` → `Habit`.

Validação dos hábitos:
- `name` com 1 a 80 caracteres.
- `emoji` com até 8 caracteres.
- `levelDescription` e `minimumDescription` com 1 a 120 caracteres.
- `SpecificDays` exige ao menos 1 dia entre 0 e 6, sem repetições.
- `WeeklyTarget` exige `timesPerWeek` entre 1 e 7.
- `goalId` precisa existir.

## Ciclos

- `GET /api/cycles` → `Cycle[]` (mais recentes primeiro)
- `GET /api/cycles/current` → `Cycle` ou **204** quando não há ciclo ativo.
- `POST /api/cycles` `{ name, startDate?, weeks? }` → **201** `Cycle`.
  - `startDate` padrão = hoje e `weeks` padrão = 6 (aceita 1 a 12).
  - Se já existe ciclo ativo → 409 `active_cycle_exists`.
- `PUT /api/cycles/{id}` `{ name, endDate }` → `Cycle`. Só para ciclos ativos; `endDate` precisa ser ≥ `startDate`.
- `POST /api/cycles/{id}/complete` `{ reviewNotes }` → `{ cycle, xpDelta: 100, player }`. Ciclo já concluído → 409 `conflict`.

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

## Settings

- `GET /api/settings` → `{ timezone }`
- `PUT /api/settings` `{ timezone }`: o fuso precisa ser um id IANA válido.
