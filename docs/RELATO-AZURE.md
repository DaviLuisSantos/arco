# Relato: esteira de deploy do Arco no Azure App Service

Registro do que foi feito para publicar o Arco no Azure, com as decisões e os problemas encontrados.
O guia operacional (passo a passo) está em [DEPLOY.md](DEPLOY.md). Este arquivo é a história por trás dele.

Outubro de 2026.

## Contexto

- **O app:** Arco, um app pessoal de hábitos. Backend .NET 10 (Minimal APIs + EF Core + SQLite) e frontend
  React 19 + Vite (PWA com Web Push), num único processo: a API serve o build do React no `wwwroot`.
- **O ponto de partida:** a versão anterior (HabitSystem, .NET 9) já rodava num App Service Windows no plano
  **Free (F1)**, com o workflow gerado pelo Deployment Center do portal.
- **O objetivo:** CI/CD do zero para o Arco, no mesmo plano gratuito, sem segredos de longa duração no GitHub.

## O que foi construído

Um workflow do GitHub Actions (`.github/workflows/deploy.yml`) com dois jobs.

**build** (todo PR e todo push):
1. Frontend: `npm ci`, lint (oxlint) e build (Vite + service worker do PWA).
2. Backend: `dotnet test` (72 testes de domínio e de integração).
3. Copia o build do React para o `wwwroot` e publica a API **auto-contida para `win-x86`**.

**deploy** (só na `main`, no environment `production`):
1. Login no Azure por **OIDC** (credencial federada numa managed identity), sem senha nem publish profile no GitHub.
2. Consulta o hostname real do Web App (`az webapp show`).
3. Aplica a configuração que não é segredo via `az webapp config`: ambiente, caminho do banco, worker 32 bits,
   HTTP/2, TLS 1.2 e FTP desligado. A infraestrutura fica descrita no próprio repositório.
4. Zip deploy (`azure/webapps-deploy`).
5. Smoke test em `/api/health`, com retentativas. Se o app não sobe, o deploy fica vermelho.

O deploy só roda depois que as variáveis do Azure existem, então a esteira pôde entrar na `main` antes da
infraestrutura estar pronta sem gerar execuções falhas.

## Recursos no Azure

| Recurso | Nome | Observação |
|---|---|---|
| Resource group | `arco_group` | |
| Web App (Windows) | `arco` | Plano Free (F1), runtime irrelevante (pacote auto-contido) |
| User-assigned managed identity | `arco-github` | Brazil South; usada só pelo GitHub Actions |
| Federated credential | `arco-production` | Issuer do GitHub, entity = Environment `production` |
| Role assignment | Website Contributor | Escopo: só o Web App `arco` (menor privilégio) |
| App settings (manuais) | `Auth__PasswordHash`, `Push__Subject` | Segredos ficam no Azure, não no GitHub |

No GitHub: secrets `AZURE_CLIENT_ID`, `AZURE_TENANT_ID` e `AZURE_SUBSCRIPTION_ID`; variáveis `AZURE_WEBAPP_NAME`
e `AZURE_RESOURCE_GROUP`.

## Resultado

O primeiro deploy passou de primeira, cerca de 7 min no total: build 6m19s e deploy 1m6s. O smoke test
respondeu `{"status":"healthy","version":"0.1.0"}` na segunda tentativa, enquanto o app iniciava. Conferi o app no ar:

- A interface (`/`) e o service worker (`/sw.js`) respondem 200.
- A API (`/api/today`) devolve 401 sem login, como esperado.

Endereço do Azure: `https://arco-<hash>.<região>.azurewebsites.net`. Endereço público atual:
`https://arco.daviluis.dev` (proxy do anterior).

## Decisões técnicas

- **Auto-contido para `win-x86`.** O plano Free do Windows só roda processos de 32 bits, e o runtime .NET 10
  poderia não estar instalado no App Service. Publicar o app com o próprio runtime elimina as duas dependências
  ao custo de um pacote maior (cerca de 107 MB). Validei localmente: o `.exe` x86 sobe em `Production`, cria o
  banco e responde no `/api/health`.
- **Dados fora do pacote.** O zip deploy substitui a pasta do app (e pode montá-la como somente leitura). O
  SQLite, as chaves de Data Protection (cookies de login) e as chaves VAPID (push) foram apontados para
  `D:\home\data`, que é persistente no App Service. Sem isso, cada deploy apagaria o banco e deslogaria o usuário.
