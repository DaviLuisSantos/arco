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
- Duração padrão de 6 semanas (42 dias), configurável na criação: número **par** de semanas, de 2 a 12.
- Tem uma **meta semanal de foco** (60 a 3360 min), definida na criação e editável.
- No máximo **um ciclo ativo** por vez.
- Tem de 0 a 3 objetivos (o limite é recomendação de UI, não regra rígida do backend).
- **Excluir um ciclo** (ativo ou encerrado, para os criados ou encerrados por engano) apaga seus objetivos e marcos e
  tira todo o XP que eles deram, inclusive o do encerramento. Os hábitos continuam. Excluir um objetivo também tira o
  XP dele (conclusão e marcos).
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
- **Escada planejada** (opcional): ao criar ou editar o hábito, dá para listar os próximos níveis (dose e mínimo de cada
  um, até 10). "Subir de nível" já vem preenchido com o próximo degrau (editável) e o consome. A página do hábito mostra
  os degraus planejados.
- **Regra de subida**, escolhida por hábito:
  - **80% em 14 dias** (padrão), detalhada abaixo;
  - **Semanas seguidas**: N semanas seguidas (1–8) com pelo menos X dias (1–7) com Feito ou Mínimo. Só contam semanas
    inteiras (segunda a domingo) no nível atual; a semana atual entra assim que bate a meta. Ex.: inglês "3 semanas
    seguidas com 5+ dias".
- **Sugestão de subir de nível**: quando a regra é cumprida, o app sugere subir (tela Hoje e página do hábito). Na regra padrão, com ≥ 80% de consistência nos últimos 14 dias, todos no nível atual:
  - Só "Feito" conta: o Mínimo mantém o hábito vivo, mas não mostra que a dose ficou fácil.
  - A janela termina hoje se hoje já tem check-in; senão, ontem (um "hoje" ainda não marcado não derruba a taxa).
  - Esperado: Daily = 14; SpecificDays = dias da lista na janela; WeeklyTarget = N × 2.
  - "Agora não" esconde a sugestão por 7 dias.
- **Voltar um nível** (sempre manual): na página do hábito, a partir do nível 2. O nível atual sai do histórico e volta
  para o topo da escada planejada (a próxima subida já vem com ele). O XP já ganho continua; subir de novo dá XP.
  - **Sugestão de voltar**: quando as 2 últimas semanas terminadas, inteiras no nível atual, ficaram abaixo da metade
    da meta semanal (Feito e Mínimo contam). Meta = dias da regra de semanas, se houver; senão a frequência (Diário 7,
    dias específicos = quantidade de dias, X/semana = X). Ex.: inglês com "5+ dias" → 2 dias ou menos em 2 semanas.
  - Aparece na tela Hoje e na página do hábito; "Agora não" adia por 7 dias. Nunca aparece junto com a de subir.
- Lembrete opcional por hábito (horário local): push nos dias em que ele é devido e ainda não foi marcado.
- Arquivar e restaurar.

### Sequência (streak)
- Cada hábito tem uma sequência atual e um recorde, calculados a partir do histórico (não dão XP).
- Diário e dias específicos: conta os **dias devidos** com Feito ou Mínimo. Meta semanal: conta as **semanas** com a meta batida.
- Tolerância: **uma falha isolada por semana é perdoada** (mantém a sequência, mas não soma). Duas falhas seguidas,
  ou duas na mesma semana (segunda a domingo), zeram a sequência.
  - Falha = dia devido com "Não fiz" ou sem check-in. Na meta semanal, uma semana com 1 a menos que a meta é a falha
    perdoável (meta de 1×/semana não tem tolerância); mais que isso zera.
  - Hoje ainda sem check-in (ou a semana atual sem a meta batida) fica pendente, não é falha. A primeira semana
    incompleta da meta semanal também não conta como falha.
  - A frequência atual vale para todo o histórico; o início é a criação do hábito (ou o check-in mais antigo).
- Aparece como 🔥 N no card da tela Hoje e com o recorde na página do hábito.

### Conquistas
- Medalhas que desbloqueiam sozinhas a partir do que você já faz. **Não dão XP** e não se perdem no uso normal
  (desfazer um check-in não tira uma conquista). Só a exclusão de dados (ciclo ou objetivo) retira as conquistas que
  deixaram de valer.
- Catálogo (16): sequência de 7, 30 e 100 dias; meta semanal 4 e 12 semanas seguidas; 1º, 100 e 500 check-ins
  (Feito ou Mínimo); 10 dias salvos com o Mínimo; hábito no nível 2 e no 5; jogador nível 5 e 10; 1º objetivo
  concluído; 1º ciclo encerrado; 4 revisões semanais. Contam todos os hábitos, inclusive arquivados.
- Ao desbloquear, aparece o aviso "Conquista desbloqueada". O Perfil mostra todas: as desbloqueadas com a data, as
  bloqueadas em cinza com o progresso.
- O que já tinha sido conquistado antes de as conquistas existirem desbloqueia em silêncio.

### Histórico e estatísticas
- **Histórico do hábito** (página do hábito): calendário mês a mês (‹ ›), da semana de segunda a domingo, com o resumo
  do mês (Feito, Mínimo, Não fiz). Vai do mês de início do hábito até o mês atual.
- **Estatísticas** (link no Perfil):
  - gráfico das últimas 12 semanas, uma medida por vez: **Consistência** (dias com Feito ou Mínimo ÷ dias esperados,
    somando os hábitos) ou **XP**. Tocar numa barra mostra os números da semana; a semana atual vem hachurada e conta
    só até hoje;
  - últimos 30 dias: XP, totais de check-in e consistência por hábito (mesma visão das revisões).
