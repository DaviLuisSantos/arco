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
- Encerrar o ciclo é uma revisão guiada: primeiro os números do ciclo (objetivos, consistência por hábito, XP, revisões semanais), depois três perguntas (o que funcionou, o que não funcionou, o que levar para o próximo) e notas livres. Dá XP.
- Quando o ciclo chega ao fim, a tela Hoje e a do ciclo convidam para a revisão (o encerramento continua manual).
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
- Nível atual = o maior nível. "Subir de nível" cria o próximo nível (com nova descrição e mínimo) e dá XP. A decisão é sempre manual.
- **Sugestão de subir de nível**: com ≥ 80% de consistência nos últimos 14 dias, todos no nível atual, o app sugere subir (tela Hoje e página do hábito).
  - Só "Feito" conta: o Mínimo mantém o hábito vivo, mas não mostra que a dose ficou fácil.
  - A janela termina hoje se hoje já tem check-in; senão, ontem (um "hoje" ainda não marcado não derruba a taxa).
  - Esperado: Daily = 14; SpecificDays = dias da lista na janela; WeeklyTarget = N × 2.
  - "Agora não" esconde a sugestão por 7 dias.
- Lembrete opcional por hábito (horário local): push nos dias em que ele é devido e ainda não foi marcado.
- Arquivar e restaurar.

### Check-in
- Um por hábito por dia: `Done` (Feito), `Minimum` (Mínimo) ou `Missed` (Não fiz).
- Pode ser alterado ou desfeito. Datas futuras são proibidas; até 7 dias no passado são permitidos.
- "Hoje" é calculado no fuso configurado (padrão `America/Sao_Paulo`).

### Revisão semanal
- Revisa a semana de segunda a domingo. No domingo a semana revisável é a atual; de segunda a sábado, a anterior.
- Mostra os números da semana (XP, check-ins, consistência por hábito, objetivos do ciclo) e pergunta: o que funcionou, o que atrapalhou e qual o foco da próxima semana. Ao menos uma resposta.
- Uma por semana; dá XP. A tela Hoje avisa quando a semana revisável teve check-ins e ainda não foi revisada.
- Lembrete push configurável (padrão: domingo, 18:00).

### Notificações
- Web Push (VAPID), por dispositivo, ativado no Perfil. No iPhone/iPad exige o app instalado na tela de início.
- Lembretes de hábito e da revisão semanal saem no máximo uma vez; com mais de 2 h de atraso são pulados.

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
| Revisão semanal | 30 |

- Alterar um check-in substitui o XP dele, e desfazer remove esse XP. Reabrir um marco também remove o XP correspondente.
- Nível do jogador: começa no 1. Para passar do nível L ao L+1 são necessários `100 + 50 × (L − 1)` XP (100, 150, 200, …).

## Fases
1. **MVP** — ciclo, objetivos, hábitos com níveis, check-in, XP e nível de jogador, PWA instalável, login.
2. **Retenção** — push (lembrete por hábito e revisão semanal), revisão semanal e de fim de ciclo guiadas, sugestão de subir de nível (≥ 80% de consistência em 14 dias).
3. **Polimento** — streaks com tolerância (1 falha por semana), conquistas, histórico e estatísticas.

## Pendências

### Uso offline (nível 2: ler e marcar check-in sem internet)
Hoje o service worker pré-cacheia só o app shell: o app abre offline, mas as telas falham por falta de dados.
- **Ler offline:** persistir o cache do TanStack Query no aparelho (IndexedDB) e mostrar a última resposta de
  cada tela com o aviso "Offline — dados de HH:mm". O service worker continua sem cachear `/api`.
- **Check-in offline:** check-ins feitos sem rede entram numa fila persistida, aparecem na hora (atualização
  otimista, XP estimado) e são enviados em ordem quando a conexão volta; o servidor recalcula o XP oficial.
  Cada check-in já leva a própria data e a API aceita até 7 dias para trás, então o envio atrasado cai no dia certo.
- **Demais ações** (criar/editar hábito, subir de nível, objetivos, revisões) continuam exigindo internet:
  ficam desabilitadas offline, com aviso.
- Indicador global de "Offline" / "Sincronizando…".
- Ao implementar, atualizar a regra do CLAUDE.md: dados podem vir de cache local do app, nunca do service worker.