- **OIDC em vez de publish profile.** Nenhuma credencial de longa duração no GitHub: o token é emitido por
  execução e só vale para o environment `production` deste repositório.
- **Menor privilégio.** A identidade tem *Website Contributor* só no Web App, não no grupo nem na assinatura.
- **Segredos no Azure, configuração no código.** O hash da senha fica nas App settings do portal; o restante
  (caminho do banco, TLS, 32 bits) é aplicado pela esteira a cada deploy, então não depende de clique manual.
- **Fail fast.** Sem `Auth__PasswordHash` o app recusa subir em produção, e o smoke test transforma isso num
  deploy vermelho em vez de um site quebrado em silêncio.

## Problemas encontrados

1. **O portal sugere um plano pago por padrão.** O formulário de criação do Web App vinha com um *Premium V3
   P0V3* e um grupo de recursos novo. Para reaproveitar o plano Free existente, a região e o sistema
   operacional precisam ser iguais aos do plano, senão ele nem aparece na lista. Lição: conferir o
   *Pricing plan* na tela de revisão antes de criar.
2. **Hostname com sufixo aleatório.** O Azure passou a gerar hostnames únicos
   (`arco-<hash>.<região>.azurewebsites.net`), e a esteira supunha `<nome>.azurewebsites.net`. Correção: a
   esteira consulta o `defaultHostName` real via CLI e usa esse valor no link do environment e no smoke test.
3. **Formato novo do *subject* do OIDC.** A tela de credencial federada pediu *Organization ID* e *Repository
   ID* e gerou um subject com IDs imutáveis:
   `repo:DaviLuisSantos@83884291/arco@1429116785:environment:production`, diferente do formato clássico
   `repo:owner/repo:environment:production`. Antes de salvar, confirmei pela API do GitHub
   (`GET /repos/{owner}/{repo}/actions/oidc/customization/sub` → `use_immutable_subject: true`) que o token
   emitido pelo GitHub usaria o mesmo formato. Se não batesse, o login falharia com um erro pouco claro de
   *subject mismatch*.
4. **Limites do plano Free.** Sem *Always On*, o app hiberna após cerca de 20 min sem requisições, e com ele o
   loop que envia os lembretes push. A solução sem custo é um monitor externo (UptimeRobot etc.) chamando
   `/api/health` a cada 5 min. Um cron do GitHub Actions estouraria os minutos gratuitos num repositório privado.

## O que eu faria diferente

- **Começar pelo Deployment Center e trocar o yml.** O portal cria a identidade, a credencial federada, a
  permissão e os secrets em poucos cliques. O workflow que ele gera não serve para este app (não builda o
  frontend, não persiste o banco, não testa), mas dava para aproveitar a parte de identidade e substituir o
  arquivo. Teria poupado os passos manuais de IAM.
- **Infraestrutura como código.** Um Bicep (ou `az` num script) para o Web App, a identidade, a credencial
  federada e o role assignment tornaria o ambiente reproduzível em um comando.
- **Backup automático do SQLite**, por exemplo um job agendado que copia o banco para um Blob Storage.

## Como contar essa experiência

> Montei do zero a esteira de CI/CD de um app .NET 10 + React no Azure App Service, no plano gratuito. Usei
> GitHub Actions com autenticação OIDC por managed identity e credencial federada, sem segredos de longa
> duração no repositório, e role com escopo só no Web App. Como o plano Free só roda processos de 32 bits,
> publiquei a aplicação auto-contida para `win-x86`, sem depender do runtime instalado no servidor, e movi o
> SQLite e as chaves de criptografia para o armazenamento persistente do App Service, para não perder dados a
> cada deploy. A esteira roda lint, build e testes em todo PR, aplica a configuração do app por CLI e termina
> com um smoke test no health check. No caminho resolvi o hostname com sufixo aleatório que o Azure passou a
> gerar e validei, pela API do GitHub, o formato novo do *subject* do OIDC com IDs imutáveis antes de criar a
> credencial federada.

Termos que vale saber explicar: OIDC / workload identity federation, managed identity (user-assigned vs
system-assigned), RBAC com menor privilégio, zip deploy, publicação self-contained vs framework-dependent,
plano Free (sem Always On, 32 bits, cota de CPU), smoke test, GitHub environments.
