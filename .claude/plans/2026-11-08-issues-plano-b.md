# Plano B (produto): issues do Arco

Sessão paralela. Outra sessão executa o outro plano ao mesmo tempo em outro worktree: não mexa fora de `D:/Projetos/Arco/arco-b`.
Caminhos curtos (`Domain/…`, `Infrastructure/…`, `Features/…`) são relativos a `src/Arco.Api/`. Portas E2E deste plano: `E2E_API_PORT=5399 E2E_WEB_PORT=4399`.

## Como executar
Para cada onda, em ordem:
1. Dispare todos os agentes `executor` da onda numa única mensagem, em paralelo, com `model` e `effort` da tarefa. Prompt: "Execute a tarefa T<n> do PLAN.md (leia só a seção da tarefa, os Contratos e as Regras comuns). Relatório em no máximo 10 linhas."
2. Aguarde todos. Rode a verificação da onda e mostre na conversa só erros ou as últimas linhas.
3. Se falhar, corrija antes de seguir. Faça o commit da onda, marque a onda como concluída neste arquivo e escreva: "Onda <n> concluída: verificação passou."
Se uma tarefa precisar editar arquivo fora da lista dela, o executor para e reporta. Não peça confirmações: pare só por erro que não consiga corrigir.
Ao fim, siga "Encerramento de cada plano" e resuma o que foi feito.

## Regras comuns (vão nos dois PLAN.md)
- **Portas E2E:** Plano A usa `E2E_API_PORT=5299 E2E_WEB_PORT=4299`; Plano B usa `5399/4399`.
- **Commits:** um por onda, depois da verificação. `git add` sem incluir `PLAN.md` nem `.claude/`. Nenhuma atribuição a IA no commit (sem `Co-Authored-By`). Use `Closes #N` quando a issue estiver completa e `Refs #N` quando sobrar parte manual.
- **Só um executor por onda roda Playwright.**
- **Arquivos compartilhados entre A e B:**
  - `docs/DEPLOY.md`: A só acrescenta linhas na tabela do §2; B só acrescenta uma seção nova no fim.
  - `docs/repo_map.md`: cada plano só acrescenta ou altera linhas da própria área.
  - `docs/PRODUCT.md`: A acrescenta o aviso ao parceiro; B só mexe em listas de ferramentas MCP, se houver.
- **Encerramento de cada plano:**
  1. `git fetch origin && git rebase origin/main`. Em conflito de docs, manter os dois lados. O commit da `plano-base` some sozinho se o outro plano já o tiver levado.
  2. Rodar a verificação final.
  3. `git push origin HEAD:main`. Se for rejeitado, voltar ao passo 1.
  4. Mover o `PLAN.md` para `D:\Projetos\Arco\arco\.claude\plans\2026-11-08-issues-plano-<a|b>.md` (pasta da cópia principal, porque `.claude` não é versionado).
- **Verificação final:** `dotnet test`; `cd web && npm run lint && npm run build`; `npm run e2e` com as portas do plano; `dotnet publish src/Arco.Api -c Release -r win-x86 --self-contained`.

---

# PLANO B: produto e infra

## Contratos
- `PublicProfileSettingsDto` ganha `string? Url` (`{App:BaseUrl}/u/{slug}`), aditivo. Espelhar no cliente em `web/src/api`.
- Ferramentas MCP novas: `ver_parceiros`, `ver_resumo_parceiro(id)`, `configurar_perfil_publico(ativo?, slug?)` (PUT parcial; sem argumentos, só devolve o estado e o link), `ver_plano`.
- Terraform: pasta `infra/`. **Nomes reais** (de `docs/RELATO-AZURE.md`): RG `arco_group`, Web App `arco`, Brazil South, F1, identidade `arco-github`, credencial `arco-production`. Nome do plano e subject do OIDC como variáveis.
- Segredos só como referência `@Microsoft.KeyVault(...)`: `Auth__PasswordHash`, `Email__ResendApiKey`, `Stripe__SecretKey`, `Stripe__WebhookSecret`, `Auth__Google__ClientSecret`, `Push__VapidPrivateKey` (confirmar uso em `VapidKeys.cs`), `Sentry__Dsn`, `APPLICATIONINSIGHTS_CONNECTION_STRING`, `ServiceBus__ConnectionString`. O Terraform **não** cria valores de segredo, para não irem ao state.

## T1: ferramentas MCP (#6)
- Arquivos: `Features/Mcp/ArcoTools.cs`, `Features/Mcp/McpInstructions.cs`, `Features/Profiles/ProfileEndpoints.cs`, `tests/Arco.Api.Tests/McpTests.cs`, `docs/API.md`, `docs/PRODUCT.md` (só lista de ferramentas, se houver), `web/src/api/*` (tipo do DTO)
- Corrigir `docs/API.md:191`: o `PUT /api/profile/public` aceita chave de API.
- Pronto: `McpTests` chama as 4 ferramentas; `API.md:237-240` atualizado; `dotnet test` passa.
- modelo: sonnet · esforço: medium

