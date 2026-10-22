# Arco — Produto

App pessoal (single-user) de hábitos com **progresso e objetivo**: cada check-in alimenta algo maior.

```
Ciclo (6 semanas)           → começo, meio e fim; objetivos do ciclo; encerramento com revisão
 └─ Objetivo (com prazo)    → "Correr 10km até 30/11" — barra de progresso até a meta
     └─ Hábito (com níveis) → "Correr — Nível 2: 20 min, 3x/semana"
         └─ Check-in diário → Feito / Mínimo / Não fiz → gera XP
```

## Conceitos

### Ciclo
- Duração padrão de 6 semanas (42 dias), configurável na criação (1–12 semanas).
- No máximo **um ciclo ativo** por vez.
- Tem de 0 a 3 objetivos (o limite é recomendação de UI, não regra rígida do backend).
- Encerrar o ciclo pede uma nota de revisão (texto livre) e dá XP.
- Hábitos **não** pertencem ao ciclo: atravessam ciclos.

### Objetivo
- Pertence a um ciclo. Tem `deadline` (padrão: fim do ciclo).
- Dois tipos:
  - **Numérico**: `startValue → targetValue` com `unit` (ex.: 3 → 10 km). O progresso é registrado como atualizações (`GoalUpdate`) e o valor atual é a última atualização. A meta pode ser crescente ou decrescente (ex.: peso 90 → 80).
  - **Marcos**: uma lista ordenada de marcos; o progresso é concluídos/total.
- Concluir um objetivo dá XP. Ao atingir a meta (numérico) ou todos os marcos, o objetivo é concluído automaticamente.

### Hábito com níveis
- Frequência: `Daily`, `SpecificDays` (0=domingo … 6=sábado) ou `WeeklyTarget` (N vezes por semana, semana começa na segunda).
- Ligação opcional a um objetivo.
- Cada nível tem uma **descrição** (a dose: "20 min") e uma **versão mínima** ("5 min").
- Nível atual = o maior nível. "Subir de nível" cria o próximo nível (com nova descrição e mínimo) e dá XP. Na Fase 1 é manual; a sugestão automática vem na Fase 2.
- Arquivar e restaurar.

### Check-in
- Um por hábito por dia: `Done` (Feito), `Minimum` (Mínimo) ou `Missed` (Não fiz).
- Pode ser alterado ou desfeito. Datas futuras são proibidas; até 7 dias no passado são permitidos.
- "Hoje" é calculado no fuso configurado (padrão `America/Sao_Paulo`).

### XP e nível do jogador
O XP fica num livro-razão (`XpEvent`); o total é a soma dos eventos.

| Evento | XP |
|---|---|
| Check-in Feito | 10 + 2 × (nível do hábito − 1) |
| Check-in Mínimo | metade do Feito, arredondado para baixo |
| Check-in Não fiz | 0 |
| Subir de nível um hábito | 50 |
| Marco de objetivo concluído | 25 |
| Objetivo concluído | 150 |
| Ciclo encerrado | 100 |

- Alterar um check-in substitui o XP dele, e desfazer remove esse XP. Reabrir um marco também remove o XP correspondente.
- Nível do jogador: começa no 1. Para passar do nível L ao L+1 são necessários `100 + 50 × (L − 1)` XP (100, 150, 200, …).

## Fases
1. **MVP** — ciclo, objetivos, hábitos com níveis, check-in, XP e nível de jogador, PWA instalável, login.
2. **Retenção** — push (lembrete por hábito e revisão semanal), revisão semanal e de fim de ciclo guiadas, sugestão de subir de nível (≥ 80% de consistência em 14 dias).
3. **Polimento** — streaks com tolerância (1 falha por semana), conquistas, histórico e estatísticas.
