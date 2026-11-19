# Arco — Produto

App multiusuário de hábitos com **progresso e objetivo**: cada check-in alimenta algo maior.

```
Ciclo (6 semanas)           → começo, meio e fim; objetivos do ciclo; encerramento com revisão
 └─ Objetivo (com prazo)    → "Correr 10km até 30/11" — barra de progresso até a meta
     └─ Hábito (com níveis) → "Correr — Nível 2: 20 min, 3x/semana"
         └─ Check-in diário → Feito / Mínimo / Pulei / Não fiz → gera XP
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
  - Dias dispensados (ver "Pausa e modo férias") saem do esperado; esperado 0 = sem sugestão.
- **Voltar um nível** (sempre manual): na página do hábito, a partir do nível 2. O nível atual sai do histórico e volta
  para o topo da escada planejada (a próxima subida já vem com ele). O XP já ganho continua; subir de novo dá XP.
  - **Sugestão de voltar**: quando as 2 últimas semanas terminadas, inteiras no nível atual, ficaram abaixo da metade
    da meta semanal (Feito e Mínimo contam). Meta = dias da regra de semanas, se houver; senão a frequência (Diário 7,
    dias específicos = quantidade de dias, X/semana = X). Ex.: inglês com "5+ dias" → 2 dias ou menos em 2 semanas.
  - Semanas totalmente dispensadas são ignoradas; nas parciais a meta é proporcional (ver "Pausa e modo férias").
  - Aparece na tela Hoje e na página do hábito; "Agora não" adia por 7 dias. Nunca aparece junto com a de subir.
- Lembrete opcional por hábito (horário local): push nos dias em que ele é devido e ainda não foi marcado.
- Arquivar e restaurar.

### Sequência (streak)
- Cada hábito tem uma sequência atual e um recorde, calculados a partir do histórico (não dão XP).
- Diário e dias específicos: conta os **dias devidos** com Feito ou Mínimo. Meta semanal: conta as **semanas** com a meta batida.
- Tolerância: **uma falha isolada por semana é perdoada** (mantém a sequência, mas não soma). Duas falhas seguidas,
  ou duas na mesma semana (segunda a domingo), zeram a sequência.
  - Dia dispensado (pausado ou Pulei) é neutro: não soma nem quebra (ver "Pausa e modo férias").
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
- Um por hábito por dia: `Done` (Feito), `Minimum` (Mínimo), `Skipped` (Pulei: 0 XP, não conta como check-in) ou `Missed` (Não fiz).
- `note` opcional (até 280 caracteres) em qualquer status.
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
- Ao concluir a revisão, cada parceiro aceito é avisado uma única vez por semana: push se tiver inscrição, senão e-mail (no idioma dele). O aviso sai do Outbox e do barramento de eventos, com reentrega sem duplicar.

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

## Pausa e modo férias
- **Pulei** (`Skipped`): novo status de check-in, 0 XP, sem `XpEvent`, sem minutos; `note` opcional (≤ 280, qualquer status; ver "Nota e humor"). Funciona offline (vai pela fila do check-in).
- **Pausa** (`HabitPause`): período por hábito, `startDate` ≥ hoje − 7, `endDate` opcional (incluso, ≤ start + 365; nulo = até retomar), sem sobreposição no mesmo hábito, hábito arquivado não pausa. Retomar: pausa em curso com início hoje é apagada; senão `endDate = ontem`. Pausas futuras não mudam.
- **Modo férias**: cria uma pausa (início, fim obrigatório) em todo hábito ativo sem pausa sobreposta. Essas pausas ficam marcadas como de férias: encerrar as férias retoma as que estão em curso e apaga as futuras, sem mexer nas pausas individuais.
- **Dia dispensado** = dia pausado ou com check-in Pulei. Se o dia tiver Feito/Mínimo, conta normalmente (vale como sucesso). Senão é **neutro**:
  - sequência: não soma nem quebra (Diário/dias específicos);
  - consistência, estatísticas, revisões e parceiro: sai dos esperados;
  - meta semanal: alvo da semana = `ceil(alvo × (7 − dispensados) / 7)`; semana com 7 dispensados = neutra (não soma, não quebra, não conta como falha);
  - subir de nível (80%/14d): esperados descontam os dispensados; esperado 0 = sem sugestão. Regra de semanas seguidas: semana neutra é pulada (não quebra nem soma);
  - voltar de nível: semanas totalmente dispensadas são ignoradas (olha as 2 últimas semanas não neutras); nas parciais, meta proporcional como acima;
  - lembrete push: não sai em dia dispensado;
  - Hoje: hábito pausado hoje vai para "Pausados" (`isPaused`, `pausedUntil`), `isDueToday = false`;
  - conquistas e admin: Pulei não conta como check-in.

## Nota e humor
- **Humor** (`DailyMood`): um por usuário e dia, de 1 (muito ruim) a 5 (muito bom), registrado num card "Como foi seu dia?" na tela Hoje. Janela igual à do check-in (hoje e até 7 dias atrás). Alterar substitui; tocar de novo no mesmo valor remove. Não dá XP e não entra em conquistas, estatísticas, ciclo, perfil público nem parceiro.
- **Nota do check-in:** opcional (≤ 280) em qualquer status. `note` ausente ou `null` mantém a nota salva (tocar em outro status não a perde); `""` remove; texto substitui.
- **Uso só na revisão semanal** (visível só ao dono): as notas da semana (por dia, hábito e status) e o humor da semana, com média, dias bons (4–5) x ruins (1–2) e a consistência em cada grupo (Feito/Mínimo ÷ dias devidos, hábitos Diário e Dias específicos, sem dias dispensados; meta semanal fora). Sem dado suficiente, o app avisa em vez de mostrar número. Serve para responder "o que atrapalhou?" com evidência.
- Funciona pela API e pelo MCP (`registrar_humor`, `fazer_checkin` com `nota`); entra na exportação (`dailyMoods`).

## Ordem do dia
- **Período** (`TimeOfDay`): `Anytime` (padrão), `Morning`, `Afternoon`, `Evening`. O período atual vem da hora local (fuso de `Settings`): manhã 05:00–11:59, tarde 12:00–17:59, noite 18:00–04:59.
- **Âncora "Depois de…":** opcional, ou um hábito (`anchorHabitId`) ou um texto (`anchorText`, ≤ 60), nunca os dois (400 `anchor_conflict`). O hábito âncora deve ser do próprio usuário, não o próprio hábito, e sem ciclo (A depois de B depois de A = 400 `anchor_cycle`). Âncora arquivada ou excluída: o vínculo fica, mas o Hoje a ignora (sem posição especial, sem rótulo).
- **Ordem** (`SortOrder`): manual dentro do período. Hábito novo entra no fim do período; trocar de período leva o hábito para o fim do novo período.
- **Ordem do dia:** por período (Manhã, Tarde, Noite, A qualquer hora), depois `SortOrder`, depois nome; em seguida cada hábito encadeado a um hábito do mesmo período vai para logo depois da âncora (cadeias A→B→C ficam em sequência). Âncora em outro período vale só como rótulo.
- **Destaque da vez:** `anchorDone` é verdadeiro quando a âncora-hábito teve Feito/Mínimo hoje e o hábito ainda não tem check-in hoje.
- **Hoje:** grupos por período (só os não vazios); o do período atual vem marcado "Agora", sem reordenar os grupos; "A qualquer hora" fica no fim. Pausados e "fora de hoje" continuam nas seções atuais, também na ordem do dia.
- **Organizar:** modo no Hoje com setas subir/descer por grupo (só hábitos ativos do período, inclusive os não devidos hoje), salvo por `PUT /api/habits/order`.

## Hábito quantitativo
- **Hábito quantitativo** = hábito com `unit` (texto, 1–16). Cada nível (atual e da escada) tem `target` (meta) e `minimumTarget` (mínimo): números > 0 com até 1 casa decimal, `minimumTarget ≤ target ≤ 100000`. Em hábito quantitativo, `levelDescription`/`minimumDescription` são opcionais: vazias, o servidor grava `"<meta> <unidade>"` e `"<mínimo> <unidade>"` (vírgula decimal no texto, ex.: "2,5 km").
- **Total do dia** (`HabitDayQuantity`): um por hábito e dia, em décimos (`TotalTenths`, inteiro) para evitar decimal no SQLite. A API usa números com 1 casa. Total 0 apaga o registro. Mesma janela do check-in (até 7 dias atrás, sem futuro). O registro soma no dia (+1, + valor ou corrigir o total).
- **Status derivado** (`Domain/QuantityRules.cs`): total ≥ meta = Feito; mínimo ≤ total < meta = Mínimo; abaixo = pendente. Ao gravar o total, na mesma transação: status não nulo cria/atualiza o `CheckIn` (mantém nota e minutos) e o `XpEvent` como o check-in normal; status nulo remove um check-in Feito/Mínimo existente com o XP (e revoga conquistas não mais cumpridas). Check-in manual Não fiz/Pulei abaixo do mínimo fica como está; ao atingir o mínimo, é substituído.
- **Check-in manual:** `PUT /api/checkins` aceita só `Missed`/`Skipped` (Feito/Mínimo → 400 `quantity_required`). `DELETE /api/checkins/...` também apaga o total do dia.
- **Subir/voltar de nível:** recalcula o status de hoje com a meta/mínimo novos (pode virar Feito, Mínimo ou pendente).
- XP, sequência, sugestões, pausa, revisões, conquistas e estatísticas seguem pelo `CheckIn` resultante, sem mudança.
- **Offline:** o app envia o total absoluto (idempotente) pela mesma fila do check-in; o card deriva o status na hora (otimista) com a mesma regra do servidor.
- **Conversão:** tornar um hábito quantitativo exige meta e mínimo no nível atual; voltar a não quantitativo (unidade vazia) mantém o histórico e apaga meta/mínimo dos níveis.

## Hábito negativo
Para acompanhar o que se quer **largar** ("não fumar", "sem açúcar"). Limpo por padrão: o dia sem recaída conta como limpo; o usuário só registra "Recaída" (e pode confirmar "Limpo" hoje).
- **Tipo** (`HabitKind`): `Positive` (padrão) ou `Negative`. Definido na criação; não muda depois (400 `kind_change`). No PUT, `kind` ausente mantém.
- **Regras do hábito:** frequência sempre `Daily` (outra → 400 `validation`); nunca quantitativo (`unit` → 400 `validation`); dose e mínimo opcionais (vazios: o servidor grava "Sem recaída" / "Sem recaída").
- **Check-in:** `Done` = Limpo, `Missed` = Recaída, `Skipped` permitido; `Minimum` → 400 `invalid_status`. XP pelas regras atuais (Done = 10 + 2 × (nível − 1); Missed = 0).
- **Dias limpos automáticos** (`CleanDaysService`, job a cada hora, chamável em teste): para cada usuário com hábito negativo ativo, sob `CurrentUser.Impersonate`, no fuso do usuário: para cada dia de `max(início do hábito, hoje − 7)` até **ontem** sem check-in e não dispensado (pausa), grava Done com XP (`CheckInEndpoints.ApplyStatusAsync`) e depois chama `AchievementService.UnlockAsync`. Idempotente. Hoje nunca é preenchido: fica pendente até virar o dia ou o usuário confirmar.
- **Dias limpos e recorde:** sequência **sem tolerância**. Recaída zera; Pulei e pausa são neutros; hoje sem check-in é pendente. Exibidos como "N dias limpo · recorde M".
- **Marcos** (`Domain/CleanMilestones.cs`): 7, 14, 30, 60, 90, 180, 365 dias. O nível L → L+1 é sugerido quando os dias limpos atuais ≥ marco[L−1] (respeita `LevelUpSnoozedUntil` e "Agora não"). Subir de nível gera a descrição automática "<marco> dias limpo". A sugestão de voltar de nível fica desligada para hábito negativo.
- Revisões, estatísticas, parceiro, conquistas e pausa seguem pelo check-in resultante, sem mudança.

## Fases
1. **MVP** — ciclo, objetivos, hábitos com níveis, check-in, XP e nível de jogador, PWA instalável, login.
4. **Multiusuário** — cadastro por e-mail e senha ou Google, verificação de e-mail, recuperação de senha, conta demo (dados de exemplo, apagada após 24h), troca de senha, exclusão de conta e exportação dos próprios dados em JSON. Cada usuário só vê os próprios dados.
2. **Retenção** — push (lembrete por hábito e revisão semanal), revisão semanal e de fim de ciclo guiadas, sugestão de subir de nível (≥ 80% de consistência em 14 dias).
5. **Vitrine SaaS** — planos Free/Pro com Stripe, onboarding, 2FA e sessões, perfil público, parceiro de responsabilidade, painel admin, pt-BR/en, OpenAPI e Scalar, Sentry.
3. **Polimento** — streaks com tolerância (1 falha por semana), escada planejada e regra de subida por hábito, conquistas, histórico e estatísticas.

## Planos, perfil público e parceiro

- **Planos:** Free (até 5 hábitos ativos, 1 chave de API, sem MCP, perfil público só no tema índigo) e Pro (sem limites e
  com temas). Quem passa do limite recebe `plan_limit` e o app mostra um aviso com link para o Plano; nada é apagado no
  downgrade. A cobrança é por assinatura mensal no Stripe (checkout e portal); sem configuração do Stripe o app roda
  sem cobrança. Contas demo e a do dono nascem Pro.
- **Onboarding:** contas novas passam por "Primeiros passos" (`/welcome`): idioma, primeiro hábito e lembretes, e dá para pular
  cada passo. Em Hoje, o estado vazio oferece atalhos de modelo (hábitos prontos com um toque), há um checklist de primeira
  sessão, e depois do primeiro check-in o app convida a ativar lembretes (o push só é pedido nesse momento) e a instalar o PWA.
- **Segurança da conta:** 2FA por app autenticador (TOTP) com 10 códigos de recuperação, e lista de sessões por aparelho,
  com revogação de uma ou de todas as outras.
- **Perfil público:** opcional, em `/u/<slug>`: nome, nível, XP, arco do ciclo e (se o usuário quiser) hábitos com nível
  e sequência. Tem prévia social (Open Graph) com imagem gerada no servidor. Fora dos buscadores (`noindex`).
- **Parceiro de responsabilidade:** o usuário gera um link de convite (7 dias); quem aceita passa a ver o resumo das duas
  últimas semanas dele (feito/mínimo/esperado por hábito). Qualquer lado pode encerrar.
- **Idiomas:** pt-BR (fonte) e en. A escolha vale para o app, os e-mails e as notificações; fica na conta e em
  `localStorage`. A API devolve mensagens em pt-BR e o app traduz pelo `code`.
- **Admin:** quem está em `Admin:Emails` vê métricas agregadas (usuários, cadastros, check-ins, usuários ativos e retenção
  por coorte semanal), sem dados pessoais.

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
- Multiusuário: testar cadastro, link de verificação/reset (log ou Resend), Google e a conta demo; conferir que o dono antigo entra com `Auth:OwnerEmail` e vê os dados.
- Offline: testar no celular em modo avião (check-in na fila, "Sincronizando…" ao voltar a conexão).
- Escada do inglês: cadastrar 25/35/45/60 min com a regra "3 semanas seguidas, 5+ dias" e acompanhar as sugestões de
  subir e de voltar.
- Histórico: navegação entre meses (só aparece com dados de mais de um mês).

**Opcionais de infraestrutura**
- Backup externo automático (ex.: cópia diária para um Blob Storage); a cópia diária do SQLite fica só no servidor.
- Infraestrutura como código (Bicep ou script `az`) para recriar o ambiente do Azure com um comando.
- Ambiente local: a porta 5080 está ocupada por outro programa nesta máquina; `NODE_ENV=production` faz o
  `npm install` pular as devDependencies (use `npm install --include=dev`).

## Próximos passos
O servidor MCP, o multiusuário, a especificação OpenAPI (`/api/openapi/v1.json`, `/api/docs`) e a cobrança já estão feitos.

1. **Tipos de resposta nas rotas antigas** do OpenAPI (as novas já têm `.Produces<T>`).
2. **Chaves só de leitura.** Escopo por chave (`leitura` ou `leitura e escrita`), para integrações que só consultam
   (dashboards, relatórios) não poderem alterar nada.

### Adendos da pesquisa de concorrentes
3. **Pausa e modo férias** (em andamento, ver seção própria): viagem ou doença não deve zerar a sequência; "pular ou pausar sem falhar" é o pedido mais comum (Strides, apps com férias).
4. ~~**Nota e humor no check-in**~~ (feito, ver "Nota e humor"): registrar o contexto do dia ajuda a entender falhas e alimenta a revisão (Habitify, Finch).
5. ~~**Ordem do dia por período e encadeamento**~~ (feito, ver "Ordem do dia"): manhã/tarde/noite e "depois de X" tornam a lista do Hoje um roteiro (Habitify, Atoms).
6. ~~**Hábito quantitativo**~~ (feito, ver "Hábito quantitativo"): metas com número (copos, páginas, km) que combinam com os níveis, em vez de só Feito/Mínimo e minutos (Habitify, Streaks).
7. ~~**Hábito negativo**~~ (feito, ver "Hábito negativo"): acompanhar "não fazer" (dias sem fumar, sem açúcar) contando os dias limpos (Streaks).
8. **Desafio cooperativo com o parceiro:** meta compartilhada da semana, em vez de só ver o resumo do outro (Habitica).