- Os esperados usam a frequência atual e começam na criação do hábito ou num check-in retroativo anterior a ela.

### Check-in
- Um por hábito por dia: `Done` (Feito), `Minimum` (Mínimo) ou `Missed` (Não fiz).
- Pode ser alterado ou desfeito. Datas futuras são proibidas; até 7 dias no passado são permitidos.
- "Hoje" é calculado no fuso configurado (padrão `America/Sao_Paulo`).

### Foco e arco
- Sessões de foco registram minutos dedicados a um hábito; **não dão XP** nem afetam streak.
- O ciclo é um arco de pedras: **uma pedra por semana**, que se enche com os minutos de foco da semana, em relação à meta semanal do ciclo. Cada dia com foco vira um **tijolo** na pedra, com faixas por hábito. A tela Hoje mostra a **pedra do dia**.
- Ao encerrar o ciclo, a **pedra-chave** encaixa e o arco fica completo; ele passa a aparecer em "Ciclos anteriores".
- Semanas pares formam o arco (layout `Arch`); ciclos com número ímpar de semanas (ex.: `endDate` editado) usam o layout `Row`, em fileira.
- Minutos manuais no check-in são **únicos por hábito e dia** (informar de novo substitui; 0 remove). Sessões do timer se somam a eles, o que é intencional.
- O timer não pune: parar antes ou não usar não tira nada.

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
3. **Polimento** — streaks com tolerância (1 falha por semana), escada planejada e regra de subida por hábito, conquistas, histórico e estatísticas.

## API e chaves de API
- Tudo o que o app faz é feito pela API (`docs/API.md`), e a API aceita, além do cookie do app, **chaves de API**
  (`Authorization: Bearer arco_…`): scripts, automações e assistentes de IA podem operar o sistema inteiro.
- As chaves são criadas e revogadas no Perfil (Acesso pela API). A chave aparece uma única vez; o servidor guarda só
  o hash. Cada chave dá acesso total, menos gerenciar chaves (uma chave vazada não cria outras).
- O Perfil mostra o nome, o começo e o último uso de cada chave.
- **Servidor MCP** em `/api/mcp`: assistentes de IA (Claude etc.) operam o Arco direto na conversa com uma chave de
  API. Há ferramentas para o dia a dia (ver o dia, check-in, criar hábito, subir ou voltar nível, objetivos, revisão
  semanal, estatísticas, conquistas) e `chamar_api` + `ler_documentacao_api` para todo o resto.

## Uso offline
O app abre e funciona sem internet para **ler** e **marcar check-in**:
- O cache do app (TanStack Query) fica salvo no aparelho (IndexedDB) por até 7 dias. Sem rede, cada tela mostra a
  última resposta, com o aviso "Offline — dados de HH:mm" (a hora da resposta mais antiga entre as que estão na tela).
  O service worker continua sem cachear `/api`.
- Uma tela que nunca foi aberta com internet neste aparelho avisa que carrega quando a conexão voltar.
- Check-ins feitos sem rede entram numa fila salva no aparelho (sobrevive a fechar o app), aparecem na hora e são
  enviados um de cada vez, na ordem, quando a conexão volta; o servidor recalcula o XP oficial. Cada check-in leva a
  própria data e a API aceita até 7 dias para trás, então o envio atrasado cai no dia certo.
- As demais ações (criar/editar hábito, subir de nível, objetivos, revisões) precisam de internet: offline, elas
  falham na hora com o aviso "Você está offline. Sem internet, só o check-in funciona."
- Indicador global: "Offline" (com quantos check-ins estão na fila) e "Sincronizando…" enquanto a fila é enviada.

## Pendências (em 22/10/2026)

**Verificar em produção e no uso real**
- MCP: criar a chave "Claude Code" no Perfil e conectar
  (`claude mcp add --transport http arco https://<host>/api/mcp --header "Authorization: Bearer arco_…"`).
- Backup: o cartão do Perfil deve mostrar a cópia automática do dia; baixar a primeira cópia para fora do Azure.
- Offline: testar no celular em modo avião (check-in na fila, "Sincronizando…" ao voltar a conexão).
- Escada do inglês: cadastrar 25/35/45/60 min com a regra "3 semanas seguidas, 5+ dias" e acompanhar as sugestões de
  subir e de voltar.
- Histórico: navegação entre meses (só aparece com dados de mais de um mês).

**Opcionais de infraestrutura**
- Backup externo automático (ex.: cópia diária para um Blob Storage), para não depender do download manual.
- Infraestrutura como código (Bicep ou script `az`) para recriar o ambiente do Azure com um comando.
- Ambiente local: a porta 5080 está ocupada por outro programa nesta máquina; `NODE_ENV=production` faz o
  `npm install` pular as devDependencies (use `npm install --include=dev`).

## Próximos passos (rumo a SaaS)
Em ordem de impacto. O servidor MCP (o primeiro da lista original) já está feito (veja "API e chaves de API").

1. **Especificação OpenAPI.** Documento legível por máquina (ex.: `/api/openapi.json`) gerado a partir dos
   endpoints, com os tipos de resposta. Serve para gerar clientes, validar o contrato em teste e publicar a documentação
   para quem integra. Hoje os handlers devolvem `IResult` sem tipo declarado: será preciso anotar os tipos.
2. **Chaves só de leitura.** Escopo por chave (`leitura` ou `leitura e escrita`), para integrações que só consultam
   (dashboards, relatórios) não poderem alterar nada.

Fora desta lista, e bem maior: **multiusuário** (contas, isolamento dos dados por usuário, cobrança). É uma fase
própria; hoje o app assume um único dono em todo lugar (sem `UserId` nas tabelas).
