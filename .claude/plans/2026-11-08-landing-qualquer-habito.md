# Landing: mostrar que o Arco vale para qualquer hábito

## Contexto
Feedback: quem viu a landing achou que o app é só para estudo e academia. O produto é genérico, mas todos os exemplos (Correr, Inglês, Ler, escada em minutos, objetivo "Correr 10 km") puxam para esse lado.
Só muda conteúdo estático; a estrutura e o JS ficam iguais.

## Contratos
Valem para as duas tarefas.

- Manter `data-xp` (10,4 / 8,3 / 6,2), botões, classes e o `<h1>`: o JS faz `split` em `data-xp` e `web/e2e/app.spec.ts:56-59` confere o h1 em pt-BR.
- Não precisa trocar `buster` nem versão de cache: o precache do Workbox usa hash de conteúdo.

| Peça | pt-BR | en |
|---|---|---|
| Linha 1 do mockup (`data-xp="10,4"`) | Meditar · `Nível 1 · 10 min (mínimo: 2 min)` | Meditate · `Level 1 · 10 min (minimum: 2 min)` |
| Linha 2 (`data-xp="8,3"`) | Beber água · `Nível 2 · 2 L (mínimo: 1 L)` | Drink water · `Level 2 · 2 L (minimum: 1 L)` |
| Linha 3 (`data-xp="6,2"`) | Ler · sem mudança | Reading · sem mudança |
| Abrangência (novo `<p class="areas">`, entre `.lead` e `.cta`) | Saúde, mente, estudo, trabalho, finanças: qualquer hábito que você queira levar a sério. | Health, mind, study, work, money: any habit you want to take seriously. |
| Escada, nível 1 | 1,5 L / mínimo: 500 ml | 1.5 L / minimum: 500 ml |
| Escada, nível 2 (atual) | 2 L / mínimo: 1 L | 2 L / minimum: 1 L |
| Escada, nível 3 (planejado) | 2,5 L / mínimo: 1,5 L | 2.5 L / minimum: 1.5 L |
| Card de objetivo (L241) | "Guardar R$ 1.000 até 30/11" | "Save $1,000 by Nov 30" |

CSS novo, junto das regras do hero (depois de `.lead`, L88):
```css
.areas { max-width: 500px; margin: -18px 0 30px; color: var(--muted); font-size: .95rem; }
```
No `@media (max-width: 860px)` da L84, acrescentar `.hero .areas { margin-inline: auto; }`.

## T1: landing pt-BR
- Arquivos: `web/public/landing/index.html`
- Dependências: nenhuma
- Pronto: todas as linhas pt-BR da tabela aplicadas; sem "Correr", "Inglês" ou escada em minutos no arquivo.
- modelo: sonnet · esforço: low

## T2: landing en
- Arquivos: `web/public/landing/en/index.html`
- Dependências: nenhuma
- Pronto: todas as linhas en da tabela aplicadas; sem "Running", "English" ou "Run 10 km" no arquivo.
- modelo: sonnet · esforço: low

## Ondas
- [x] Onda 1: T1, T2

## Verificação (fim da onda 1)
1. `cd web && npm run build`
2. `grep -n -E 'Correr|Inglês|Running|>English<|Run 10 km' web/dist/landing/index.html web/dist/landing/en/index.html` não retorna nada.
3. `cd web && npx playwright test e2e/app.spec.ts` (inclui a checagem do h1 da landing).
4. Abrir `/landing/` e `/landing/en/` em desktop e em largura de celular: linha nova alinhada, mockup sem quebra estranha e botões somando XP.