## T2: Azure DevOps (#13)
- Arquivos: `azure-pipelines.yml` (novo), `README.md`
- `trigger: main`, ubuntu, .NET 10.0.x, Node 24: `dotnet test`, `npm ci`, `npm run lint`, `npm run build`. Sem deploy.
- Badge fica em comentário HTML no README até o pipeline existir.
- Pronto: YAML válido; nenhum passo de deploy.
- modelo: sonnet · esforço: low

## T3: Terraform (#9)
- Arquivos: `infra/*` (novos: `versions.tf`, `main.tf`, `variables.tf`, `outputs.tf`, `backend.hcl.example`, `IMPORT.md`), `.gitignore`, `.github/workflows/deploy.yml`, `docs/DEPLOY.md` (seção nova no fim)
- `azurerm_windows_web_app`: 32 bits, `always_on = false` (F1), HTTP/2, TLS 1.2, FTPS off; domínio `arco.daviluis.dev`; Key Vault com RBAC e identidade da Web App; backend `azurerm` parcial.
- `app_settings` é o mapa inteiro: listar **todas** as settings atuais (`DEPLOY.md:40-49`, `Mcp__SelfBaseUrl`, as de `deploy.yml:134-140`). `IMPORT.md` exige conferir o `plan` com zero destroy e zero setting removida.
- OIDC: a credencial atual só vale para `environment:production`, então criar uma para `pull_request`.
- Job `terraform-plan` em PR, com `if: vars.TF_PLAN_ENABLED == 'true'`, sem bloquear o deploy e sem apply. Manter os `az webapp config` atuais do `deploy.yml`.
- `.gitignore`: `.terraform/`, `*.tfstate*`, `*.tfvars`, mantendo os `.example`.
- Pronto: `D:\Projetos\Arco\.tools\terraform.exe fmt -check`, `init -backend=false` e `validate` passam em `infra/`.
- modelo: sonnet · esforço: high

## T4: página pública (#5)
- Arquivos: `Features/Profiles/PublicProfilePage.cs`, `web/e2e/public-profile.spec.ts` (novo, projeto `screenshots`, seed no próprio arquivo)
- Refazer o visual com os tokens da landing (`landing/index.html:33-41`, radius 18px, arco SVG), mantendo o HTML no servidor e a cor do tema do usuário. Conferir OG (SkiaSharp, fontes Inter) nos 5 temas.
- Seed: conta demo (Pro, para temas) com ciclo, hábitos, check-ins e **focus sessions** (as pedras vêm delas). `/u/...` não passa no proxy do preview: usar `http://localhost:${E2E_API_PORT}`.
- Pronto: screenshots em 390 e 1280 px, PT e EN, abertos e revisados; o executor olha as imagens e o OG de cada tema, sem texto cortado.
- modelo: sonnet · esforço: medium

## T5: E2E dos fluxos novos (#4)
- Arquivos: `web/e2e/helpers.ts`, `web/e2e/totp.ts` (novo, `node:crypto`, SHA1, 6 dígitos, 30 s), `web/e2e/security.spec.ts`, `web/e2e/partners.spec.ts`, `web/e2e/plan-limit.spec.ts`, `web/e2e/locale.spec.ts` (novos), `web/src/features/habits/HabitFormDialog.tsx`
- **Lacuna de produto:** o 6º hábito hoje só mostra toast. Renderizar `PlanLimitNotice` (com link "Ver plano") dentro do dialog; o resto de `usePlanLimitHandler` não muda.
- Rótulos e fluxos: ver `SecurityCard.tsx`, `LoginPage.tsx`, `PartnersCard.tsx`, `AcceptInvitePage.tsx`. Segredo TOTP lido do `<code>` do dialog. Segunda sessão com `browser.newContext()`. Usar `getByRole('dialog')` por causa do título repetido. Se o mesmo código for recusado no login, gerar o do passo seguinte.
- Pronto: as specs passam com `E2E_API_PORT=5399 E2E_WEB_PORT=4399`.
- modelo: sonnet · esforço: high

## T6: docs de produto
- Arquivos: `docs/repo_map.md`, `CLAUDE.md` (linha de Produção, se precisar), `README.md` (seção de CI, se precisar)
- Registrar `infra/`, `azure-pipelines.yml` e as specs novas.
- Pronto: docs refletem o código.
- modelo: sonnet · esforço: low

## Ondas
- [x] Onda 1: T1, T2, T3, T4
- [x] Onda 2: T5, T6

## Verificação por onda
`dotnet build && dotnet test`; `cd web && npm run lint && npm run build`; `E2E_API_PORT=5399 E2E_WEB_PORT=4399 npm run e2e`. Na onda 1, também `terraform validate` em `infra/`.

